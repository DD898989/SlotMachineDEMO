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
                if (arg.Equals("--sync-only"))
                    syncOnly = true;
                if (arg.StartsWith("--port="))
                    portStr = arg.Substring("--port=".Length);
                if (arg.StartsWith("--dbdns="))
                    dbdns = arg.Substring("--dbdns=".Length);
            }

            DatabaseManager.ConnectionString = DatabaseManager.ParseDsn(dbdns);
            if (syncOnly)
            {
                DatabaseManager.SyncTables();
                return 0;
            }

            DatabaseManager.SyncTables();

            var port = int.Parse(portStr.TrimStart(':'));
            var builder = WebApplication.CreateBuilder();
            
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
            builder.Logging.AddFilter("Microsoft", LogLevel.Critical);

            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");


            var app = builder.Build();

            app.UseMiddleware<ErrorLoggingMiddleware>();

            app.MapApiRoutes();

            await app.RunAsync();

            return 0;
        }
    }
}
