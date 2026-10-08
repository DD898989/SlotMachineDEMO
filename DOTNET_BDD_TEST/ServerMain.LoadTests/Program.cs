using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using MySqlConnector;

namespace ServerMain.LoadTests;

public class Program
{
    private static string _baseUrl = "http://localhost:8080";
    private static string _dbIp = "127.0.0.1";
    private static int _dbPort = 3306;
    private static string _dbUser = "root";
    private static string _dbPass = "rootpassword";
    private static string _dbName = $"slot_loadtest_{Guid.NewGuid():N}"[..18];

    private static long _requestCounter = 0;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==============================================================================");
        Console.WriteLine(" [ServerMain.LoadTests - SlotMachine 高併發壓力測試平台 (NBomber)]");
        Console.WriteLine("==============================================================================");

        // 解析命令列參數
        int concurrentUsers = 50;  // 併發虛擬使用者數量 (Concurrent Copies)
        int durationSec = 15;      // 測試持續時間 (秒)
        int warmUpSec = 3;         // 預熱時間 (秒)
        int sessionPoolSize = 20;  // 遊戲場景池大小 (避免全併發擠同一個 GID)
        bool skipDbInit = false;
        bool initDbOnly = false;
        bool dropDbOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--url" && i + 1 < args.Length) _baseUrl = args[++i];
            if ((args[i] == "--concurrency" || args[i] == "--copies") && i + 1 < args.Length && int.TryParse(args[++i], out var c)) concurrentUsers = c;
            if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[++i], out var d)) durationSec = d;
            if (args[i] == "--warmup" && i + 1 < args.Length && int.TryParse(args[++i], out var w)) warmUpSec = w;
            if (args[i] == "--pool" && i + 1 < args.Length && int.TryParse(args[++i], out var pSize)) sessionPoolSize = pSize;
            if (args[i] == "--dbip" && i + 1 < args.Length) _dbIp = args[++i];
            if (args[i] == "--dbport" && i + 1 < args.Length && int.TryParse(args[++i], out var p)) _dbPort = p;
            if (args[i] == "--dbuser" && i + 1 < args.Length) _dbUser = args[++i];
            if (args[i] == "--dbpass" && i + 1 < args.Length) _dbPass = args[++i];
            if (args[i] == "--dbname" && i + 1 < args.Length) _dbName = args[++i];
            if (args[i] == "--skip-db-init") skipDbInit = true;
            if (args[i] == "--init-db-only") initDbOnly = true;
            if (args[i] == "--drop-db") dropDbOnly = true;
        }

        if (dropDbOnly)
        {
            Console.WriteLine($"\n[清理] 正在刪除壓測暫存資料庫 ({_dbName})...");
            await DropDatabaseAsync();
            Console.WriteLine("[成功] 暫存資料庫已清理完畢！");
            return 0;
        }

        if (initDbOnly)
        {
            Console.WriteLine($"\n[初始化] 獨立執行資料庫初始化 ({_dbName})...");
            await InitDatabaseAsync();
            Console.WriteLine("[成功] 資料庫與初始資料初始化完成！");
            return 0;
        }

        Console.WriteLine($"目標伺服器位址: {_baseUrl}");
        Console.WriteLine($"目標測試資料庫: {_dbName}");
        Console.WriteLine($"併發虛擬使用者 (VU / Copies): {concurrentUsers}");
        Console.WriteLine($"壓測執行時長: {durationSec} 秒 (預熱 {warmUpSec} 秒)");

        if (!skipDbInit)
        {
            Console.WriteLine("\n[初始化] 正在檢查與初始化壓測資料庫...");
            try
            {
                await InitDatabaseAsync();
                Console.WriteLine("[成功] 資料庫與初始資料初始化完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[警告] 資料庫初始化跳過或發生錯誤: {ex.Message}");
            }
        }

        using var httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        // 登入並準備多組遊戲場景池 (避免所有併發請求爭用單一 GID)
        Console.WriteLine($"\n[準備] 登入測試帳號並建立 {sessionPoolSize} 組遊戲場景池...");
        string playerToken;
        List<ulong> gids;
        try
        {
            (playerToken, gids) = await SetupPlayerSessionsPoolAsync(httpClient, sessionPoolSize);
            Console.WriteLine($"[成功] 玩家登入成功！已建立 {gids.Count} 組 GID 場景 (GID: {string.Join(", ", gids.Take(5))}...)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[錯誤] 準備玩家階段失敗，請確認 Go 伺服器已啟動 ({_baseUrl})。錯誤: {ex.Message}");
            return 1;
        }

        // 定義 NBomber 核心壓測場景: 老虎機高併發 Spin 旋轉 (/slot/spin)
        // 使用 KeepConstant 保持穩定的併發虛擬使用者 (Concurrent Virtual Users)
        var spinScenario = Scenario.Create("slot_spin_scenario", async context =>
        {
            try
            {
                var idx = (int)(Interlocked.Increment(ref _requestCounter) % gids.Count);
                var currentGid = gids[idx];

                var spinBody = new
                {
                    gid = currentGid,
                    bet = 10.0,
                    sel = 10
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "/slot/spin");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
                request.Content = new StringContent(JsonSerializer.Serialize(spinBody), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = response.Content.Headers.ContentLength ?? 0;
                    return Response.Ok(statusCode: ((int)response.StatusCode).ToString(), sizeBytes: (long)bytes);
                }
                else
                {
                    return Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(warmUpSec))
        .WithLoadSimulations(
            // 閉迴路併發模型 (Closed Model): 維持指定數量的併發虛擬使用者 (Copies) 全速發送請求
            Simulation.KeepConstant(copies: concurrentUsers, during: TimeSpan.FromSeconds(durationSec))
        );

        Console.WriteLine("\n[啟動] 開始執行 NBomber 併發壓力測試...");
        var stats = NBomberRunner
            .RegisterScenarios(spinScenario)
            .Run();

        Console.WriteLine("\n==============================================================================");
        Console.WriteLine(" [NBomber 壓力測試執行完畢]");
        Console.WriteLine("==============================================================================");

        return 0;
    }

    private static JsonElement GetProp(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Element is not an object, but {el.ValueKind}");
        }
        if (el.TryGetProperty(name, out var val)) return val;
        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }
        throw new KeyNotFoundException($"Property '{name}' not found");
    }

    private static async Task<(string token, List<ulong> gids)> SetupPlayerSessionsPoolAsync(HttpClient client, int count)
    {
        // 1. Signin
        var signinBody = new { email = "player@example.org", secret = "iVI05M" };
        var signinRes = await client.PostAsync("/signin",
            new StringContent(JsonSerializer.Serialize(signinBody), Encoding.UTF8, "application/json"));

        if (!signinRes.IsSuccessStatusCode)
        {
            var err = await signinRes.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"登入失敗: {signinRes.StatusCode}, {err}");
        }

        var signinJson = await signinRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(signinJson);
        string token = GetProp(doc.RootElement, "access").GetString()!;
        ulong uid = GetProp(doc.RootElement, "uid").GetUInt64();

        // 2. 建立多組 Game Scenes (13 = AGT 100 Jokers)
        var gids = new List<ulong>();
        for (int i = 0; i < count; i++)
        {
            var gameBody = new { cid = 1, uid = uid, game = 13 };
            using var gameReq = new HttpRequestMessage(HttpMethod.Post, "/game/new");
            gameReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            gameReq.Content = new StringContent(JsonSerializer.Serialize(gameBody), Encoding.UTF8, "application/json");

            var gameRes = await client.SendAsync(gameReq);
            if (!gameRes.IsSuccessStatusCode)
            {
                var err = await gameRes.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"建立遊戲場景失敗: {gameRes.StatusCode}, {err}");
            }

            var gameJson = await gameRes.Content.ReadAsStringAsync();
            using var gameDoc = JsonDocument.Parse(gameJson);
            ulong gid = GetProp(gameDoc.RootElement, "gid").GetUInt64();
            gids.Add(gid);
        }

        return (token, gids);
    }

    private static async Task InitDatabaseAsync()
    {
        var adminConnStr = $"Server={_dbIp};Port={_dbPort};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var conn = new MySqlConnection(adminConnStr);
        await conn.OpenAsync();

        await using (var cmd = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{_dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var appConnStr = $"Server={_dbIp};Port={_dbPort};Database={_dbName};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var appConn = new MySqlConnection(appConnStr);
        await appConn.OpenAsync();

        // 若資料表尚未存在，嘗試呼叫 server.exe --sync-only 同步結構
        var serverExe = FindServerExePath();
        if (File.Exists(serverExe))
        {
            var dsn = $"{_dbUser}:{_dbPass}@tcp({_dbIp}:{_dbPort})/{_dbName}?charset=utf8mb4";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = $"--sync-only --dbdns={dsn}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(serverExe)!
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }
        }

        // 檢查並填入 ClubData (提供充沛水池)
        var clubInsertSql = @"
INSERT INTO `ClubData` (`ID`, `Name`, `Bank`, `Fund`, `Lock`, `Rate`, `MRTP`)
VALUES (1, 'virtual', 1000000000, 1000000000, 0, 2.5, 95)
ON DUPLICATE KEY UPDATE `Bank` = 1000000000, `Fund` = 1000000000;";
        await using (var cmd = new MySqlCommand(clubInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // 檢查並填入 User
        var userInsertSql = @"
INSERT INTO `User` (`ID`, `Email`, `Secret`, `Name`, `GAL`)
VALUES (1, 'admin@example.org', '0YBoaT', 'admin', 31),
       (2, 'dealer@example.org', 'LtpkAr', 'dealer', 3),
       (3, 'player@example.org', 'iVI05M', 'player', 1)
ON DUPLICATE KEY UPDATE `Secret` = VALUES(`Secret`);";
        await using (var cmd = new MySqlCommand(userInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // 檢查並填入 Props (充值十億點數以支援海量高併發 Spin)
        var propsInsertSql = @"
INSERT INTO `Props` (`CID`, `UID`, `Wallet`, `Access`, `MRTP`)
VALUES (1, 1, 100000000, 1, 0),
       (1, 2, 100000000, 13, 0),
       (1, 3, 1000000000, 1, 95)
ON DUPLICATE KEY UPDATE `Wallet` = 1000000000;";
        await using (var cmd = new MySqlCommand(propsInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task DropDatabaseAsync()
    {
        var adminConnStr = $"Server={_dbIp};Port={_dbPort};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var conn = new MySqlConnection(adminConnStr);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{_dbName}`;", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string FindServerExePath()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "server.exe");
            if (File.Exists(candidate))
                return candidate;
            currentDir = currentDir.Parent;
        }
        return "server.exe";
    }
}
