using System.Globalization;
using FluentAssertions;
using MySqlConnector;
using TechTalk.SpecFlow;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ServerMain.Tests.Steps
{
    public partial class Jokers100Steps
    {
        public static long _Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private string GetCurrentDatabaseName() =>
            _featureContext != null && _featureContext.TryGetValue("DbName", out string? dbName) && !string.IsNullOrWhiteSpace(dbName)
                ? dbName
                : "slot_club";

        private string GetMySqlConnectionString() =>
            $"Server={DbIp};Port={DbPort};Database={GetCurrentDatabaseName()};Uid={DbUser};Pwd={DbPass};Charset=utf8mb4;Pooling=false;";

        private string GetGoMySqlDsn() =>
            $"{DbUser}:{DbPass}@tcp({DbIp}:{DbPort})/{GetCurrentDatabaseName()}?charset=utf8mb4";

        private static string GetMySqlAdminConnectionString() =>
            $"Server={DbIp};Port={DbPort};Uid={DbUser};Pwd={DbPass};Charset=utf8mb4;Pooling=false;";

        private static int _portCounter = 18000;
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        private static int GetAvailablePort()
        {
            return Interlocked.Increment(ref _portCounter);
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
            throw new FileNotFoundException("無法找到 server.exe，請確認在執行測試前已成功編譯 server.exe。");
        }

        private async Task EnsureIsolatedDatabaseCreatedAsync()
        {
            if (_featureContext.ContainsKey("DbName"))
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_featureContext.ContainsKey("DbName"))
                {
                    return;
                }

                var dbName = $"test_{_Time}_{_featureContext.FeatureInfo.Title.ToString()}";

                _featureContext["DbName"] = dbName;
                var port = GetAvailablePort();
                _featureContext["ServerPort"] = port;

                var adminConnStr = GetMySqlAdminConnectionString();
                await using (var conn = new MySqlConnection(adminConnStr))
                {
                    await conn.OpenAsync();
                    await using var cmd = new MySqlCommand($"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", conn);
                    await cmd.ExecuteNonQueryAsync();
                }

                var serverExe = FindServerExePath();
                var syncPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = serverExe,
                    Arguments = $"--sync-only --dbdns={GetGoMySqlDsn()}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(serverExe)!
                };
                using var syncProc = System.Diagnostics.Process.Start(syncPsi);
                if (syncProc != null)
                {
                    var stdout = await syncProc.StandardOutput.ReadToEndAsync();
                    var stderr = await syncProc.StandardError.ReadToEndAsync();
                    await syncProc.WaitForExitAsync();
                    if (syncProc.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"server.exe --sync-only 執行失敗，ExitCode: {syncProc.ExitCode}, StdOut: {stdout}, StdErr: {stderr}");
                    }
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static string ResolveActualTableName(string tableName)
        {
            var lower = tableName.ToLowerInvariant().Replace("_", "");
            return lower switch
            {
                "clubdata" or "clubs" => "ClubData",
                "user" or "users" => "User",
                "props" or "prop" => "Props",
                "story" or "stories" => "Story",
                "spinlog" => "Spinlog",
                "multlog" => "Multlog",
                "banklog" => "Banklog",
                "walletlog" => "Walletlog",
                _ => tableName
            };
        }

        private async Task InsertTableRecordsAsync(string tableName, Table table)
        {
            tableName = ResolveActualTableName(tableName);
            var connStr = GetMySqlConnectionString();
            await using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            var headers = table.Header.ToList();
            var cols = string.Join(", ", headers.Select(h => $"`{h}`"));
            var paramNames = string.Join(", ", headers.Select((h, i) => $"@p{i}"));
            var sql = $"INSERT INTO `{tableName}` ({cols}) VALUES ({paramNames});";

            foreach (var row in table.Rows)
            {
                await using var cmd = new MySqlCommand(sql, conn);
                for (int i = 0; i < headers.Count; i++)
                {
                    var valStr = row[headers[i]].Trim();
                    if (string.Equals(valStr, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", DBNull.Value);
                    }
                    else if (long.TryParse(valStr, out var longVal))
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", longVal);
                    }
                    else if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleVal))
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", doubleVal);
                    }
                    else if (bool.TryParse(valStr, out var boolVal))
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", boolVal);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", valStr);
                    }
                }
                await cmd.ExecuteNonQueryAsync();
            }
        }

        [Given(@"建立獨立資料庫")]
        public async Task GivenCreateIsolatedDatabase()
        {
            await EnsureIsolatedDatabaseCreatedAsync();
        }

        [Given(@"建立獨立資料庫並寫入初始資料表 ""(.*)"":")]
        public async Task GivenCreateIsolatedDatabaseAndSeedTable(string tableName, Table table)
        {
            await EnsureIsolatedDatabaseCreatedAsync();

            var seedKey = "Seeded_" + ResolveActualTableName(tableName);
            if (_featureContext.ContainsKey(seedKey))
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_featureContext.ContainsKey(seedKey))
                {
                    return;
                }
                await InsertTableRecordsAsync(tableName, table);
                _featureContext[seedKey] = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        [Given(@"寫入初始資料表 ""(.*)"":")]
        public async Task GivenSeedTable(string tableName, Table table)
        {
            var seedKey = "Seeded_" + ResolveActualTableName(tableName);
            if (_featureContext.ContainsKey(seedKey))
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_featureContext.ContainsKey(seedKey))
                {
                    return;
                }
                await InsertTableRecordsAsync(tableName, table);
                _featureContext[seedKey] = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        [Given(@"啟動 Golang 伺服器")]
        public async Task GivenStartGolangServer()
        {
            if (_featureContext.ContainsKey("ServerStarted"))
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_featureContext.ContainsKey("ServerStarted"))
                {
                    return;
                }

                var dbName = (string)_featureContext["DbName"];
                var port = (int)_featureContext["ServerPort"];
                var serverExe = FindServerExePath();

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = serverExe,
                    Arguments = $"--port=:{port} --dbdns={GetGoMySqlDsn()}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(serverExe)!
                };

                var errBuilder = new System.Text.StringBuilder();
                var process = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("啟動 server.exe 失敗");
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) { errBuilder.AppendLine(e.Data); Console.WriteLine("[SERVER] " + e.Data); } };
                process.BeginErrorReadLine();

                _featureContext["ServerProcess"] = process;

                var client = new HttpClient
                {
                    BaseAddress = new Uri($"http://localhost:{port}")
                };
                _featureContext["HttpClient"] = client;

                var ready = false;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (!cts.IsCancellationRequested && !process.HasExited)
                {
                    try
                    {
                        var res = await client.GetAsync("/", cts.Token);
                        ready = true;
                        break;
                    }
                    catch
                    {
                        await Task.Delay(200, cts.Token);
                    }
                }

                if (process.HasExited)
                {
                    throw new InvalidOperationException($"server.exe 已退出，ExitCode: {process.ExitCode}, StdErr: {errBuilder}");
                }
                if (!ready)
                {
                    throw new TimeoutException($"等待 server.exe 監聽埠號 {port} 逾時，StdErr: {errBuilder}");
                }

                _featureContext["ServerStarted"] = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        [AfterFeature]
        public static async Task AfterFeatureCleanup(FeatureContext featureContext)
        {
            if (featureContext.TryGetValue("ServerProcess", out System.Diagnostics.Process? process) && process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync();
                    }
                }
                catch { }
            }

            if (featureContext.TryGetValue("DbName", out string? dbName) && !string.IsNullOrWhiteSpace(dbName))
            {
                try
                {
                    var adminConnStr = GetMySqlAdminConnectionString();
                    await using var conn = new MySqlConnection(adminConnStr);
                    await conn.OpenAsync();
                    await using var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{dbName}`;", conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { }
            }
        }

        [Given(@"Sleep (.*) 秒")]
        [When(@"Sleep (.*) 秒")]
        [Then(@"Sleep (.*) 秒")]
        public async Task SleepSeconds(int seconds)
        {
            await Task.Delay(seconds * 1000);
        }

        [Given(@"驗證MySQL資料庫 ""(.*)"" 資料表的所有紀錄數值應該為:")]
        [When(@"驗證MySQL資料庫 ""(.*)"" 資料表的所有紀錄數值應該為:")]
        [Then(@"驗證MySQL資料庫 ""(.*)"" 資料表的所有紀錄數值應該為:")]
        public async Task VerifyMySqlTableRecords(string tableName, Table table)
        {
            var connStr = GetMySqlConnectionString();
            await using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            var headers = table.Header.ToList();
            headers.Should().NotBeEmpty("Gherkin 表格必須包含至少一個欄位名稱作為頂端列");

            if (table.Rows.Count == 0)
            {
                var countSql = $"SELECT COUNT(*) FROM `{tableName}`";
                await using var countCmd = new MySqlCommand(countSql, conn);
                var totalCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
                totalCount.Should().Be(0, $"資料表 `{tableName}` 預期為空，但實際有 {totalCount} 筆記錄");

                var selectCols = string.Join(", ", headers.Select(h => $"`{h}`"));
                var checkColsSql = $"SELECT {selectCols} FROM `{tableName}` LIMIT 0";
                await using var checkCmd = new MySqlCommand(checkColsSql, conn);
                await checkCmd.ExecuteNonQueryAsync();
                return;
            }

            var keyColumn = headers.First();

            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                var keyValue = row[keyColumn];
                var selectCols = string.Join(", ", headers.Select(h => $"`{h}`"));
                var sql = $"SELECT {selectCols} FROM `{tableName}` WHERE `{keyColumn}` = @keyValue LIMIT 1";

                await using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@keyValue", keyValue);

                await using var reader = await cmd.ExecuteReaderAsync();
                var exists = await reader.ReadAsync();

                Dictionary<string, object> rowValues;
                if (exists)
                {
                    rowValues = headers.ToDictionary(h => h, h => reader[h]);
                }
                else
                {
                    await reader.CloseAsync();
                    // 若以 keyColumn 查詢不到且 keyValue 為整數索引 (例如 SID)，嘗試依 keyColumn 排序取得對應 offset 記錄
                    var fallbackSql = $"SELECT {selectCols} FROM `{tableName}` ORDER BY `{keyColumn}` ASC LIMIT 1 OFFSET {rowIndex}";
                    await using var fallbackCmd = new MySqlCommand(fallbackSql, conn);
                    await using var fallbackReader = await fallbackCmd.ExecuteReaderAsync();
                    var fallbackExists = await fallbackReader.ReadAsync();
                    fallbackExists.Should().BeTrue($"在資料表 `{tableName}` 中找不到 `{keyColumn}` = '{keyValue}' 的記錄 (且無對應索引記錄)");
                    rowValues = headers.ToDictionary(h => h, h => fallbackReader[h]);
                }

                foreach (var col in headers)
                {
                    var expectedStr = row[col].Trim();
                    if (expectedStr == "*")
                    {
                        continue;
                    }

                    var actualObj = rowValues[col];

                    if (actualObj == DBNull.Value)
                    {
                        expectedStr.ToLowerInvariant().Should().Be("null",
                            $"資料表 `{tableName}` 欄位 `{col}` 預期為 '{expectedStr}'，但資料庫為 NULL");
                        continue;
                    }

                    // 數值比對處理 (浮點數 / 整數)
                    if (double.TryParse(expectedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedDouble) &&
                        double.TryParse(actualObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var actualDouble))
                    {
                        actualDouble.Should().BeApproximately(expectedDouble, 0.01,
                            $"資料表 `{tableName}` 記錄 (`{keyColumn}`='{keyValue}') 的欄位 `{col}` 數值不符合");
                    }
                    else if (bool.TryParse(expectedStr, out var expectedBool) &&
                             bool.TryParse(actualObj?.ToString(), out var actualBool))
                    {
                        actualBool.Should().Be(expectedBool,
                            $"資料表 `{tableName}` 記錄 (`{keyColumn}`='{keyValue}') 的欄位 `{col}` 布林值不符合");
                    }
                    else
                    {
                        var actualStr = actualObj?.ToString()?.Trim();
                        actualStr.Should().Be(expectedStr,
                            $"資料表 `{tableName}` 記錄 (`{keyColumn}`='{keyValue}') 的欄位 `{col}` 字串不符合");
                    }
                }
            }
        }

        [Given(@"驗證MySQL資料庫 ""(.*)"" 資料表應該沒有任何紀錄")]
        [When(@"驗證MySQL資料庫 ""(.*)"" 資料表應該沒有任何紀錄")]
        [Then(@"驗證MySQL資料庫 ""(.*)"" 資料表應該沒有任何紀錄")]
        public async Task VerifyMySqlTableEmpty(string tableName)
        {
            tableName = ResolveActualTableName(tableName);
            var connStr = GetMySqlConnectionString();
            await using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            var sql = $"SELECT COUNT(*) FROM `{tableName}`";
            await using var cmd = new MySqlCommand(sql, conn);
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            count.Should().Be(0, $"資料表 `{tableName}` 應該沒有任何紀錄，但目前有 {count} 筆");
        }

        [Given(@"除錯傾印MySQL資料庫所有表格數值")]
        [When(@"除錯傾印MySQL資料庫所有表格數值")]
        [Then(@"除錯傾印MySQL資料庫所有表格數值")]
        public async Task DumpAllTables()
        {
            var tables = new[] { "ClubData", "User", "Props", "Story", "Spinlog", "Multlog", "Walletlog", "Banklog" };
            var connStr = GetMySqlConnectionString();
            await using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            foreach (var tbl in tables)
            {
                var cmd = new MySqlCommand($"SELECT * FROM `{tbl}`", conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                Console.WriteLine($"\n=== TABLE: {tbl} ===");
                var cols = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));
                Console.WriteLine($"| {string.Join(" | ", cols)} |");
                while (await reader.ReadAsync())
                {
                    var vals = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader[i];
                        vals.Add(val == DBNull.Value ? "NULL" : val?.ToString() ?? "");
                    }
                    Console.WriteLine($"| {string.Join(" | ", vals)} |");
                }
                await reader.CloseAsync();
            }
        }
    }

    public class CustomTestCaseOrderer : ITestCaseOrderer
    {
        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
        {
            return testCases.OrderBy(tc =>
            {
                if (tc.Traits != null && tc.Traits.TryGetValue("Category", out var categories) && categories != null)
                {
                    foreach (var cat in categories)
                    {
                        var lower = cat.ToLowerInvariant();
                        if (lower.StartsWith("order_") || lower.StartsWith("order:"))
                        {
                            var numStr = cat.Substring(6);
                            if (int.TryParse(numStr, out int order))
                            {
                                return order;
                            }
                        }
                    }
                }
                return int.MaxValue;
            }).ThenBy(tc => tc.DisplayName, StringComparer.Ordinal);
        }
    }
}
