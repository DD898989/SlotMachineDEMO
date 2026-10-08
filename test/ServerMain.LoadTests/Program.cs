using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using MySqlConnector;

namespace ServerMain.LoadTests;

public sealed class GameSession
{
    public required ulong Uid { get; init; }
    public required string Token { get; init; }
    public required ulong Gid { get; init; }
    public required ulong Cid { get; init; }
    public required int GameId { get; init; }
    public required int PlayerIndex { get; init; }
    public required int TabIndex { get; init; }
    public required AuthenticationHeaderValue AuthHeader { get; init; }
    public required byte[] SpinJsonBytes { get; init; }
}

public class Program
{
    private static string _baseUrl = "http://localhost:8080";
    private static string _dbIp = "127.0.0.1";
    private static int _dbPort = 3306;
    private static string _dbUser = "root";
    private static string _dbPass = "rootpassword";
    private static string _dbName = $"slot_loadtest_{Guid.NewGuid():N}"[..18];

    private static readonly MediaTypeHeaderValue JsonMediaType = new("application/json") { CharSet = "utf-8" };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==============================================================================");
        Console.WriteLine(" [ServerMain.LoadTests - SlotMachine 高併發閉迴路壓力測試平台 (NBomber)]");
        Console.WriteLine("==============================================================================");

        // 解析命令列參數
        int playersCount = 1000;       // 玩家數量 (預設 1000 位獨立玩家)
        int[] gameTypes = [2, 12, 13, 14]; // 預設 4 款不同遊戲 (2: Book of Ra Deluxe, 12: Slotopol, 13: 100 Jokers, 14: 50 Happy Santa)
        int tabsPerPlayer = gameTypes.Count();         // 每個玩家開啟的分頁數 (預設 4 個獨立 GID)
        int? explicitConcurrency = null;
        int durationSec = 10;          // 測試持續時間 (秒)
        int warmUpSec = 3;             // 預熱時間 (秒)
        int thinkTimeMs = 0;           // 單次 Spin 收到回傳後的等待間隔 (ms，0 為全速極限壓測)
        double bet = 10.0;             // 下注額度
        int sel = 2;                  // 選擇線數
        int authConcurrency = 50;      // 登入與建局並行度
        bool skipDbInit = false;
        bool initDbOnly = false;
        bool seedUsersOnly = false;
        bool dropDbOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--help" || args[i] == "-h")
            {
                PrintHelp();
                return 0;
            }
            if (args[i] == "--url" && i + 1 < args.Length) _baseUrl = args[++i];
            if (args[i] == "--players" && i + 1 < args.Length && int.TryParse(args[++i], out var pc)) playersCount = pc;
            if (args[i] == "--tabs" && i + 1 < args.Length && int.TryParse(args[++i], out var tc)) tabsPerPlayer = tc;
            if (args[i] == "--games" && i + 1 < args.Length)
            {
                var parsed = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                      .Select(s => int.TryParse(s, out var gid) ? gid : -1)
                                      .Where(gid => gid > 0)
                                      .ToArray();
                if (parsed.Length > 0) gameTypes = parsed;
            }
            if ((args[i] == "--concurrency" || args[i] == "--copies") && i + 1 < args.Length && int.TryParse(args[++i], out var c)) explicitConcurrency = c;
            if (args[i] == "--think-time" && i + 1 < args.Length && int.TryParse(args[++i], out var tt)) thinkTimeMs = tt;
            if (args[i] == "--bet" && i + 1 < args.Length && double.TryParse(args[++i], out var b)) bet = b;
            if (args[i] == "--sel" && i + 1 < args.Length && int.TryParse(args[++i], out var s)) sel = s;
            if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[++i], out var d)) durationSec = d;
            if (args[i] == "--warmup" && i + 1 < args.Length && int.TryParse(args[++i], out var w)) warmUpSec = w;
            if (args[i] == "--auth-concurrency" && i + 1 < args.Length && int.TryParse(args[++i], out var ac)) authConcurrency = ac;
            if (args[i] == "--dbip" && i + 1 < args.Length) _dbIp = args[++i];
            if (args[i] == "--dbport" && i + 1 < args.Length && int.TryParse(args[++i], out var p)) _dbPort = p;
            if (args[i] == "--dbuser" && i + 1 < args.Length) _dbUser = args[++i];
            if (args[i] == "--dbpass" && i + 1 < args.Length) _dbPass = args[++i];
            if (args[i] == "--dbname" && i + 1 < args.Length) _dbName = args[++i];
            if (args[i] == "--skip-db-init") skipDbInit = true;
            if (args[i] == "--init-db-only") initDbOnly = true;
            if (args[i] == "--seed-users-only") seedUsersOnly = true;
            if (args[i] == "--drop-db") dropDbOnly = true;
        }

        // 若使用者僅指定總併發度 (concurrency)，則自動換算玩家數
        if (explicitConcurrency.HasValue && !args.Any(a => a == "--players"))
        {
            playersCount = Math.Max(1, explicitConcurrency.Value / tabsPerPlayer);
        }

        int totalSessions = playersCount * tabsPerPlayer;

        if (dropDbOnly)
        {
            Console.WriteLine($"\n[清理] 正在刪除壓測暫存資料庫 ({_dbName})...");
            await DropDatabaseAsync();
            Console.WriteLine("[成功] 暫存資料庫已清理完畢！");
            return 0;
        }

        if (initDbOnly)
        {
            Console.WriteLine($"\n[初始化] 獨立執行資料庫結構與 {playersCount} 位玩家資料初始化 ({_dbName})...");
            await InitDatabaseAsync(playersCount);
            Console.WriteLine("[成功] 資料庫與初始資料初始化完成！");
            return 0;
        }

        if (seedUsersOnly)
        {
            Console.WriteLine($"\n[寫入] 獨立寫入 {playersCount} 位測試玩家與錢包資料到 ({_dbName})...");
            await SeedUsersOnlyAsync(playersCount);
            Console.WriteLine("[成功] 玩家與錢包資料寫入完成！");
            return 0;
        }

        Console.WriteLine($"目標伺服器位址: {_baseUrl}");
        Console.WriteLine($"目標測試資料庫: {_dbName}");
        Console.WriteLine($"模擬玩家總數: {playersCount} 位 (UID 1001 ~ {1000 + playersCount})");
        Console.WriteLine($"每位玩家分頁數: {tabsPerPlayer} 個獨立遊戲視窗");
        Console.WriteLine($"分頁俱樂部配置: 分頁 1~2 綁定 Club 1, 分頁 3~4 綁定 Club 2");
        Console.WriteLine($"分頁遊戲類型配置: {string.Join(" | ", Enumerable.Range(1, tabsPerPlayer).Select(t => $"分頁 {t}: {GetGameName(gameTypes[(t - 1) % gameTypes.Length])} (ID {gameTypes[(t - 1) % gameTypes.Length]})"))}");
        Console.WriteLine($"總併發遊戲數 (VU / Copies): {totalSessions} 組 (閉迴路 1 對 1 綁定)");
        Console.WriteLine($"單次 Spin 間隔 (Think Time): {thinkTimeMs} ms {(thinkTimeMs == 0 ? "(全速模式: 收到回傳立即 Spin)" : "(擬真模式: 間隔轉動)")}");
        Console.WriteLine($"單次 Spin 參數: Bet = {bet}, Sel = {sel}");
        Console.WriteLine($"壓測執行時長: {durationSec} 秒 (預熱 {warmUpSec} 秒)");
        Console.WriteLine("==============================================================================");

        if (!skipDbInit)
        {
            Console.WriteLine("\n[初始化] 正在檢查與初始化壓測資料庫與玩家資料...");
            try
            {
                await InitDatabaseAsync(playersCount);
                Console.WriteLine("[成功] 資料庫結構與初始資料初始化完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[警告] 資料庫初始化跳過或發生錯誤: {ex.Message}");
            }
        }

        // 配置高效能 Socket HTTP 連線處理常式
        var socketsHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10000,
            EnableMultipleHttp2Connections = true
        };

        using var httpClient = new HttpClient(socketsHandler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // 登入並為玩家各建立獨立遊戲場景
        Console.WriteLine($"\n[準備] 開始建立 {playersCount} 位玩家及其 {tabsPerPlayer} 個遊戲場景 (合計 {totalSessions} 組 Session)...");
        GameSession[] sessions;
        try
        {
            sessions = await SetupPlayerSessionsAsync(httpClient, playersCount, tabsPerPlayer, authConcurrency, bet, sel, gameTypes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[錯誤] 準備玩家階段失敗，請確認 C# 伺服器已啟動 ({_baseUrl})。錯誤: {ex.Message}");
            return 1;
        }

        // 定義 NBomber 核心壓測場景:
        // 閉迴路模型 (Closed Model): 每個 Virtual User (Copy) 固定綁定專屬的 GameSession (專屬 GID 與 Token)。
        // 每個 Session 必須等待上一次 Spin 響應結束 (並歷經 thinkTime) 後，才會發動下一次 Spin，嚴格避免同一 GID 併發重疊。
        var spinScenario = Scenario.Create("slot_spin_scenario", async context =>
        {
            try
            {
                // 透過 InstanceNumber 與 Session 進行 1 對 1 靜態映射
                var sessionIndex = (int)((uint)context.ScenarioInfo.InstanceNumber % (uint)sessions.Length);
                var session = sessions[sessionIndex];

                using var request = new HttpRequestMessage(HttpMethod.Post, "/slot/spin");
                request.Headers.Authorization = session.AuthHeader;
                request.Content = new ByteArrayContent(session.SpinJsonBytes);
                request.Content.Headers.ContentType = JsonMediaType;

                using var response = await httpClient.SendAsync(request);

                // 若有設定思考/轉動延遲，在收到 Response 後等待
                if (thinkTimeMs > 0)
                {
                    await Task.Delay(thinkTimeMs);
                }

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
            Simulation.KeepConstant(copies: totalSessions, during: TimeSpan.FromSeconds(durationSec))
        );

        Console.WriteLine("\n[啟動] 開始執行 NBomber 高併發壓力測試...");
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

    private static async Task<GameSession[]> SetupPlayerSessionsAsync(
        HttpClient httpClient,
        int playerCount,
        int tabsPerPlayer,
        int authConcurrency,
        double bet,
        int sel,
        int[] gameTypes)
    {
        // 1. 併發登入所有玩家
        Console.WriteLine($"\n[準備] 步驟 1/2: 正在併發登入 {playerCount} 位測試帳號 (併發度: {authConcurrency})...");
        var playerTokens = new string[playerCount + 1];
        int signedInCount = 0;
        var loginErrors = new ConcurrentBag<string>();
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = authConcurrency };

        await Parallel.ForEachAsync(Enumerable.Range(1, playerCount), parallelOptions, async (i, ct) =>
        {
            var email = $"loadtest_{i}@example.org";
            var secret = "iVI05M";
            var body = JsonSerializer.Serialize(new { email, secret });

            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "/signin");
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    using var res = await httpClient.SendAsync(req, ct);
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        var token = GetProp(doc.RootElement, "access").GetString()!;
                        playerTokens[i] = token;
                        var current = Interlocked.Increment(ref signedInCount);
                        if (current % 200 == 0 || current == playerCount)
                        {
                            Console.WriteLine($"  -> 登入進度: {current} / {playerCount} ({(current * 100.0 / playerCount):F0}%)");
                        }
                        return;
                    }
                    else
                    {
                        var err = await res.Content.ReadAsStringAsync(ct);
                        if (retry == 2)
                        {
                            loginErrors.Add($"玩家 {email} 登入失敗: HTTP {(int)res.StatusCode} - {err}");
                        }
                        else
                        {
                            await Task.Delay(150 * (retry + 1), ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        loginErrors.Add($"玩家 {email} 登入異常: {ex.Message}");
                    }
                    else
                    {
                        await Task.Delay(150 * (retry + 1), ct);
                    }
                }
            }
        });

        if (!loginErrors.IsEmpty)
        {
            throw new InvalidOperationException($"共有 {loginErrors.Count} 位玩家登入失敗！範例錯誤: {loginErrors.First()}");
        }
        Console.WriteLine($"[成功] 全部 {playerCount} 位玩家登入成功並取得 Token！");

        // 2. 併發建立遊戲場景 (分頁 1~2 為 Club 1, 分頁 3~4 為 Club 2, 各分頁使用不同遊戲類型)
        int totalSessions = playerCount * tabsPerPlayer;
        Console.WriteLine($"\n[準備] 步驟 2/2: 正在為每位玩家建立 {tabsPerPlayer} 個獨立遊戲分頁 (共 {totalSessions} 個 GID 場景)...");

        var sessions = new GameSession[totalSessions];
        int createdSessionsCount = 0;
        var sessionErrors = new ConcurrentBag<string>();

        var sessionTasks = new List<(int playerIndex, int tabIndex, int flatIndex)>(totalSessions);
        for (int p = 1; p <= playerCount; p++)
        {
            for (int t = 1; t <= tabsPerPlayer; t++)
            {
                int flatIndex = (p - 1) * tabsPerPlayer + (t - 1);
                sessionTasks.Add((p, t, flatIndex));
            }
        }

        await Parallel.ForEachAsync(sessionTasks, parallelOptions, async (item, ct) =>
        {
            var token = playerTokens[item.playerIndex];
            ulong uid = 1000 + (ulong)item.playerIndex;
            ulong cid = item.tabIndex <= 2 ? 1UL : 2UL;
            int game = gameTypes[(item.tabIndex - 1) % gameTypes.Length];
            var gameBody = JsonSerializer.Serialize(new { cid, uid, game });

            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "/game/new");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    req.Content = new StringContent(gameBody, Encoding.UTF8, "application/json");

                    using var res = await httpClient.SendAsync(req, ct);
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        ulong gid = GetProp(doc.RootElement, "gid").GetUInt64();

                        var effectiveSel = game == 11 ? 0 : sel;
                        var spinJson = JsonSerializer.Serialize(new { gid, bet, sel = effectiveSel });
                        var spinBytes = Encoding.UTF8.GetBytes(spinJson);

                        sessions[item.flatIndex] = new GameSession
                        {
                            Uid = uid,
                            Token = token,
                            Gid = gid,
                            Cid = cid,
                            GameId = game,
                            PlayerIndex = item.playerIndex,
                            TabIndex = item.tabIndex,
                            AuthHeader = new AuthenticationHeaderValue("Bearer", token),
                            SpinJsonBytes = spinBytes
                        };

                        var current = Interlocked.Increment(ref createdSessionsCount);
                        if (current % 500 == 0 || current == totalSessions)
                        {
                            Console.WriteLine($"  -> 場景建立進度: {current} / {totalSessions} ({(current * 100.0 / totalSessions):F0}%)");
                        }
                        return;
                    }
                    else
                    {
                        var err = await res.Content.ReadAsStringAsync(ct);
                        if (retry == 2)
                        {
                            sessionErrors.Add($"玩家 {uid} 分頁 {item.tabIndex} (Club {cid}, Game {game}) 建立失敗: HTTP {(int)res.StatusCode} - {err}");
                        }
                        else
                        {
                            await Task.Delay(150 * (retry + 1), ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        sessionErrors.Add($"玩家 {uid} 分頁 {item.tabIndex} (Club {cid}, Game {game}) 建立異常: {ex.Message}");
                    }
                    else
                    {
                        await Task.Delay(150 * (retry + 1), ct);
                    }
                }
            }
        });

        if (!sessionErrors.IsEmpty)
        {
            throw new InvalidOperationException($"共有 {sessionErrors.Count} 個遊戲場景建立失敗！範例錯誤: {sessionErrors.First()}");
        }
        Console.WriteLine($"[成功] 全部 {totalSessions} 組遊戲場景已建立完成！(範例 GID: {string.Join(", ", sessions.Take(5).Select(s => s.Gid))}...)");

        return sessions;
    }

    private static async Task InitDatabaseAsync(int playerCount)
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
        if (!File.Exists(serverExe))
        {
            throw new FileNotFoundException($"無法找到 server.exe 進行資料表結構同步 (--sync-only)！搜尋起點: {AppContext.BaseDirectory}");
        }

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
        using (var proc = System.Diagnostics.Process.Start(psi))
        {
            if (proc != null)
            {
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException($"server.exe --sync-only 執行失敗 (ExitCode: {proc.ExitCode})\nStdOut: {stdout}\nStdErr: {stderr}");
                }
            }
        }

        // 檢查並填入 ClubData (Club 1 與 Club 2 提供充沛水池)
        var clubInsertSql = @"
INSERT INTO `ClubData` (`ID`, `Name`, `Bank`, `Fund`, `Lock`, `Rate`, `MRTP`)
VALUES (1, 'virtual', 10000000000, 10000000000, 0, 2.5, 95),
       (2, 'club2', 10000000000, 10000000000, 0, 2.5, 95)
ON DUPLICATE KEY UPDATE `Bank` = 10000000000, `Fund` = 10000000000;";
        await using (var cmd = new MySqlCommand(clubInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // 檢查並填入基礎管理員、荷官、預設玩家
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

        var propsInsertSql = @"
INSERT INTO `Props` (`CID`, `UID`, `Wallet`, `Access`, `MRTP`)
VALUES (1, 1, 100000000, 1, 0),
       (1, 2, 100000000, 13, 0),
       (1, 3, 1000000000, 1, 95),
       (2, 1, 100000000, 1, 0),
       (2, 2, 100000000, 13, 0),
       (2, 3, 1000000000, 1, 95)
ON DUPLICATE KEY UPDATE `Wallet` = 1000000000;";
        await using (var cmd = new MySqlCommand(propsInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // 批次填入 playerCount 位壓測專用玩家與錢包 (UID 1001 ~ 1000 + playerCount)
        await SeedUsersAndPropsAsync(appConn, playerCount);
    }

    private static async Task SeedUsersOnlyAsync(int playerCount)
    {
        var appConnStr = $"Server={_dbIp};Port={_dbPort};Database={_dbName};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var appConn = new MySqlConnection(appConnStr);
        await appConn.OpenAsync();

        // 確保 Club 1 與 Club 2 均存在
        var clubInsertSql = @"
INSERT INTO `ClubData` (`ID`, `Name`, `Bank`, `Fund`, `Lock`, `Rate`, `MRTP`)
VALUES (1, 'virtual', 10000000000, 10000000000, 0, 2.5, 95),
       (2, 'club2', 10000000000, 10000000000, 0, 2.5, 95)
ON DUPLICATE KEY UPDATE `Bank` = 10000000000, `Fund` = 10000000000;";
        await using (var cmd = new MySqlCommand(clubInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        await SeedUsersAndPropsAsync(appConn, playerCount);
    }

    private static async Task SeedUsersAndPropsAsync(MySqlConnection appConn, int count)
    {
        Console.WriteLine($"[資料庫] 正在批次寫入 {count} 位測試玩家與充值錢包 (UID 1001 ~ {1000 + count})...");
        const int batchSize = 500;
        for (int start = 1; start <= count; start += batchSize)
        {
            int end = Math.Min(start + batchSize - 1, count);
            var userSb = new StringBuilder();
            userSb.Append("INSERT INTO `User` (`ID`, `Email`, `Secret`, `Name`, `GAL`) VALUES ");
            var propsSb = new StringBuilder();
            propsSb.Append("INSERT INTO `Props` (`CID`, `UID`, `Wallet`, `Access`, `MRTP`) VALUES ");

            for (int i = start; i <= end; i++)
            {
                ulong uid = 1000 + (ulong)i;
                var email = $"loadtest_{i}@example.org";
                var name = $"loadtest_{i}";
                var secret = "iVI05M";

                if (i > start)
                {
                    userSb.Append(',');
                    propsSb.Append(',');
                }
                userSb.Append($"({uid}, '{email}', '{secret}', '{name}', 1)");
                propsSb.Append($"(1, {uid}, 1000000000, 1, 95),(2, {uid}, 1000000000, 1, 95)");
            }

            userSb.Append(" ON DUPLICATE KEY UPDATE `Secret` = VALUES(`Secret`);");
            propsSb.Append(" ON DUPLICATE KEY UPDATE `Wallet` = 1000000000;");

            await using (var cmd = new MySqlCommand(userSb.ToString(), appConn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = new MySqlCommand(propsSb.ToString(), appConn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }
        Console.WriteLine($"[成功] {count} 位玩家帳號與十億點數錢包初始化完畢！");
    }

    private static async Task DropDatabaseAsync()
    {
        var adminConnStr = $"Server={_dbIp};Port={_dbPort};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var conn = new MySqlConnection(adminConnStr);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{_dbName}`;", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string GetGameName(int gameId) => gameId switch
    {
        1 => "Book of Ra (9 lines)",
        2 => "Book of Ra Deluxe (10 lines)",
        3 => "Gate of Ra Deluxe",
        4 => "Golden Prophecies",
        5 => "Down Under",
        6 => "God of Sun",
        7 => "Lord of the Ocean",
        8 => "Faust",
        9 => "The Real King Gold Records",
        10 => "Angry Birds",
        11 => "Gonzo's Quest (NetEnt)",
        12 => "Slotopol (Megajack)",
        13 => "100 Jokers (AGT)",
        14 => "50 Happy Santa (AGT)",
        15 => "40 Bigfoot (AGT)",
        16 => "American Keno",
        _ => $"Game #{gameId}"
    };

    private static string FindServerExePath()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            var candidateInPublish = Path.Combine(currentDir.FullName, "bin", "publish", "server.exe");
            if (File.Exists(candidateInPublish))
                return candidateInPublish;

            var candidateInRelease = Path.Combine(currentDir.FullName, "bin", "Release", "net10.0", "publish", "server.exe");
            if (File.Exists(candidateInRelease))
                return candidateInRelease;

            var candidateInReleaseBuild = Path.Combine(currentDir.FullName, "bin", "Release", "net10.0", "server.exe");
            if (File.Exists(candidateInReleaseBuild))
                return candidateInReleaseBuild;

            var candidateInDebugBuild = Path.Combine(currentDir.FullName, "bin", "Debug", "net10.0", "server.exe");
            if (File.Exists(candidateInDebugBuild))
                return candidateInDebugBuild;

            var candidateInBin = Path.Combine(currentDir.FullName, "bin", "server.exe");
            if (File.Exists(candidateInBin))
                return candidateInBin;

            var candidate = Path.Combine(currentDir.FullName, "server.exe");
            if (File.Exists(candidate))
                return candidate;

            currentDir = currentDir.Parent;
        }
        return "server.exe";
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
用法: dotnet run [參數] 或 ServerMain.LoadTests.exe [參數]

參數選項:
  --players <數量>           模擬玩家數量 (預設: 1000)
  --tabs <數量>              每位玩家開啟的分頁數 (預設: 4)
  --games <清單>             遊戲類型清單 (逗號分隔，預設: 2,12,13,14)
  --concurrency / --copies   總併發遊戲數 (預設依 players * tabs 換算，例如 4000)
  --think-time <毫秒>        單次 Spin 收到回傳後的等待間隔 (預設: 0，0 為全速極限壓測)
  --duration <秒>            壓測執行秒數 (預設: 30)
  --warmup <秒>              預熱秒數 (預設: 5)
  --bet <金額>               單次 Spin 下注金額 (預設: 10.0)
  --sel <線數>               單次 Spin 選擇線數 (預設: 10)
  --auth-concurrency <數量>  前置登入與建立場景的併發度 (預設: 50)
  --url <網址>               目標伺服器位址 (預設: http://localhost:8080)
  --dbip <IP>                MySQL 資料庫主機 (預設: 127.0.0.1)
  --dbport <Port>            MySQL 資料庫通訊埠 (預設: 3306)
  --dbuser <帳號>            MySQL 帳號 (預設: root)
  --dbpass <密碼>            MySQL 密碼 (預設: rootpassword)
  --dbname <庫名>            MySQL 測試資料庫名稱 (預設: 動態產生 slot_loadtest_xxxx)
  --skip-db-init             跳過資料庫與帳號初始化 (適用於已存在的資料庫)
  --init-db-only             僅執行資料庫同步與玩家資料植入後退出
  --seed-users-only          僅對現有資料庫寫入玩家與錢包點數後退出
  --drop-db                  刪除測試資料庫後退出
  --help, -h                 顯示此說明畫面
");
    }
}
