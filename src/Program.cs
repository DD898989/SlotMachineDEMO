using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using ServerMain.Api;
using ServerMain.Config;
using ServerMain.Data;
using ServerMain.Middleware;

namespace ServerMain
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var syncOnly = false;
            var portStr = AppConfig.PortHTTP;
            var dbdns = string.Empty;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals("--sync-only", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-sync-only", StringComparison.OrdinalIgnoreCase))
                {
                    syncOnly = true;
                }
                else if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
                {
                    portStr = arg.Substring("--port=".Length);
                }
                else if (arg.Equals("-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    portStr = args[++i];
                }
                else if (arg.StartsWith("--dbdns=", StringComparison.OrdinalIgnoreCase))
                {
                    dbdns = arg.Substring("--dbdns=".Length);
                }
                else if (arg.Equals("-dbdns", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dbdns = args[++i];
                }
            }

            if (!string.IsNullOrEmpty(dbdns))
            {
                DatabaseManager.ConnectionString = DatabaseManager.ParseDsn(dbdns);
            }

            if (syncOnly)
            {
                Console.WriteLine("Executing SyncTables only...");
                try
                {
                    DatabaseManager.SyncTables();
                    Console.WriteLine("SyncTables completed successfully.");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"SyncTables failed: {ex.Message}");
                    return 1;
                }
            }

            // Sync tables and memory
            try
            {
                if (!string.IsNullOrEmpty(DatabaseManager.ConnectionString))
                {
                    DatabaseManager.SyncTables();
                    DatabaseManager.SyncMemory();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Database initialization failed: {ex.Message}");
            }

            var port = 8080;
            if (!string.IsNullOrWhiteSpace(portStr))
            {
                var p = portStr.TrimStart(':');
                if (int.TryParse(p, out var parsedPort))
                {
                    port = parsedPort;
                }
            }

            var builder = WebApplication.CreateBuilder();

            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
            builder.Logging.AddFilter("Microsoft", LogLevel.Critical);

            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            });

            var app = builder.Build();

            app.UseMiddleware<ErrorLoggingMiddleware>();

            app.MapApiRoutes();

            // Background queue flusher
            var cts = new CancellationTokenSource();
            var flushTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(AppConfig.SqlFlushTick, cts.Token);
                        if (!string.IsNullOrEmpty(DatabaseManager.ConnectionString))
                        {
                            DatabaseManager.FlushQueues();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Flush Error]: {ex.Message}");
                    }
                }
            });

            Console.WriteLine($"Server listening on port {port}...");
            await app.RunAsync();

            cts.Cancel();
            await flushTask;

            // Final flush
            if (!string.IsNullOrEmpty(DatabaseManager.ConnectionString))
            {
                DatabaseManager.FlushQueues();
            }

            return 0;
        }
    }
}
