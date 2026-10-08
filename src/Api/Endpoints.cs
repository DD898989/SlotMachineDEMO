using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using ServerMain.Config;
using ServerMain.Data;
using ServerMain.Game;
using ServerMain.Game.Keno;
using ServerMain.Game.Slot;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ServerMain.Api
{
    public static class Endpoints
    {
        public static void MapApiRoutes(this WebApplication app)
        {
            // Health check
            app.MapGet("/", () => Results.Ok("Server is ready"));

            // Auth
            app.MapPost("/signin", HandleSignin);

            // Game
            app.MapPost("/game/new", HandleGameNew);

            // Slot
            app.MapPost("/slot/sel/set", HandleSlotSelSet);
            app.MapPost("/slot/spin", HandleSlotSpin);
            app.MapPost("/slot/admin/spin/info", HandleSlotAdminSpinInfo);
            app.MapPost("/slot/doubleup", HandleSlotDoubleup);

            // Keno
            app.MapPost("/keno/spin", HandleKenoSpin);

            // Props
            app.MapPost("/prop/wallet/get", HandlePropWalletGet);
            app.MapPost("/prop/wallet/add", HandlePropWalletAdd);
            app.MapPost("/prop/rtp/set", HandlePropRtpSet);

            // Club
            app.MapPost("/club/info", HandleClubInfo);
            app.MapPost("/club/cashin", HandleClubCashin);

            // Admin / Test Graceful Shutdown
            app.MapPost("/api/admin/shutdown", (Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime) =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    lifetime.StopApplication();
                });
                return Results.Ok("Shutting down");
            });
        }

        private static IResult RetErr(int statusCode, string what, ulong uid = 0)
        {
            return Results.Json(new ErrorResponse { What = what, UID = uid }, statusCode: statusCode);
        }

        private static double GetRTP(User user, ClubData club, Props? props = null)
        {
            if (props != null && props.MRTP > 0)
            {
                return props.MRTP;
            }
            return club.MRTP;
        }

        private static IResult HandleSignin(SigninArg arg)
        {
            if (string.IsNullOrWhiteSpace(arg.Email))
            {
                return RetErr(400, "email is empty");
            }

            using var db = DatabaseManager.CreateDbContext();
            var user = db.Users.FirstOrDefault(u => u.Email.ToLower() == arg.Email.ToLower());
            if (user == null)
            {
                return RetErr(404, "user with given ID does not found");
            }

            if (!string.IsNullOrEmpty(arg.Secret))
            {
                if (user.Secret != arg.Secret)
                {
                    return RetErr(403, "password does not match", user.ID);
                }
            }
            else if (!string.IsNullOrEmpty(arg.SigTime) && !string.IsNullOrEmpty(arg.HS256))
            {
                if (!DateTime.TryParse(arg.SigTime, out var sigTimeUtc))
                {
                    return RetErr(400, "invalid signature time format", user.ID);
                }

                if (Math.Abs((DateTime.UtcNow - sigTimeUtc.ToUniversalTime()).TotalSeconds) > AppConfig.NonceTimeout.TotalSeconds)
                {
                    return RetErr(403, "signature is too old or too new", user.ID);
                }

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(user.Secret));
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(arg.SigTime));
                var computedHex = Convert.ToHexStringLower(computedHash);

                if (!string.Equals(computedHex, arg.HS256, StringComparison.OrdinalIgnoreCase))
                {
                    return RetErr(403, "password does not match", user.ID);
                }
            }
            else
            {
                return RetErr(400, "either Secret or SigTime+HS256 must be provided", user.ID);
            }

            var access = Auth.MakeToken(user.ID, AppConfig.AccessKey, AppConfig.AccessTTL);
            var refrsh = Auth.MakeToken(user.ID, AppConfig.RefreshKey, AppConfig.RefreshTTL);
            var now = DateTime.UtcNow;

            return Results.Ok(new SigninResp
            {
                Access = access,
                Refrsh = refrsh,
                Expire = now.Add(AppConfig.AccessTTL).ToString("o"),
                Living = now.Add(AppConfig.RefreshTTL).ToString("o"),
                UID = user.ID,
                Email = user.Email
            });
        }

        private static IResult HandleGameNew(HttpContext context, GameNewArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            var club = db.ClubData.Find(arg.CID);
            if (club == null)
            {
                return RetErr(404, "club with given ID does not found", user.ID);
            }

            var props = db.Props.FirstOrDefault(p => p.UID == user.ID && p.CID == arg.CID);
            if (!Auth.CheckClubAccess(user, props))
            {
                return RetErr(403, "no access to this club", user.ID);
            }

            if (arg.UID == 0)
            {
                arg.UID = user.ID;
            }
            else if (arg.UID != user.ID)
            {
                if (!Auth.CheckMasterAccess(user, props))
                {
                    return RetErr(403, "action not allowed", user.ID);
                }
            }

            Gamble anyGame;
            try
            {
                anyGame = GameRegistry.NewGame(arg.Game);
            }
            catch
            {
                return RetErr(400, "no game alias", user.ID);
            }

            var story = new Story
            {
                CID = arg.CID,
                UID = arg.UID,
                Game = arg.Game,
                State = GameRegistry.SerializeGame(anyGame)
            };

            db.Stories.Add(story);
            db.SaveChanges();
            ulong gid = story.ID;

            var targetProp = db.Props.FirstOrDefault(p => p.UID == arg.UID && p.CID == arg.CID);
            double wallet = targetProp?.Wallet ?? 0.0;

            return Results.Ok(new GameNewResp
            {
                GID = gid,
                Game = anyGame,
                Wallet = wallet
            });
        }

        private static IResult HandleSlotSelSet(HttpContext context, SlotSelArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            var story = db.Stories.Find(arg.GID);
            if (story == null)
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            var game = GameRegistry.RestoreGame(story.Game, story.State);
            if (game is not ISlotGame slotGame)
            {
                return RetErr(403, "specified GID refers to non-slot game", user.ID);
            }

            try
            {
                slotGame.SetSel(arg.Sel);
                story.State = GameRegistry.SerializeGame(slotGame);
                db.SaveChanges();
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return RetErr(403, ex.Message, user.ID);
            }
        }

        public class Temp
        {
            public User User { get; set; } = null!;
            public Story Story { get; set; } = null!;
            public ClubData Club { get; set; } = null!;
            public Props Props { get; set; } = null!;
        }
        public record TCSReadRequest(ulong GID, TaskCompletionSource<Temp> Tcs);
        private static readonly Channel<TCSReadRequest> _readChannel = Channel.CreateUnbounded<TCSReadRequest>();
        private static readonly Timer _timerRead = new(ProcessReadLoop, null, 0, 50/*1=1ms*/);
        private static void ProcessReadLoop(object? state)
        {
            var batch = new List<TCSReadRequest>();
            while (_readChannel.Reader.TryRead(out var req))
                batch.Add(req);

            if (batch.Count == 0)
                return;

            var db = DatabaseManager.CreateDbContext();
            var GIDS = batch.Select(r => r.GID).Distinct().ToList();

            var sessions = (
                from g in db.Stories
                join u in db.Users on g.UID equals u.ID
                join c in db.ClubData on g.CID equals c.ID
                join p in db.Props on new { g.CID, UID = g.UID } equals new { p.CID, p.UID }
                where GIDS.Contains(g.ID)
                select new Temp
                {
                    User = u,
                    Story = g,
                    Club = c,
                    Props = p,
                }
            ).ToList();

            var sessionMap = sessions.ToDictionary(s => s.Story.ID);

            foreach (var req in batch)
            {
                sessionMap.TryGetValue(req.GID, out var s);
                req.Tcs.SetResult(s);
            }
        }


        public record TCSWriteRequest(Spinlog Rec, ulong CID, ulong UID, ulong GID, string State, double Debit, double FundDelta, double WalletDelta, TaskCompletionSource<ulong> Tcs);
        private static readonly Channel<TCSWriteRequest> _writeChannel = Channel.CreateUnbounded<TCSWriteRequest>();
        private static readonly Timer _timerWrite = new(ProcessWriteLoop, null, 0, 100/*1=1ms*/);
        private static void ProcessWriteLoop(object? state)
        {
                var batch = new List<TCSWriteRequest>();
                while (_writeChannel.Reader.TryRead(out var req))
                    batch.Add(req);

                if (batch.Count == 0) 
                    return;

                var db = DatabaseManager.CreateDbContext();
                var tx = db.Database.BeginTransaction();

                db.Spinlogs.AddRange(batch.Select(r => r.Rec));
                db.SaveChanges();

                var sqlBatch = ((MySqlConnection)db.Database.GetDbConnection()).CreateBatch();
                sqlBatch.Transaction = (MySqlTransaction)tx.GetDbTransaction();

                foreach (var cg in batch.GroupBy(r => r.CID))
                {
                    var cmd = new MySqlBatchCommand($"UPDATE `{nameof(ClubData)}` SET `{nameof(ClubData.Bank)}`=ROUND(`{nameof(ClubData.Bank)}`+@d,2), `{nameof(ClubData.Fund)}`=ROUND(`{nameof(ClubData.Fund)}`+@f,2) WHERE `{nameof(ClubData.ID)}`=@id");
                    cmd.Parameters.AddWithValue("@d", Math.Round(cg.Sum(r => r.Debit) * 100.0, MidpointRounding.AwayFromZero) / 100.0);
                    cmd.Parameters.AddWithValue("@f", Math.Round(cg.Sum(r => r.FundDelta) * 100.0, MidpointRounding.AwayFromZero) / 100.0);
                    cmd.Parameters.AddWithValue("@id", cg.Key);
                    sqlBatch.BatchCommands.Add(cmd);
                }

                foreach (var sg in batch.GroupBy(r => r.GID))
                {
                    var cmd = new MySqlBatchCommand($"UPDATE `{nameof(Story)}` SET `{nameof(Story.State)}`=@s WHERE `{nameof(Story.ID)}`=@id");
                    cmd.Parameters.AddWithValue("@s", sg.Last().State);
                    cmd.Parameters.AddWithValue("@id", sg.Key);
                    sqlBatch.BatchCommands.Add(cmd);
                }

                foreach (var pg in batch.GroupBy(r => new { r.CID, r.UID }))
                {
                    var cmd = new MySqlBatchCommand($"UPDATE `{nameof(Props)}` SET `{nameof(Props.Wallet)}`=ROUND(`{nameof(Props.Wallet)}`+@w,2) WHERE `{nameof(Props.CID)}`=@c AND `{nameof(Props.UID)}`=@u");
                    cmd.Parameters.AddWithValue("@w", Math.Round(pg.Sum(r => r.WalletDelta) * 100.0, MidpointRounding.AwayFromZero) / 100.0);
                    cmd.Parameters.AddWithValue("@c", pg.Key.CID);
                    cmd.Parameters.AddWithValue("@u", pg.Key.UID);
                    sqlBatch.BatchCommands.Add(cmd);
                }

                sqlBatch.ExecuteNonQuery();
                tx.Commit();

                foreach (var req in batch)
                    req.Tcs.TrySetResult(req.Rec.ID);
        }

        private static async Task<IResult> HandleSlotSpin(HttpContext context, SlotSpinArg arg)
        {
            var uid = Auth.GetUID(context);
            if (uid == null)
                return RetErr(401, "authorization is required");

            var tcs = new TaskCompletionSource<Temp?>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _readChannel.Writer.WriteAsync(new TCSReadRequest(arg.GID, tcs));
            var session = await tcs.Task;

            if (session == null || session.Story == null)
                return RetErr(404, "game with given ID is not opened", uid.Value);

            if (session.User == null || session.User.ID != uid.Value)
                return RetErr(401, "authorization is required");

            var user = session.User;
            var story = session.Story;

            var game = GameRegistry.RestoreGame(story.Game, story.State);
            if (game is not ISlotGame slotGame)
                return RetErr(403, "specified GID refers to non-slot game", user.ID);

            if (session.Club == null)
                return RetErr(500, "club with given ID does not found", user.ID);

            var club = session.Club;

            if (session.Props == null)
                return RetErr(500, "properties for given user does not found", user.ID);

            var props = session.Props;

            if (arg.Bet != 0)
            {
                try { slotGame.SetBet(arg.Bet); }
                catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
            }

            if (arg.Sel != 0)
            {
                try { slotGame.SetSel(arg.Sel); }
                catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
            }

            double cost = 0;
            if (!slotGame.FreeMode())
            {
                cost = slotGame.Cost();
                if (props.Wallet < cost)
                    return RetErr(403, "not enough money on balance", user.ID);
            }

            double mrtp = GetRTP(user, club, props);
            bool isjp = slotGame.JackFreq(mrtp)?.Count > 0;
            double jprate = isjp ? club.Rate : 0.0;
            var bank = club.Bank;
            var fund = club.Fund;
            double jprent = Math.Round(cost * jprate, MidpointRounding.AwayFromZero) / 100.0;

            var wins = new List<WinItem>();
            double gain = 0;
            double debit = 0;
            double jack = 0;
            int n = 0;

            slotGame.Prepare();

            List<int>? cheat = arg.Cheat != null ? new List<int>(arg.Cheat) : null;

            while (true)
            {
                if (++n > AppConfig.MaxSpinAttempts)
                    return RetErr(500, "can not generate spin with current bank balance", user.ID);


                wins.Clear();
                slotGame.Spin(mrtp - jprate, ref cheat);
                slotGame.Scanner(wins);

                var (cgain, cok) = CascadeExtensions.CascadeGain((ISlotGeneric)slotGame, wins, fund, mrtp - jprate);
                if (!cok)
                {
                    wins.Clear();
                    continue;
                }

                slotGame.Spawn(wins, fund, mrtp - jprate);
                gain = wins.Gain();
                jack = wins.Jackpot();
                debit = Math.Round((cost - jprent - gain) * 100.0, MidpointRounding.AwayFromZero) / 100.0;

                if ((bank + debit >= 0 || debit > 0) && (jack == 0 || jack > AppConfig.MinJackpot))
                {  //^^^^^^^^^^^^^^^^^
                   //兩個參數比較不合理 前者是莊家水池(能賠多少)  後者是玩家從[莊家水池+玩家彩金池]贏的錢  算法有問題?
                    break;
                }
            }

            double walletDelta = Math.Round((gain + jack - cost) * 100.0, MidpointRounding.AwayFromZero) / 100.0;
            double fundDelta = Math.Round((jprent - jack) * 100.0, MidpointRounding.AwayFromZero) / 100.0;

            slotGame.Apply(wins);

            story.State = GameRegistry.SerializeGame(slotGame);
            props.Wallet = Math.Round((props.Wallet + walletDelta) * 100.0, MidpointRounding.AwayFromZero) / 100.0;

            var rec = new Spinlog
            {
                GID = arg.GID,
                Bet = cost,
                Gain = gain,
                Jack = jack,
                Wallet = props.Wallet,
                MRTP = mrtp,
                Game = story.State,
                Wins = wins.Count > 0 ? JsonSerializer.Serialize(wins) : null
            };

            var writeTcs = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
            _writeChannel.Writer.TryWrite(new TCSWriteRequest(rec, club.ID, user.ID, arg.GID, story.State, debit, fundDelta, walletDelta, writeTcs));
            rec.ID = await writeTcs.Task;
            ulong sid = rec.ID;

            if (arg.Cheat?.Count > 0)
            {
                double effRtp = mrtp - jprate;
                if (GameRegistry.InfoMap.TryGetValue(story.Game, out var gi))
                    effRtp = gi.FindClosest(mrtp - jprate);

                var rawReels = slotGame.GetReels(mrtp - jprate);

                var audit = new SpinAuditData
                {
                    SID = sid,
                    GID = arg.GID,
                    CID = story.CID,
                    Attempts = n,
                    TargetRTP = mrtp,
                    EffRTP = effRtp,
                    FundBefore = fund,
                    FundAfter = fund + jprent - jack,
                    JackWin = jack,
                    JackRate = jprate,
                    JackRent = jprent,
                    Reels = rawReels
                };
                DatabaseManager.SpinAudits[sid] = audit;
            }

            var resp = new SlotSpinResp
            {
                SID = sid,
                Game = slotGame,
                Wins = wins,
                Wallet = props.Wallet,
                JpFund = isjp ? fund : 0
            };

            return Results.Ok(resp);
        }

        private static IResult HandleSlotAdminSpinInfo(HttpContext context, SlotSpinInfoArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            if (!user.GAL.HasFlag(AccessLevel.Admin))
            {
                return RetErr(403, "action not allowed", user.ID);
            }

            if (!DatabaseManager.SpinAudits.TryGetValue(arg.SID, out var audit))
            {
                return RetErr(404, "spin audit with given SID does not found", user.ID);
            }

            return Results.Ok(audit);
        }

        private static IResult HandleSlotDoubleup(HttpContext context, SlotDoubleupArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            using var tran = db.Database.BeginTransaction();

            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            if (arg.Mult <= 1 || arg.Mult > 10)
            {
                return RetErr(400, "given value exceeds the limit", user.ID);
            }

            var story = db.Stories.Find(arg.GID);
            if (story == null)
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            var game = GameRegistry.RestoreGame(story.Game, story.State);
            if (game is not ISlotGame slotGame)
            {
                return RetErr(403, "specified GID refers to non-slot game", user.ID);
            }

            var club = db.ClubData.Find(story.CID);
            if (club == null)
            {
                return RetErr(500, "club with given ID does not found", user.ID);
            }

            var props = db.Props.FirstOrDefault(p => p.UID == user.ID && p.CID == story.CID);
            if (props == null)
            {
                return RetErr(500, "properties for given user does not found", user.ID);
            }

            double gain = slotGame.GetGain();
            if (gain <= 0)
            {
                return RetErr(403, "no money won", user.ID);
            }

            double risk = gain;
            if (arg.Half)
            {
                risk /= 2.0;
            }

            double bank = club.Bank;
            double mrtp = GetRTP(user, club, props);

            double upgainResult = 0;
            double upgainTemp = risk * arg.Mult;
            if (bank >= upgainTemp)
            {
                double side = (1.0 / arg.Mult) * (mrtp / 100.0);
                if (arg.Cheat == 1)
                    side = 1.0;
                else if (arg.Cheat == -1)
                    side = -0.1;

                if (Random.Shared.NextDouble() < side)
                    upgainResult = upgainTemp;
            }

            double newgain = Math.Round(upgainResult * 100) / 100.0;
            bool isWin = newgain > 0;
            slotGame.SetGain(newgain);
            double walletDelta = Math.Round((newgain - risk) * 100) / 100.0;
            double bankDelta = -walletDelta;

            props.Wallet = Math.Round((props.Wallet + walletDelta) * 100) / 100.0;
            club.Bank += bankDelta;

            story.State = GameRegistry.SerializeGame(slotGame);

            var rec = new Multlog
            {
                GID = arg.GID,
                MRTP = mrtp,
                Mult = arg.Mult,
                Risk = risk,
                Win = isWin,
                Gain = newgain,
                Wallet = props.Wallet
            };
            db.Multlogs.Add(rec);
            db.SaveChanges();
            tran.Commit();

            ulong id = rec.ID;

            return Results.Ok(new SlotDoubleupResp
            {
                ID = id,
                Win = isWin,
                Risk = risk,
                Gain = newgain,
                Wallet = props.Wallet
            });
        }

        private static IResult HandleKenoSpin(HttpContext context, KenoSpinArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            using var tran = db.Database.BeginTransaction();

            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            var story = db.Stories.Find(arg.GID);
            if (story == null)
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            var game = GameRegistry.RestoreGame(story.Game, story.State);
            if (game is not IKenoGame kenoGame)
            {
                return RetErr(403, "specified GID refers to non-keno game", user.ID);
            }

            var club = db.ClubData.Find(story.CID);
            if (club == null)
            {
                return RetErr(500, "club with given ID does not found", user.ID);
            }

            var props = db.Props.FirstOrDefault(p => p.UID == user.ID && p.CID == story.CID);
            if (props == null)
            {
                return RetErr(500, "properties for given user does not found", user.ID);
            }

            if (arg.Bet != 0)
            {
                try { kenoGame.SetBet(arg.Bet); }
                catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
            }

            if (arg.Sel != null && arg.Sel.Count > 0)
            {
                var bs = new ServerMain.Util.Bitset128();
                bs.Pack(arg.Sel);
                try { kenoGame.SetSel(bs); }
                catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
            }

            double cost = kenoGame.GetBet();
            if (props.Wallet < cost)
            {
                return RetErr(403, "not enough money on balance", user.ID);
            }

            double mrtp = GetRTP(user, club, props);
            List<int>? cheat = arg.Cheat != null ? new List<int>(arg.Cheat) : null;
            kenoGame.Spin(mrtp, ref cheat);

            var wins = new KenoWins();
            kenoGame.Scanner(wins);

            double gain = wins.Pay;
            double walletDelta = Math.Round((gain - cost) * 100) / 100.0;
            props.Wallet = Math.Round((props.Wallet + walletDelta) * 100) / 100.0;
            club.Bank += cost - gain;

            story.State = GameRegistry.SerializeGame(kenoGame);

            var rec = new Spinlog
            {
                GID = arg.GID,
                Bet = cost,
                Gain = gain,
                Jack = 0,
                Wallet = props.Wallet,
                MRTP = mrtp,
                Game = GameRegistry.SerializeGame(kenoGame),
                Wins = JsonSerializer.Serialize(wins)
            };
            db.Spinlogs.Add(rec);
            db.SaveChanges();
            tran.Commit();

            ulong sid = rec.ID;

            return Results.Ok(new KenoSpinResp
            {
                SID = sid,
                Game = kenoGame,
                Wins = wins,
                Wallet = props.Wallet
            });
        }

        private static IResult HandlePropWalletGet(HttpContext context, PropWalletGetArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            if (arg.UID == 0)
            {
                arg.UID = user.ID;
            }

            var targetUser = db.Users.Find(arg.UID);
            if (targetUser == null)
            {
                return RetErr(404, "user with given ID does not found", user.ID);
            }

            var props = db.Props.FirstOrDefault(p => p.UID == arg.UID && p.CID == arg.CID);
            if (props == null)
            {
                return RetErr(500, "properties for given user does not found", user.ID);
            }

            return Results.Ok(new PropWalletGetResp
            {
                Wallet = props.Wallet,
                Access = props.Access,
                MRTP = props.MRTP
            });
        }

        private static IResult HandlePropWalletAdd(HttpContext context, PropWalletAddArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            using var tran = db.Database.BeginTransaction();

            var admin = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (admin == null) return RetErr(401, "authorization is required");

            if (Math.Abs(arg.Sum) > AppConfig.AdjunctLimit)
            {
                return RetErr(400, "given value exceeds the limit", admin.ID);
            }

            var adminProps = db.Props.FirstOrDefault(p => p.UID == admin.ID && p.CID == arg.CID);
            if (!Auth.CheckBookerAccess(admin, adminProps))
            {
                return RetErr(403, "action not allowed", admin.ID);
            }

            var targetUser = db.Users.Find(arg.UID);
            if (targetUser == null)
            {
                return RetErr(404, "user with given ID does not found", admin.ID);
            }

            var props = db.Props.FirstOrDefault(p => p.UID == arg.UID && p.CID == arg.CID);
            if (props == null)
            {
                return RetErr(500, "properties for given user does not found", admin.ID);
            }

            if (props.Wallet + arg.Sum < 0)
            {
                return RetErr(403, "not enough money on balance", admin.ID);
            }

            props.Wallet += arg.Sum;

            var wlog = new Walletlog
            {
                CID = arg.CID,
                UID = arg.UID,
                AID = admin.ID,
                Wallet = props.Wallet,
                Sum = arg.Sum
            };
            db.Walletlogs.Add(wlog);
            db.SaveChanges();
            tran.Commit();

            return Results.Ok(new PropWalletAddResp
            {
                Wallet = props.Wallet
            });
        }

        private static IResult HandlePropRtpSet(HttpContext context, PropRtpSetArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            var admin = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (admin == null) return RetErr(401, "authorization is required");

            var adminProps = db.Props.FirstOrDefault(p => p.UID == admin.ID && p.CID == arg.CID);
            if (!Auth.CheckBookerAccess(admin, adminProps))
            {
                return RetErr(403, "action not allowed", admin.ID);
            }

            var targetUser = db.Users.Find(arg.UID);
            if (targetUser == null)
            {
                return RetErr(404, "user with given ID does not found", admin.ID);
            }

            var props = db.Props.FirstOrDefault(p => p.UID == arg.UID && p.CID == arg.CID);
            if (props == null)
            {
                return RetErr(500, "properties for given user does not found", admin.ID);
            }

            props.MRTP = arg.MRTP;
            db.SaveChanges();

            return Results.NoContent();
        }

        private static IResult HandleClubInfo(HttpContext context, ClubInfoArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            var club = db.ClubData.Find(arg.CID);
            if (club == null)
            {
                return RetErr(404, "club with given ID does not found", user.ID);
            }

            return Results.Ok(new ClubInfoResp
            {
                Name = club.Name,
                Bank = club.Bank,
                Fund = club.Fund,
                Lock = club.Lock,
                Rate = club.Rate,
                MRTP = club.MRTP
            });
        }

        private static IResult HandleClubCashin(HttpContext context, ClubCashinArg arg)
        {
            using var db = DatabaseManager.CreateDbContext();
            using var tran = db.Database.BeginTransaction();

            var user = db.Users.FirstOrDefault(x => x.ID == Auth.GetUID(context));
            if (user == null) return RetErr(401, "authorization is required");

            var props = db.Props.FirstOrDefault(p => p.UID == user.ID && p.CID == arg.CID);
            if (!Auth.CheckMasterAccess(user, props))
            {
                return RetErr(403, "action not allowed", user.ID);
            }

            var club = db.ClubData.Find(arg.CID);
            if (club == null)
            {
                return RetErr(404, "club with given ID does not found", user.ID);
            }

            var bank = club.Bank;
            var fund = club.Fund;
            var lockVal = club.Lock;
            bank += arg.BankSum;
            fund += arg.FundSum;
            lockVal += arg.LockSum;

            if (bank < 0) return RetErr(403, "club bank out of limits", user.ID);
            if (fund < 0) return RetErr(403, "club fund out of limits", user.ID);
            if (lockVal < 0) return RetErr(403, "club deposit out of limits", user.ID);

            club.Bank = bank;
            club.Fund = fund;
            club.Lock = lockVal;

            var banklog = new Banklog
            {
                CID = club.ID,
                Bank = bank,
                Fund = fund,
                Lock = lockVal,
                BankSum = arg.BankSum,
                FundSum = arg.FundSum,
                LockSum = arg.LockSum
            };
            db.Banklogs.Add(banklog);
            db.SaveChanges();
            tran.Commit();

            return Results.Ok(new ClubCashinResp
            {
                BID = banklog.ID,
                Bank = bank,
                Fund = fund,
                Lock = lockVal
            });
        }
    }
}
