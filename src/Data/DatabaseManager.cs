using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ServerMain.Data
{
    public class SpinAuditData
    {
        public ulong SID { get; set; }
        public ulong GID { get; set; }
        public ulong CID { get; set; }
        public int Attempts { get; set; }
        public double TargetRTP { get; set; }
        public double EffRTP { get; set; }
        public double FundBefore { get; set; }
        public double FundAfter { get; set; }
        public double JackWin { get; set; }
        public double JackRate { get; set; }
        public double JackRent { get; set; }
        public int[][] Reels { get; set; } = Array.Empty<int[]>();
    }

    public static class DatabaseManager
    {
        public static string ConnectionString { get; set; } = string.Empty;

        // Retained in memory only for BDD test inspection
        public static ConcurrentDictionary<ulong, SpinAuditData> SpinAudits { get; } = new();

        public static AppDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 30)));
            return new AppDbContext(optionsBuilder.Options);
        }

        public static string ParseDsn(string dsn)
        {
            if (string.IsNullOrWhiteSpace(dsn)) return string.Empty;

            // Handle Go format: user:pass@tcp(ip:port)/dbname?charset=utf8mb4
            var match = Regex.Match(dsn, @"^(?<user>[^:]+):(?<pass>[^@]*)@tcp\((?<host>[^:]+):(?<port>\d+)\)\/(?<db>[^\?]+)");
            if (match.Success)
            {
                var user = match.Groups["user"].Value;
                var pass = match.Groups["pass"].Value;
                var host = match.Groups["host"].Value;
                var port = match.Groups["port"].Value;
                var db = match.Groups["db"].Value;
                return $"Server={host};Port={port};Database={db};Uid={user};Pwd={pass};Charset=utf8mb4;Pooling=false;";
            }

            return dsn;
        }

        public static void SyncTables()
        {
            using var db = CreateDbContext();
            try
            {
                var relationalCreator = db.Database.GetService<IRelationalDatabaseCreator>();
                relationalCreator.CreateTables();
            }
            catch
            {
                db.Database.EnsureCreated();
            }
        }
    }
}
