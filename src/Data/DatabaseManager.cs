using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ServerMain.Config;
using ServerMain.Game;

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

    public class Scene
    {
        public Story Story { get; set; } = new();
        public ulong SID { get; set; }
        public Gamble? Game { get; set; }
        public SpinAuditData? Audit { get; set; }
    }

    public class SqlBank
    {
        private readonly ulong _cid;
        private readonly int _capacity;
        private readonly int _logsize;
        private readonly object _lock = new();

        private double _banksum;
        private double _fundsum;
        private readonly Dictionary<ulong, double> _usersum = new();
        private readonly Dictionary<ulong, double> _userrtp = new();
        private readonly List<Walletlog> _log = new();
        private DateTime _lft = DateTime.UtcNow;

        public SqlBank(ulong cid, int capacity, int logsize)
        {
            _cid = cid;
            _capacity = capacity;
            _logsize = logsize;
        }

        public void PutSlot(ulong uid, double bankDebit, double fundDelta, double walletDelta)
        {
            lock (_lock)
            {
                _banksum += bankDebit;
                _fundsum += fundDelta;
                _usersum[uid] = _usersum.GetValueOrDefault(uid, 0.0) + walletDelta;

                if (_usersum.Count >= _capacity)
                {
                    FlushInternal();
                }
            }
        }

        public void Put(ulong uid, double sum)
        {
            PutSlot(uid, sum, 0, -sum);
        }

        public void Add(ulong uid, ulong aid, double wallet, double sum)
        {
            lock (_lock)
            {
                _usersum[uid] = _usersum.GetValueOrDefault(uid, 0.0) + sum;
                _log.Add(new Walletlog
                {
                    CID = _cid,
                    UID = uid,
                    AID = aid,
                    Wallet = wallet,
                    Sum = sum
                });

                if (_usersum.Count >= _capacity || _log.Count >= _logsize)
                {
                    FlushInternal();
                }
            }
        }

        public void MRTP(ulong uid, double mrtp)
        {
            lock (_lock)
            {
                _userrtp[uid] = mrtp;
                if (_userrtp.Count >= _capacity)
                {
                    FlushInternal();
                }
            }
        }

        public void Flush(TimeSpan d)
        {
            lock (_lock)
            {
                if (d == TimeSpan.Zero || (DateTime.UtcNow - _lft) >= d)
                {
                    FlushInternal();
                }
            }
        }

        private void FlushInternal()
        {
            if (_banksum == 0 && _fundsum == 0 && _usersum.Count == 0 && _userrtp.Count == 0 && _log.Count == 0)
            {
                _lft = DateTime.UtcNow;
                return;
            }

            try
            {
                using var db = DatabaseManager.CreateDbContext();
                using var tran = db.Database.BeginTransaction();

                if (_banksum != 0 || _fundsum != 0)
                {
                    var club = db.ClubData.Find(_cid);
                    if (club != null)
                    {
                        club.Bank += _banksum;
                        club.Fund += _fundsum;
                    }
                }

                if (_usersum.Count > 0)
                {
                    var uids = _usersum.Keys.ToList();
                    var propsList = db.Props.Where(p => p.CID == _cid && uids.Contains(p.UID)).ToList();
                    foreach (var prop in propsList)
                    {
                        if (_usersum.TryGetValue(prop.UID, out var sum))
                        {
                            prop.Wallet += sum;
                        }
                    }
                }

                if (_userrtp.Count > 0)
                {
                    var rtpUids = _userrtp.Keys.ToList();
                    var rtpPropsList = db.Props.Where(p => p.CID == _cid && rtpUids.Contains(p.UID)).ToList();
                    foreach (var prop in rtpPropsList)
                    {
                        if (_userrtp.TryGetValue(prop.UID, out var mrtp))
                        {
                            prop.MRTP = mrtp;
                        }
                    }
                }

                if (_log.Count > 0)
                {
                    db.Walletlogs.AddRange(_log);
                }

                db.SaveChanges();
                tran.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SqlBank Flush Error] cid={_cid}: {ex.Message}");
            }
            finally
            {
                _banksum = 0;
                _fundsum = 0;
                _usersum.Clear();
                _userrtp.Clear();
                _log.Clear();
                _lft = DateTime.UtcNow;
            }
        }
    }

    public static class DatabaseManager
    {
        public static string ConnectionString { get; set; } = string.Empty;

        public static ConcurrentDictionary<ulong, Club> Clubs { get; } = new();
        public static ConcurrentDictionary<ulong, User> Users { get; } = new();
        public static ConcurrentDictionary<ulong, Scene> Scenes { get; } = new();
        public static ConcurrentDictionary<ulong, SpinAuditData> SpinAudits { get; } = new();
        public static ConcurrentDictionary<ulong, SqlBank> BankBat { get; } = new();

        public static readonly ConcurrentQueue<Story> StoryQueue = new();
        public static readonly ConcurrentQueue<Spinlog> SpinQueue = new();
        public static readonly ConcurrentQueue<Multlog> MultQueue = new();

        public static ulong StoryCounter;
        public static ulong SpinCounter;
        public static ulong MultCounter;

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

        public static void SyncMemory()
        {
            using var db = CreateDbContext();

            // Load Clubs
            var clubList = db.ClubData.AsNoTracking().ToList();
            foreach (var cd in clubList)
            {
                Clubs[cd.ID] = new Club(cd);
                BankBat[cd.ID] = new SqlBank(cd.ID, AppConfig.ClubUpdateBuffer, AppConfig.ClubInsertBuffer);
            }
            Console.WriteLine($"loaded {Clubs.Count} clubs");

            // Load Users
            var userList = db.Users.AsNoTracking().ToList();
            foreach (var u in userList)
            {
                Users[u.ID] = u;
            }
            Console.WriteLine($"loaded {Users.Count} users");

            // Load Props
            var propsList = db.Props.AsNoTracking().ToList();
            foreach (var p in propsList)
            {
                if (Users.TryGetValue(p.UID, out var user))
                {
                    user.InsertProps(p);
                }
            }

            // Counters
            StoryCounter = (ulong)db.Stories.Count();
            SpinCounter = (ulong)db.Spinlogs.Count();
            MultCounter = (ulong)db.Multlogs.Count();
        }

        private static readonly object _flushLock = new();

        public static void FlushQueues(bool forceAll = false)
        {
            if (!Monitor.TryEnter(_flushLock))
            {
                return;
            }

            try
            {
                FlushQueueInChunks(StoryQueue, (db, chunk) => db.Stories.AddRange(chunk), chunkSize: 2000);
                FlushQueueInChunks(SpinQueue, (db, chunk) => db.Spinlogs.AddRange(chunk), chunkSize: 2000);
                FlushQueueInChunks(MultQueue, (db, chunk) => db.Multlogs.AddRange(chunk), chunkSize: 2000);

                foreach (var sb in BankBat.Values)
                {
                    try
                    {
                        sb.Flush(forceAll ? TimeSpan.Zero : AppConfig.SqlFlushTick);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SqlLoop Bank Flush Error]: {ex.Message}");
                    }
                }
            }
            finally
            {
                Monitor.Exit(_flushLock);
            }
        }

        private static void FlushQueueInChunks<T>(
            ConcurrentQueue<T> queue,
            Action<AppDbContext, List<T>> addAction,
            int chunkSize = 2000)
        {
            while (!queue.IsEmpty)
            {
                var chunk = new List<T>(chunkSize);
                while (chunk.Count < chunkSize && queue.TryDequeue(out var item))
                {
                    chunk.Add(item);
                }

                if (chunk.Count == 0) break;

                try
                {
                    using var db = CreateDbContext();
                    db.Database.SetCommandTimeout(60);
                    addAction(db, chunk);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SqlLoop Flush Error] Type={typeof(T).Name}, Count={chunk.Count}: {ex.Message}");
                }
            }
        }
    }
}
