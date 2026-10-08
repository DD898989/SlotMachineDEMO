using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace ServerMain.LoadTestsFixedLB;

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
    public required byte[][] SpinPayloads { get; init; }

    public int CompletedSteps;
    public int FailedSteps;
}

public sealed class GameStepProfile
{
    public required int GameId { get; init; }
    public required double[] WalletDeltas { get; init; }
    public required double[] FundDeltas { get; init; }
    public required double[] BankDeltas { get; init; }

    public double TotalWalletDelta => WalletDeltas.Sum();
    public double TotalFundDelta => FundDeltas.Sum();
    public double TotalBankDelta => BankDeltas.Sum();
}

public class Program
{
    private static string[] _baseUrls = ["http://localhost:18080", "http://localhost:18081"];
    private static string _dbIp = "127.0.0.1";
    private static int _dbPort = 3306;
    private static string _dbUser = "root";
    private static string _dbPass = "rootpassword";
    private static string _dbName = $"slot_fixed_{Guid.NewGuid():N}"[..18];

    private static readonly MediaTypeHeaderValue JsonMediaType = new("application/json") { CharSet = "utf-8" };

    // 10 步固定 Cheat 循環陣列 (偏移 10~19，避免觸發 Free Spins 或 Monopoly/Wheel 等互動狀態)
    public static readonly int[][] DefaultCheatPattern =
    [
        [10, 10, 10, 10, 10],
        [11, 11, 11, 11, 11],
        [12, 12, 12, 12, 12],
        [13, 13, 13, 13, 13],
        [14, 14, 14, 14, 14],
        [15, 15, 15, 15, 15],
        [16, 16, 16, 16, 16],
        [17, 17, 17, 17, 17],
        [18, 18, 18, 18, 18],
        [19, 19, 19, 19, 19]
    ];

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==============================================================================");
        Console.WriteLine(" [ServerMain.LoadTestsFixedLB - 雙伺服器負載均衡 (LB) 數據時長壓測與驗證平台]");
        Console.WriteLine("==============================================================================");

        int playersCount = 50;        // 預設 50 位測試玩家
        int[] gameTypes = [2, 12, 13, 14]; // 預設 4 款遊戲 (2: Book of Ra, 12: Slotopol, 13: 100 Jokers, 14: 50 Happy Santa)
        int tabsPerPlayer = gameTypes.Length; // 每個玩家預設 4 個分頁
        int durationSec = 10;         // 壓測持續時間 (預設 10 秒)
        int thinkTimeMs = 0;          // 單次 Spin 間隔 (0 為全速極限壓測)
        double bet = 10.0;            // 單注金額
        int sel = 2;                  // 選擇線數
        int authConcurrency = 50;     // 登入與建局並行度
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
            if ((args[i] == "--urls" || args[i] == "--url") && i + 1 < args.Length)
            {
                var parsed = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parsed.Length > 0) _baseUrls = parsed;
            }
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
            if (args[i] == "--think-time" && i + 1 < args.Length && int.TryParse(args[++i], out var tt)) thinkTimeMs = tt;
            if (args[i] == "--bet" && i + 1 < args.Length && double.TryParse(args[++i], out var b)) bet = b;
            if (args[i] == "--sel" && i + 1 < args.Length && int.TryParse(args[++i], out var s)) sel = s;
            if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[++i], out var d)) durationSec = d;
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

        Console.WriteLine($"目標伺服器位址 (LB 負載均衡): {string.Join(", ", _baseUrls)}");
        Console.WriteLine($"目標測試資料庫: {_dbName}");
        Console.WriteLine($"模擬玩家總數: {playersCount} 位 (UID 1001 ~ {1000 + playersCount})");
        Console.WriteLine($"每位玩家分頁數: {tabsPerPlayer} 個獨立遊戲視窗");
        Console.WriteLine($"俱樂部配置: 分頁 1~2 綁定 Club 1, 分頁 3~4 綁定 Club 2");
        Console.WriteLine($"遊戲種類配置: {string.Join(" | ", Enumerable.Range(1, tabsPerPlayer).Select(t => $"分頁 {t}: {GetGameName(gameTypes[(t - 1) % gameTypes.Length])} (ID {gameTypes[(t - 1) % gameTypes.Length]})"))}");
        Console.WriteLine($"總併發連線數: {totalSessions} 個 GameSession");
        Console.WriteLine($"壓測執行時長: {durationSec} 秒");
        Console.WriteLine($"單次 Spin 參數: Bet = {bet}, Sel = {sel}, ThinkTime = {thinkTimeMs} ms");
        Console.WriteLine($"固定作弊循環: 每輪 {DefaultCheatPattern.Length} 步");
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

        var socketsHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10000,
            EnableMultipleHttp2Connections = true
        };

        var httpClients = _baseUrls.Select(url => new HttpClient(socketsHandler)
        {
            BaseAddress = new Uri(url.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        }).ToArray();

        // 階段一：基準測試 (Dry Run)，對每種遊戲類型記錄步進 Delta 並驗證確定性 (透過第 1 台伺服器)
        Console.WriteLine("\n[階段一] 執行基準測試 (Dry Run)，建立各遊戲每步數值變化對照表...");
        Dictionary<int, GameStepProfile> stepProfiles;
        try
        {
            stepProfiles = await ExecuteDryRunProfilesAsync(httpClients[0], gameTypes, bet, sel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[錯誤] 基準測試失敗，請確認伺服器已正常啟動 ({_baseUrls[0]})。錯誤: {ex.Message}");
            return 1;
        }

        // 登入測試玩家並建立各場景 (輪流分散到各 LB 伺服器)
        Console.WriteLine($"\n[準備] 開始為 {playersCount} 位玩家建立 {totalSessions} 個遊戲場景 (分流到 {_baseUrls.Length} 台伺服器)...");
        GameSession[] sessions;
        try
        {
            sessions = await SetupPlayerSessionsAsync(httpClients, playersCount, tabsPerPlayer, authConcurrency, bet, sel, gameTypes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[錯誤] 建立玩家場景失敗: {ex.Message}");
            return 1;
        }

        // 階段二：取得壓測前資料庫快照 (初始資料)
        Console.WriteLine("\n[階段二] 擷取壓測前資料庫快照 (Props 錢包餘額、Club 水池、Spinlog 計數)...");
        var initialSnapshot = await CaptureDbSnapshotAsync(playersCount);
        Console.WriteLine($"  -> 初始總玩家 Props 錢包金額: {initialSnapshot.TotalWallet:N2}");
        Console.WriteLine($"  -> 初始 Club 1 Fund: {initialSnapshot.ClubFunds[1]:N2}, Bank: {initialSnapshot.ClubBanks[1]:N2}");
        Console.WriteLine($"  -> 初始 Club 2 Fund: {initialSnapshot.ClubFunds[2]:N2}, Bank: {initialSnapshot.ClubBanks[2]:N2}");
        Console.WriteLine($"  -> 初始 Spinlog 最大 ID: {initialSnapshot.MaxSpinlogId}");

        // 階段三：高併發時長壓測 (固定 durationSec 秒，輪流向兩台伺服器發送 Spin API)
        Console.WriteLine($"\n[階段三] 開始發動雙伺服器 LB 輪流高併發時長壓力測試 (持續 {durationSec} 秒)...");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
        var sw = Stopwatch.StartNew();

        var tasks = sessions.Select(session => Task.Run(() => RunSessionLoopAsync(session, httpClients, thinkTimeMs, cts.Token))).ToArray();
        await Task.WhenAll(tasks);
        sw.Stop();

        int totalCompleted = sessions.Sum(s => s.CompletedSteps);
        int totalFailed = sessions.Sum(s => s.FailedSteps);
        double actualElapsedSec = sw.Elapsed.TotalSeconds;
        double overallRps = totalCompleted / actualElapsedSec;

        Console.WriteLine($"\n[壓測結束] 實耗時: {actualElapsedSec:F2} 秒");
        Console.WriteLine($"  -> 總成功 Spin 數: {totalCompleted:N0} 次");
        Console.WriteLine($"  -> 總失敗次數: {totalFailed:N0} 次");
        Console.WriteLine($"  -> 平均吞吐量 (RPS): {overallRps:F1} req/sec");

        // 階段四：數學理論值精算 (Math Projection)
        Console.WriteLine("\n[階段四] 根據每位連線實際完成步數，精算理論資料庫快照...");
        var projection = CalculateTheoreticalProjection(sessions, stepProfiles, initialSnapshot);
        Console.WriteLine($"  -> 理論預期總 Spin 筆數: {projection.ExpectedTotalSpins:N0}");
        Console.WriteLine($"  -> 理論預期系統總 Wallet 變化: {projection.ExpectedSystemWalletDelta:N2} (預期最終總額: {projection.ExpectedTotalWallet:N2})");
        Console.WriteLine($"  -> 理論預期 Club 1 Fund 變化: {projection.ExpectedClubFundDeltas[1]:N2} (預期最終 Fund: {projection.ExpectedClubFunds[1]:N2})");
        Console.WriteLine($"  -> 理論預期 Club 2 Fund 變化: {projection.ExpectedClubFundDeltas[2]:N2} (預期最終 Fund: {projection.ExpectedClubFunds[2]:N2})");

        // 短暫延遲 500ms 確保所有已確認之非同步 DB 事務均已落盤
        await Task.Delay(500);

        // 階段五：資料庫實際快照比對 (Database Verification)
        Console.WriteLine("\n[階段五] 查詢資料庫實際數值並與理論快照進行嚴格比對...");
        var finalSnapshot = await CaptureDbSnapshotAsync(playersCount);

        var verifyResult = VerifyConsistency(initialSnapshot, finalSnapshot, projection);

        Console.WriteLine("\n==============================================================================");
        Console.WriteLine(" [資料庫一致性驗證結果報告 (雙機負載均衡模式)]");
        Console.WriteLine("==============================================================================");
        Console.WriteLine($"1. Spinlog 總生成筆數: 實際 {verifyResult.ActualSpins:N0} 筆 vs 預期 {verifyResult.ExpectedSpins:N0} 筆 -> {(verifyResult.SpinsMatch ? "[PASS]" : "[FAIL]")}");
        Console.WriteLine($"2. 系統 Props 總錢包: 實際 {verifyResult.ActualTotalWallet:N2} vs 預期 {verifyResult.ExpectedTotalWallet:N2} (差額: {verifyResult.WalletDiff:F4}) -> {(verifyResult.WalletMatch ? "[PASS]" : "[FAIL]")}");
        Console.WriteLine($"3. Club 1 Fund 水池: 實際 {verifyResult.ActualClub1Fund:N2} vs 預期 {verifyResult.ExpectedClub1Fund:N2} (差額: {verifyResult.Club1FundDiff:F4}) -> {(verifyResult.Club1FundMatch ? "[PASS]" : "[FAIL]")}");
        Console.WriteLine($"4. Club 2 Fund 水池: 實際 {verifyResult.ActualClub2Fund:N2} vs 預期 {verifyResult.ExpectedClub2Fund:N2} (差額: {verifyResult.Club2FundDiff:F4}) -> {(verifyResult.Club2FundMatch ? "[PASS]" : "[FAIL]")}");
        Console.WriteLine($"5. 各玩家 Props 錢包獨立一致性: 共 {verifyResult.PropsCheckedCount} 組 (UID, CID) 錢包，異常數量: {verifyResult.PropsMismatchCount}");

        if (verifyResult.PropsMismatchCount > 0)
        {
            Console.WriteLine("\n[詳細異常清單 (前 10 筆)]:");
            foreach (var err in verifyResult.MismatchErrors.Take(10))
            {
                Console.WriteLine($"  * {err}");
            }
        }

        Console.WriteLine("==============================================================================");

        if (verifyResult.AllPassed)
        {
            Console.WriteLine(">>> [測試成功] 所有資料庫數值完全吻合理論精算，雙機負載均衡高併發下無任何 Race Condition 或資料遺失！ <<<");
            return 0;
        }
        else
        {
            Console.WriteLine(">>> [測試失敗] 資料庫實際數值與理論推算存在偏差，檢測出併發競態條件 (Race Condition) 或寫入遺失！ <<<");
            return 1;
        }
    }

    private static async Task RunSessionLoopAsync(GameSession session, HttpClient[] httpClients, int thinkTimeMs, CancellationToken ct)
    {
        int step = 0;
        int patternLen = DefaultCheatPattern.Length;
        int clientCount = httpClients.Length;

        while (!ct.IsCancellationRequested)
        {
            var payload = session.SpinPayloads[step % patternLen];
            // 每個 Session 依步進輪流切換發送目標伺服器 (結合 PlayerIndex 確保初值即均勻分散)
            var client = httpClients[(session.PlayerIndex + step) % clientCount];

            using var req = new HttpRequestMessage(HttpMethod.Post, "slot/spin");
            req.Headers.Authorization = session.AuthHeader;
            req.Content = new ByteArrayContent(payload);
            req.Content.Headers.ContentType = JsonMediaType;

            try
            {
                using var res = await client.SendAsync(req, CancellationToken.None);
                if (res.IsSuccessStatusCode)
                {
                    session.CompletedSteps++;
                    step++;
                    if (thinkTimeMs > 0 && !ct.IsCancellationRequested)
                    {
                        await Task.Delay(thinkTimeMs, ct);
                    }
                }
                else
                {
                    session.FailedSteps++;
                    await Task.Delay(10, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                session.FailedSteps++;
                try { await Task.Delay(10, CancellationToken.None); } catch { break; }
            }
        }
    }

    private static async Task<Dictionary<int, GameStepProfile>> ExecuteDryRunProfilesAsync(
        HttpClient httpClient,
        int[] gameTypes,
        double bet,
        int sel)
    {
        var profiles = new Dictionary<int, GameStepProfile>();

        // 使用預設 admin 帳號進行 Dry Run 測試
        var email = "admin@example.org";
        var secret = "0YBoaT";
        var loginBody = JsonSerializer.Serialize(new { email, secret });
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "signin");
        loginReq.Content = new StringContent(loginBody, Encoding.UTF8, "application/json");

        using var loginRes = await httpClient.SendAsync(loginReq);
        if (!loginRes.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Dry Run 登入失敗: HTTP {(int)loginRes.StatusCode}");
        }

        var loginJson = await loginRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(loginJson);
        var token = GetProp(doc.RootElement, "access").GetString()!;
        var authHeader = new AuthenticationHeaderValue("Bearer", token);

        foreach (var gameId in gameTypes.Distinct())
        {
            Console.WriteLine($"  -> 正在針對遊戲 {GetGameName(gameId)} (ID: {gameId}) 進行單步 Dry Run 動態採樣...");

            // 建立測試遊戲場景 (Club 1, Admin UID 1)
            var newGameBody = JsonSerializer.Serialize(new { cid = 1UL, uid = 1UL, game = gameId });
            using var newGameReq = new HttpRequestMessage(HttpMethod.Post, "game/new");
            newGameReq.Headers.Authorization = authHeader;
            newGameReq.Content = new StringContent(newGameBody, Encoding.UTF8, "application/json");

            using var newGameRes = await httpClient.SendAsync(newGameReq);
            if (!newGameRes.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"遊戲 {gameId} Dry Run 場景建立失敗: HTTP {(int)newGameRes.StatusCode}");
            }

            var gameRespJson = await newGameRes.Content.ReadAsStringAsync();
            using var gameDoc = JsonDocument.Parse(gameRespJson);
            ulong gid = GetProp(gameDoc.RootElement, "gid").GetUInt64();
            double initialWallet = GetProp(gameDoc.RootElement, "wallet").GetDouble();

            var effectiveSel = gameId == 11 ? 0 : sel;
            int nSteps = DefaultCheatPattern.Length;
            var walletDeltas = new double[nSteps];
            var fundDeltas = new double[nSteps];
            var bankDeltas = new double[nSteps];

            double currentWallet = initialWallet;

            // 查詢當前 Club 1 狀態
            var (prevBank, prevFund) = await QueryClubStatusAsync(1UL);

            // 第一輪循環：採樣 Delta
            for (int s = 0; s < nSteps; s++)
            {
                var spinBody = JsonSerializer.Serialize(new
                {
                    gid,
                    bet,
                    sel = effectiveSel,
                    cheat = DefaultCheatPattern[s]
                });

                using var spinReq = new HttpRequestMessage(HttpMethod.Post, "slot/spin");
                spinReq.Headers.Authorization = authHeader;
                spinReq.Content = new StringContent(spinBody, Encoding.UTF8, "application/json");

                using var spinRes = await httpClient.SendAsync(spinReq);
                if (!spinRes.IsSuccessStatusCode)
                {
                    var err = await spinRes.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"遊戲 {gameId} 步進 {s} Spin 失敗: HTTP {(int)spinRes.StatusCode} - {err}");
                }

                var spinJson = await spinRes.Content.ReadAsStringAsync();
                using var spinDoc = JsonDocument.Parse(spinJson);
                var newWallet = GetProp(spinDoc.RootElement, "wallet").GetDouble();

                var (curBank, curFund) = await QueryClubStatusAsync(1UL);

                walletDeltas[s] = Math.Round(newWallet - currentWallet, 2);
                fundDeltas[s] = Math.Round(curFund - prevFund, 2);
                bankDeltas[s] = Math.Round(curBank - prevBank, 2);

                currentWallet = newWallet;
                prevFund = curFund;
                prevBank = curBank;
            }

            // 第二輪循環：驗證確定性 (確定性驗證，保證相同 Cheat 產生的 Delta 必然相同)
            for (int s = 0; s < nSteps; s++)
            {
                var spinBody = JsonSerializer.Serialize(new
                {
                    gid,
                    bet,
                    sel = effectiveSel,
                    cheat = DefaultCheatPattern[s]
                });

                using var spinReq = new HttpRequestMessage(HttpMethod.Post, "slot/spin");
                spinReq.Headers.Authorization = authHeader;
                spinReq.Content = new StringContent(spinBody, Encoding.UTF8, "application/json");

                using var spinRes = await httpClient.SendAsync(spinReq);
                var spinJson = await spinRes.Content.ReadAsStringAsync();
                using var spinDoc = JsonDocument.Parse(spinJson);
                var newWallet = GetProp(spinDoc.RootElement, "wallet").GetDouble();
                var (curBank, curFund) = await QueryClubStatusAsync(1UL);

                var round2WalletDelta = Math.Round(newWallet - currentWallet, 2);
                var round2FundDelta = Math.Round(curFund - prevFund, 2);
                var round2BankDelta = Math.Round(curBank - prevBank, 2);

                if (Math.Abs(round2WalletDelta - walletDeltas[s]) > 0.01 ||
                    Math.Abs(round2FundDelta - fundDeltas[s]) > 0.01)
                {
                    Console.WriteLine($"    [注意] 遊戲 {gameId} 步進 {s} 在第二輪數值存在微幅波動 (W1: {walletDeltas[s]}, W2: {round2WalletDelta})");
                }

                currentWallet = newWallet;
                prevFund = curFund;
                prevBank = curBank;
            }

            var profile = new GameStepProfile
            {
                GameId = gameId,
                WalletDeltas = walletDeltas,
                FundDeltas = fundDeltas,
                BankDeltas = bankDeltas
            };
            profiles[gameId] = profile;

            Console.WriteLine($"    [完成] 遊戲 {gameId} 單輪 Wallet Delta: {profile.TotalWalletDelta:N2}, Fund Delta: {profile.TotalFundDelta:N2}, Bank Delta: {profile.TotalBankDelta:N2}");
        }

        return profiles;
    }

    private static async Task<GameSession[]> SetupPlayerSessionsAsync(
        HttpClient[] httpClients,
        int playerCount,
        int tabsPerPlayer,
        int authConcurrency,
        double bet,
        int sel,
        int[] gameTypes)
    {
        Console.WriteLine($"\n[登入] 正在併發登入 {playerCount} 位測試玩家 (併發度: {authConcurrency})...");
        var playerTokens = new string[playerCount + 1];
        int signedInCount = 0;
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = authConcurrency };
        int clientCount = httpClients.Length;

        await Parallel.ForEachAsync(Enumerable.Range(1, playerCount), parallelOptions, async (i, ct) =>
        {
            var email = $"loadtest_{i}@example.org";
            var secret = "iVI05M";
            var body = JsonSerializer.Serialize(new { email, secret });
            var client = httpClients[i % clientCount];

            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "signin");
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    using var res = await client.SendAsync(req, ct);
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
                        await Task.Delay(150 * (retry + 1), ct);
                    }
                }
                catch
                {
                    await Task.Delay(150 * (retry + 1), ct);
                }
            }
        });

        int totalSessions = playerCount * tabsPerPlayer;
        Console.WriteLine($"\n[建局] 正在為每位玩家建立 {tabsPerPlayer} 個獨立遊戲分頁 (共 {totalSessions} 個 Session)...");

        var sessions = new GameSession[totalSessions];
        int createdSessionsCount = 0;

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
            var client = httpClients[item.flatIndex % clientCount];

            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "game/new");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    req.Content = new StringContent(gameBody, Encoding.UTF8, "application/json");

                    using var res = await client.SendAsync(req, ct);
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        ulong gid = GetProp(doc.RootElement, "gid").GetUInt64();

                        var effectiveSel = game == 11 ? 0 : sel;

                        // 預先產生該 Session 循環所需的所有 Byte Payload，徹底消除壓測中的序列化開銷
                        int nSteps = DefaultCheatPattern.Length;
                        var payloads = new byte[nSteps][];
                        for (int s = 0; s < nSteps; s++)
                        {
                            var spinJson = JsonSerializer.Serialize(new
                            {
                                gid,
                                bet,
                                sel = effectiveSel,
                                cheat = DefaultCheatPattern[s]
                            });
                            payloads[s] = Encoding.UTF8.GetBytes(spinJson);
                        }

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
                            SpinPayloads = payloads
                        };

                        var current = Interlocked.Increment(ref createdSessionsCount);
                        if (current % 200 == 0 || current == totalSessions)
                        {
                            Console.WriteLine($"  -> 場景建立進度: {current} / {totalSessions} ({(current * 100.0 / totalSessions):F0}%)");
                        }
                        return;
                    }
                    else
                    {
                        await Task.Delay(150 * (retry + 1), ct);
                    }
                }
                catch
                {
                    await Task.Delay(150 * (retry + 1), ct);
                }
            }
        });

        Console.WriteLine($"[成功] 全部 {totalSessions} 組遊戲場景已建立完成！");
        return sessions;
    }

    public record DbSnapshot(
        Dictionary<(ulong Uid, ulong Cid), double> PropsWallets,
        double TotalWallet,
        Dictionary<ulong, double> ClubFunds,
        Dictionary<ulong, double> ClubBanks,
        ulong MaxSpinlogId);

    private static async Task<DbSnapshot> CaptureDbSnapshotAsync(int playerCount)
    {
        var appConnStr = $"Server={_dbIp};Port={_dbPort};Database={_dbName};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var conn = new MySqlConnection(appConnStr);
        await conn.OpenAsync();

        ulong minUid = 1001;
        ulong maxUid = 1000 + (ulong)playerCount;

        // 1. Props Wallets
        var propsWallets = new Dictionary<(ulong Uid, ulong Cid), double>();
        var propsSql = $"SELECT `UID`, `CID`, `Wallet` FROM `Props` WHERE `UID` >= {minUid} AND `UID` <= {maxUid};";
        await using (var cmd = new MySqlCommand(propsSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                ulong uid = reader.GetUInt64("UID");
                ulong cid = reader.GetUInt64("CID");
                double wallet = reader.GetDouble("Wallet");
                propsWallets[(uid, cid)] = Math.Round(wallet, 2);
            }
        }

        double totalWallet = Math.Round(propsWallets.Values.Sum(), 2);

        // 2. Club Funds & Banks
        var clubFunds = new Dictionary<ulong, double>();
        var clubBanks = new Dictionary<ulong, double>();
        var clubSql = "SELECT `ID`, `Fund`, `Bank` FROM `ClubData` WHERE `ID` IN (1, 2);";
        await using (var cmd = new MySqlCommand(clubSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                ulong id = reader.GetUInt64("ID");
                double fund = reader.GetDouble("Fund");
                double bank = reader.GetDouble("Bank");
                clubFunds[id] = Math.Round(fund, 2);
                clubBanks[id] = Math.Round(bank, 2);
            }
        }

        // 3. Max Spinlog ID
        ulong maxSpinlogId = 0;
        var spinlogSql = "SELECT COALESCE(MAX(`ID`), 0) FROM `Spinlog`;";
        await using (var cmd = new MySqlCommand(spinlogSql, conn))
        {
            var scalar = await cmd.ExecuteScalarAsync();
            if (scalar != null && scalar != DBNull.Value)
            {
                maxSpinlogId = Convert.ToUInt64(scalar);
            }
        }

        return new DbSnapshot(propsWallets, totalWallet, clubFunds, clubBanks, maxSpinlogId);
    }

    public record TheoreticalProjection(
        int ExpectedTotalSpins,
        Dictionary<(ulong Uid, ulong Cid), double> ExpectedPropsWallets,
        double ExpectedSystemWalletDelta,
        double ExpectedTotalWallet,
        Dictionary<ulong, double> ExpectedClubFundDeltas,
        Dictionary<ulong, double> ExpectedClubFunds,
        Dictionary<ulong, double> ExpectedClubBankDeltas,
        Dictionary<ulong, double> ExpectedClubBanks);

    private static TheoreticalProjection CalculateTheoreticalProjection(
        GameSession[] sessions,
        Dictionary<int, GameStepProfile> profiles,
        DbSnapshot initialSnapshot)
    {
        int patternLen = DefaultCheatPattern.Length;
        int expectedTotalSpins = sessions.Sum(s => s.CompletedSteps);

        var walletDeltas = new Dictionary<(ulong Uid, ulong Cid), double>();
        var clubFundDeltas = new Dictionary<ulong, double> { [1UL] = 0.0, [2UL] = 0.0 };
        var clubBankDeltas = new Dictionary<ulong, double> { [1UL] = 0.0, [2UL] = 0.0 };

        foreach (var session in sessions)
        {
            var profile = profiles[session.GameId];
            int steps = session.CompletedSteps;
            int fullCycles = steps / patternLen;
            int rem = steps % patternLen;

            double sessionWalletDelta = fullCycles * profile.TotalWalletDelta;
            double sessionFundDelta = fullCycles * profile.TotalFundDelta;
            double sessionBankDelta = fullCycles * profile.TotalBankDelta;

            for (int i = 0; i < rem; i++)
            {
                sessionWalletDelta += profile.WalletDeltas[i];
                sessionFundDelta += profile.FundDeltas[i];
                sessionBankDelta += profile.BankDeltas[i];
            }

            var key = (session.Uid, session.Cid);
            walletDeltas[key] = Math.Round(walletDeltas.GetValueOrDefault(key, 0.0) + sessionWalletDelta, 2);
            clubFundDeltas[session.Cid] = Math.Round(clubFundDeltas[session.Cid] + sessionFundDelta, 2);
            clubBankDeltas[session.Cid] = Math.Round(clubBankDeltas[session.Cid] + sessionBankDelta, 2);
        }

        var expectedPropsWallets = new Dictionary<(ulong Uid, ulong Cid), double>();
        foreach (var (key, initialWallet) in initialSnapshot.PropsWallets)
        {
            var delta = walletDeltas.GetValueOrDefault(key, 0.0);
            expectedPropsWallets[key] = Math.Round(initialWallet + delta, 2);
        }

        double expectedSystemWalletDelta = Math.Round(walletDeltas.Values.Sum(), 2);
        double expectedTotalWallet = Math.Round(initialSnapshot.TotalWallet + expectedSystemWalletDelta, 2);

        var expectedClubFunds = new Dictionary<ulong, double>
        {
            [1UL] = Math.Round(initialSnapshot.ClubFunds[1] + clubFundDeltas[1UL], 2),
            [2UL] = Math.Round(initialSnapshot.ClubFunds[2] + clubFundDeltas[2UL], 2)
        };

        var expectedClubBanks = new Dictionary<ulong, double>
        {
            [1UL] = Math.Round(initialSnapshot.ClubBanks[1] + clubBankDeltas[1UL], 2),
            [2UL] = Math.Round(initialSnapshot.ClubBanks[2] + clubBankDeltas[2UL], 2)
        };

        return new TheoreticalProjection(
            expectedTotalSpins,
            expectedPropsWallets,
            expectedSystemWalletDelta,
            expectedTotalWallet,
            clubFundDeltas,
            expectedClubFunds,
            clubBankDeltas,
            expectedClubBanks);
    }

    public record ConsistencyReport(
        int ExpectedSpins,
        int ActualSpins,
        bool SpinsMatch,
        double ExpectedTotalWallet,
        double ActualTotalWallet,
        double WalletDiff,
        bool WalletMatch,
        double ExpectedClub1Fund,
        double ActualClub1Fund,
        double Club1FundDiff,
        bool Club1FundMatch,
        double ExpectedClub2Fund,
        double ActualClub2Fund,
        double Club2FundDiff,
        bool Club2FundMatch,
        int PropsCheckedCount,
        int PropsMismatchCount,
        List<string> MismatchErrors,
        bool AllPassed);

    private static ConsistencyReport VerifyConsistency(
        DbSnapshot initialSnapshot,
        DbSnapshot finalSnapshot,
        TheoreticalProjection projection)
    {
        int actualSpins = (int)(finalSnapshot.MaxSpinlogId - initialSnapshot.MaxSpinlogId);
        bool spinsMatch = actualSpins == projection.ExpectedTotalSpins;

        double walletDiff = Math.Round(finalSnapshot.TotalWallet - projection.ExpectedTotalWallet, 4);
        bool walletMatch = Math.Abs(walletDiff) <= 0.05;

        double c1Diff = Math.Round(finalSnapshot.ClubFunds[1] - projection.ExpectedClubFunds[1], 4);
        bool c1Match = Math.Abs(c1Diff) <= 0.05;

        double c2Diff = Math.Round(finalSnapshot.ClubFunds[2] - projection.ExpectedClubFunds[2], 4);
        bool c2Match = Math.Abs(c2Diff) <= 0.05;

        int propsCheckedCount = 0;
        int propsMismatchCount = 0;
        var mismatchErrors = new List<string>();

        foreach (var (key, expectedWallet) in projection.ExpectedPropsWallets)
        {
            propsCheckedCount++;
            if (!finalSnapshot.PropsWallets.TryGetValue(key, out var actualWallet))
            {
                propsMismatchCount++;
                mismatchErrors.Add($"[找不到紀錄] 玩家 UID {key.Uid} (CID {key.Cid}) 在資料庫中遺失！");
                continue;
            }

            double diff = Math.Round(actualWallet - expectedWallet, 2);
            if (Math.Abs(diff) > 0.05)
            {
                propsMismatchCount++;
                mismatchErrors.Add($"玩家 UID {key.Uid} (CID {key.Cid}) -> 預期: {expectedWallet:F2}, 實際: {actualWallet:F2}, 偏差: {diff:F2}");
            }
        }

        bool allPassed = spinsMatch && walletMatch && c1Match && c2Match && (propsMismatchCount == 0);

        return new ConsistencyReport(
            projection.ExpectedTotalSpins,
            actualSpins,
            spinsMatch,
            projection.ExpectedTotalWallet,
            finalSnapshot.TotalWallet,
            walletDiff,
            walletMatch,
            projection.ExpectedClubFunds[1],
            finalSnapshot.ClubFunds[1],
            c1Diff,
            c1Match,
            projection.ExpectedClubFunds[2],
            finalSnapshot.ClubFunds[2],
            c2Diff,
            c2Match,
            propsCheckedCount,
            propsMismatchCount,
            mismatchErrors,
            allPassed);
    }

    private static async Task<(double Bank, double Fund)> QueryClubStatusAsync(ulong cid)
    {
        var appConnStr = $"Server={_dbIp};Port={_dbPort};Database={_dbName};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var conn = new MySqlConnection(appConnStr);
        await conn.OpenAsync();

        var sql = $"SELECT `Bank`, `Fund` FROM `ClubData` WHERE `ID` = {cid};";
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetDouble("Bank"), reader.GetDouble("Fund"));
        }
        return (0.0, 0.0);
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

        var clubInsertSql = @"
INSERT INTO `ClubData` (`ID`, `Name`, `Bank`, `Fund`, `Lock`, `Rate`, `MRTP`)
VALUES (1, 'virtual', 10000000000, 10000000000, 0, 2.5, 95),
       (2, 'club2', 10000000000, 10000000000, 0, 2.5, 95)
ON DUPLICATE KEY UPDATE `Bank` = 10000000000, `Fund` = 10000000000;";
        await using (var cmd = new MySqlCommand(clubInsertSql, appConn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

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

        await SeedUsersAndPropsAsync(appConn, playerCount);
    }

    private static async Task SeedUsersOnlyAsync(int playerCount)
    {
        var appConnStr = $"Server={_dbIp};Port={_dbPort};Database={_dbName};Uid={_dbUser};Pwd={_dbPass};Charset=utf8mb4;Pooling=false;";
        await using var appConn = new MySqlConnection(appConnStr);
        await appConn.OpenAsync();

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

            var candidateInSrcReleaseBuild = Path.Combine(currentDir.FullName, "src", "bin", "Release", "net10.0", "server.exe");
            if (File.Exists(candidateInSrcReleaseBuild))
                return candidateInSrcReleaseBuild;

            var candidateInSrcDebugBuild = Path.Combine(currentDir.FullName, "src", "bin", "Debug", "net10.0", "server.exe");
            if (File.Exists(candidateInSrcDebugBuild))
                return candidateInSrcDebugBuild;

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
用法: dotnet run [參數] 或 ServerMain.LoadTestsFixedLB.exe [參數]

參數選項:
  --players <數量>           模擬玩家數量 (預設: 50)
  --tabs <數量>              每位玩家開啟的分頁數 (預設: 4)
  --games <清單>             遊戲類型清單 (逗號分隔，預設: 2,12,13,14)
  --duration <秒>            壓測執行秒數 (預設: 10)
  --think-time <毫秒>        單次 Spin 間隔延遲 (預設: 0，0 為全速極限壓測)
  --bet <金額>               單次 Spin 下注金額 (預設: 10.0)
  --sel <線數>               單次 Spin 選擇線數 (預設: 2)
  --auth-concurrency <數量>  前置登入與建立場景的併發度 (預設: 50)
  --urls <網址清單>          目標伺服器位址 (逗號分隔，預設: http://localhost:18080,http://localhost:18081)
  --dbip <IP>                MySQL 資料庫主機 (預設: 127.0.0.1)
  --dbport <Port>            MySQL 資料庫通訊埠 (預設: 3306)
  --dbuser <帳號>            MySQL 帳號 (預設: root)
  --dbpass <密碼>            MySQL 密碼 (預設: rootpassword)
  --dbname <庫名>            MySQL 測試資料庫名稱 (預設: 動態產生 slot_fixed_xxxx)
  --skip-db-init             跳過資料庫與帳號初始化 (適用於已存在的資料庫)
  --init-db-only             僅執行資料庫同步與玩家資料植入後退出
  --seed-users-only          僅對現有資料庫寫入玩家與錢包點數後退出
  --drop-db                  刪除測試資料庫後退出
  --help, -h                 顯示此說明畫面
");
    }
}
