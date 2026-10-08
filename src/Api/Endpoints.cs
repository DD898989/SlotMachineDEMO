using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using ServerMain.Config;
using ServerMain.Data;
using ServerMain.Game;
using ServerMain.Game.Keno;
using ServerMain.Game.Slot;

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

        private static double GetRTP(User user, Club club)
        {
            if (user.Props.TryGetValue(club.Data.ID, out var props) && props.MRTP > 0)
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

            var user = DatabaseManager.Users.Values.FirstOrDefault(u => string.Equals(u.Email, arg.Email, StringComparison.OrdinalIgnoreCase));
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
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (!DatabaseManager.Clubs.TryGetValue(arg.CID, out var club))
            {
                return RetErr(404, "club with given ID does not found", user.ID);
            }

            if (!Auth.CheckClubAccess(user, arg.CID))
            {
                return RetErr(403, "no access to this club", user.ID);
            }

            if (arg.UID == 0)
            {
                arg.UID = user.ID;
            }
            else if (arg.UID != user.ID)
            {
                if (!Auth.CheckMasterAccess(user, arg.CID))
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

            ulong gid = Interlocked.Increment(ref DatabaseManager.StoryCounter);
            var scene = new Scene
            {
                Story = new Story
                {
                    ID = gid,
                    CID = arg.CID,
                    UID = arg.UID,
                    Game = arg.Game
                },
                SID = DatabaseManager.SpinCounter,
                Game = anyGame
            };

            DatabaseManager.StoryQueue.Enqueue(scene.Story);
            DatabaseManager.Scenes[gid] = scene;

            var targetUser = DatabaseManager.Users.TryGetValue(arg.UID, out var tu) ? tu : user;

            return Results.Ok(new GameNewResp
            {
                GID = gid,
                Game = scene.Game,
                Wallet = targetUser.GetWallet(arg.CID)
            });
        }

        private static IResult HandleSlotSelSet(HttpContext context, SlotSelArg arg)
        {
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (!DatabaseManager.Scenes.TryGetValue(arg.GID, out var scene))
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            if (scene.Game is not ISlotGame slotGame)
            {
                return RetErr(403, "specified GID refers to non-slot game", user.ID);
            }

            try
            {
                slotGame.SetSel(arg.Sel);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return RetErr(403, ex.Message, user.ID);
            }
        }

        private static IResult HandleSlotSpin(HttpContext context, SlotSpinArg arg)
        {
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (!DatabaseManager.Scenes.TryGetValue(arg.GID, out var scene))
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            if (scene.Game is not ISlotGame game)
            {
                return RetErr(403, "specified GID refers to non-slot game", user.ID);
            }

            if (!DatabaseManager.Clubs.TryGetValue(scene.Story.CID, out var club))
            {
                return RetErr(500, "club with given ID does not found", user.ID);
            }

            if (!user.Props.TryGetValue(scene.Story.CID, out var props))
            {
                return RetErr(500, "properties for given user does not found", user.ID);
            }

            lock (scene)
            {
                if (arg.Bet != 0)
                {
                    try { game.SetBet(arg.Bet); }
                    catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
                }

                if (arg.Sel != 0)
                {
                    try { game.SetSel(arg.Sel); }
                    catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
                }

                double cost = 0;
                if (!game.FreeMode())
                {
                    cost = game.Cost();
                    if (props.Wallet < cost)
                    {
                        return RetErr(403, "not enough money on balance", user.ID);
                    }
                }

                double mrtp = GetRTP(user, club);
                var jackFreq = game.JackFreq(mrtp);
                bool isjp = jackFreq != null && jackFreq.Count > 0;
                double jprate = isjp ? club.Rate : 0.0;
                var (bank, fund, _) = club.GetCash();
                double jprent = Math.Round(cost * jprate, MidpointRounding.AwayFromZero) / 100.0;

                var wins = new List<WinItem>();
                double gain = 0;
                double debit = 0;
                double jack = 0;
                int n = 0;

                game.Prepare();

                List<int>? cheat = arg.Cheat != null ? new List<int>(arg.Cheat) : null;

                while (true)
                {
                    if (++n > AppConfig.MaxSpinAttempts)
                    {
                        return RetErr(500, "can not generate spin with current bank balance", user.ID);
                    }

                    game.Spin(mrtp - jprate, ref cheat);
                    wins.Clear();
                    game.Scanner(wins);

                    var (cgain, cok) = CascadeExtensions.CascadeGain((ISlotGeneric)game, wins, fund, mrtp - jprate);
                    if (!cok)
                    {
                        wins.Clear();
                        continue;
                    }

                    game.Spawn(wins, fund, mrtp - jprate);
                    gain = wins.Gain();
                    jack = wins.Jackpot();
                    debit = Math.Round((cost - jprent - gain) * 100.0, MidpointRounding.AwayFromZero) / 100.0;

                    if ((bank + debit >= 0 || debit > 0) && (jack == 0 || jack > AppConfig.MinJackpot))
                    {
                        break;
                    }
                    wins.Clear();
                }

                double walletDelta = Math.Round((gain + jack - cost) * 100.0, MidpointRounding.AwayFromZero) / 100.0;
                double fundDelta = Math.Round((jprent - jack) * 100.0, MidpointRounding.AwayFromZero) / 100.0;

                if (DatabaseManager.BankBat.TryGetValue(scene.Story.CID, out var sqlBank))
                {
                    sqlBank.PutSlot(scene.Story.UID, debit, fundDelta, walletDelta);
                }

                club.AddCash(debit, fundDelta, 0);
                lock (props)
                {
                    props.Wallet = Math.Round((props.Wallet + walletDelta) * 100.0, MidpointRounding.AwayFromZero) / 100.0;
                }
                game.Apply(wins);

                ulong sid = Interlocked.Increment(ref DatabaseManager.SpinCounter);
                scene.SID = sid;

                var rec = new Spinlog
                {
                    ID = sid,
                    GID = arg.GID,
                    Bet = cost,
                    Gain = gain,
                    Jack = jack,
                    Wallet = props.Wallet,
                    MRTP = mrtp,
                    Game = JsonSerializer.Serialize(scene.Game),
                    Wins = wins.Count > 0 ? JsonSerializer.Serialize(wins) : null
                };
                DatabaseManager.SpinQueue.Enqueue(rec);

                if (arg.Cheat != null && arg.Cheat.Count > 0)
                {
                    double effRtp = mrtp - jprate;
                    if (GameRegistry.InfoMap.TryGetValue(scene.Story.Game, out var gi))
                    {
                        effRtp = gi.FindClosest(mrtp - jprate);
                    }
                    var rawReels = game.GetReels(mrtp - jprate);

                    var audit = new SpinAuditData
                    {
                        SID = sid,
                        GID = arg.GID,
                        CID = scene.Story.CID,
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
                    scene.Audit = audit;
                    DatabaseManager.SpinAudits[sid] = audit;
                }
                else
                {
                    scene.Audit = null;
                }

                var resp = new SlotSpinResp
                {
                    SID = sid,
                    Game = scene.Game,
                    Wins = wins,
                    Wallet = props.Wallet,
                    JpFund = isjp ? fund : 0
                };

                return Results.Ok(resp);
            }
        }

        private static IResult HandleSlotAdminSpinInfo(HttpContext context, SlotSpinInfoArg arg)
        {
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

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
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (arg.Mult <= 1 || arg.Mult > 10)
            {
                return RetErr(400, "given value exceeds the limit", user.ID);
            }

            if (!DatabaseManager.Scenes.TryGetValue(arg.GID, out var scene))
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            if (scene.Game is not ISlotGame game)
            {
                return RetErr(403, "specified GID refers to non-slot game", user.ID);
            }

            if (!DatabaseManager.Clubs.TryGetValue(scene.Story.CID, out var club))
            {
                return RetErr(500, "club with given ID does not found", user.ID);
            }

            if (!user.Props.TryGetValue(scene.Story.CID, out var props))
            {
                return RetErr(500, "properties for given user does not found", user.ID);
            }

            double gain = game.GetGain();
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
            double mrtp = GetRTP(user, club);

            double upgainResult = 0;
            double upgainTemp = risk * arg.Mult;
            if (bank >= upgainTemp)
            {
                double side = (1.0 / arg.Mult) * (mrtp / 100.0);
                if (arg.Cheat == 1)
                {
                    side = 1.0;
                }
                else if (arg.Cheat == -1)
                {
                    side = -0.1;
                }

                if (Random.Shared.NextDouble() < side)
                {
                    upgainResult = upgainTemp;
                }
            }

            double newgain = Math.Round(upgainResult * 100) / 100.0;
            bool isWin = newgain > 0;
            game.SetGain(newgain);
            double walletDelta = Math.Round((newgain - risk) * 100) / 100.0;
            double bankDelta = -walletDelta;

            props.Wallet = Math.Round((props.Wallet + walletDelta) * 100) / 100.0;

            if (DatabaseManager.BankBat.TryGetValue(scene.Story.CID, out var sqlBank))
            {
                sqlBank.PutSlot(scene.Story.UID, bankDelta, 0, walletDelta);
            }
            club.AddBank(bankDelta);

            ulong id = Interlocked.Increment(ref DatabaseManager.MultCounter);
            var rec = new Multlog
            {
                ID = id,
                GID = arg.GID,
                MRTP = mrtp,
                Mult = arg.Mult,
                Risk = risk,
                Win = isWin,
                Gain = newgain,
                Wallet = props.Wallet
            };
            DatabaseManager.MultQueue.Enqueue(rec);

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
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (!DatabaseManager.Scenes.TryGetValue(arg.GID, out var scene))
            {
                return RetErr(404, "game with given ID is not opened", user.ID);
            }

            if (scene.Game is not IKenoGame game)
            {
                return RetErr(403, "specified GID refers to non-keno game", user.ID);
            }

            if (!DatabaseManager.Clubs.TryGetValue(scene.Story.CID, out var club))
            {
                return RetErr(500, "club with given ID does not found", user.ID);
            }

            if (!user.Props.TryGetValue(scene.Story.CID, out var props))
            {
                return RetErr(500, "properties for given user does not found", user.ID);
            }

            if (arg.Bet != 0)
            {
                try { game.SetBet(arg.Bet); }
                catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
            }

            if (arg.Sel != null && arg.Sel.Count > 0)
            {
                var bs = new ServerMain.Util.Bitset128();
                bs.Pack(arg.Sel);
                try { game.SetSel(bs); }
                catch (Exception ex) { return RetErr(403, ex.Message, user.ID); }
            }

            double cost = game.GetBet();
            if (props.Wallet < cost)
            {
                return RetErr(403, "not enough money on balance", user.ID);
            }

            double mrtp = GetRTP(user, club);
            List<int>? cheat = arg.Cheat != null ? new List<int>(arg.Cheat) : null;
            game.Spin(mrtp, ref cheat);

            var wins = new KenoWins();
            game.Scanner(wins);

            double gain = wins.Pay;
            double walletDelta = Math.Round((gain - cost) * 100) / 100.0;
            props.Wallet = Math.Round((props.Wallet + walletDelta) * 100) / 100.0;

            if (DatabaseManager.BankBat.TryGetValue(scene.Story.CID, out var sqlBank))
            {
                sqlBank.PutSlot(scene.Story.UID, cost - gain, 0, walletDelta);
            }
            club.AddBank(cost - gain);

            ulong sid = Interlocked.Increment(ref DatabaseManager.SpinCounter);
            scene.SID = sid;

            var rec = new Spinlog
            {
                ID = sid,
                GID = arg.GID,
                Bet = cost,
                Gain = gain,
                Jack = 0,
                Wallet = props.Wallet,
                MRTP = mrtp,
                Game = JsonSerializer.Serialize(scene.Game),
                Wins = JsonSerializer.Serialize(wins)
            };
            DatabaseManager.SpinQueue.Enqueue(rec);

            return Results.Ok(new KenoSpinResp
            {
                SID = sid,
                Game = scene.Game,
                Wins = wins,
                Wallet = props.Wallet
            });
        }

        private static IResult HandlePropWalletGet(HttpContext context, PropWalletGetArg arg)
        {
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (arg.UID == 0)
            {
                arg.UID = user.ID;
            }

            if (!DatabaseManager.Users.TryGetValue(arg.UID, out var targetUser))
            {
                return RetErr(404, "user with given ID does not found", user.ID);
            }

            if (!targetUser.Props.TryGetValue(arg.CID, out var props))
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
            var admin = Auth.GetUserFromContext(context, out var err);
            if (admin == null) return RetErr(401, err ?? "authorization is required");

            if (Math.Abs(arg.Sum) > AppConfig.AdjunctLimit)
            {
                return RetErr(400, "given value exceeds the limit", admin.ID);
            }

            if (!Auth.CheckBookerAccess(admin, arg.CID))
            {
                return RetErr(403, "action not allowed", admin.ID);
            }

            if (!DatabaseManager.Users.TryGetValue(arg.UID, out var targetUser))
            {
                return RetErr(404, "user with given ID does not found", admin.ID);
            }

            if (!targetUser.Props.TryGetValue(arg.CID, out var props))
            {
                return RetErr(500, "properties for given user does not found", admin.ID);
            }

            if (props.Wallet + arg.Sum < 0)
            {
                return RetErr(403, "not enough money on balance", admin.ID);
            }

            if (DatabaseManager.BankBat.TryGetValue(arg.CID, out var sqlBank))
            {
                sqlBank.Add(arg.UID, admin.ID, props.Wallet + arg.Sum, arg.Sum);
            }

            props.Wallet += arg.Sum;

            return Results.Ok(new PropWalletAddResp
            {
                Wallet = props.Wallet
            });
        }

        private static IResult HandlePropRtpSet(HttpContext context, PropRtpSetArg arg)
        {
            var admin = Auth.GetUserFromContext(context, out var err);
            if (admin == null) return RetErr(401, err ?? "authorization is required");

            if (!Auth.CheckBookerAccess(admin, arg.CID))
            {
                return RetErr(403, "action not allowed", admin.ID);
            }

            if (!DatabaseManager.Users.TryGetValue(arg.UID, out var targetUser))
            {
                return RetErr(404, "user with given ID does not found", admin.ID);
            }

            if (!targetUser.Props.TryGetValue(arg.CID, out var props))
            {
                return RetErr(500, "properties for given user does not found", admin.ID);
            }

            if (DatabaseManager.BankBat.TryGetValue(arg.CID, out var sqlBank))
            {
                sqlBank.MRTP(arg.UID, arg.MRTP);
            }

            props.MRTP = arg.MRTP;
            return Results.NoContent();
        }

        private static IResult HandleClubInfo(HttpContext context, ClubInfoArg arg)
        {
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (!DatabaseManager.Clubs.TryGetValue(arg.CID, out var club))
            {
                return RetErr(404, "club with given ID does not found", user.ID);
            }

            var d = club.GetData();
            return Results.Ok(new ClubInfoResp
            {
                Name = d.Name,
                Bank = d.Bank,
                Fund = d.Fund,
                Lock = d.Lock,
                Rate = d.Rate,
                MRTP = d.MRTP
            });
        }

        private static IResult HandleClubCashin(HttpContext context, ClubCashinArg arg)
        {
            var user = Auth.GetUserFromContext(context, out var err);
            if (user == null) return RetErr(401, err ?? "authorization is required");

            if (!Auth.CheckMasterAccess(user, arg.CID))
            {
                return RetErr(403, "action not allowed", user.ID);
            }

            if (!DatabaseManager.Clubs.TryGetValue(arg.CID, out var club))
            {
                return RetErr(404, "club with given ID does not found", user.ID);
            }

            var (bank, fund, lockVal) = club.GetCash();
            bank += arg.BankSum;
            fund += arg.FundSum;
            lockVal += arg.LockSum;

            if (bank < 0) return RetErr(403, "club bank out of limits", user.ID);
            if (fund < 0) return RetErr(403, "club fund out of limits", user.ID);
            if (lockVal < 0) return RetErr(403, "club deposit out of limits", user.ID);

            ulong bid = 0;
            try
            {
                using var db = DatabaseManager.CreateDbContext();
                using var tran = db.Database.BeginTransaction();

                var clubEntity = db.ClubData.Find(club.Data.ID);
                if (clubEntity != null)
                {
                    clubEntity.Bank += arg.BankSum;
                    clubEntity.Fund += arg.FundSum;
                    clubEntity.Lock += arg.LockSum;
                }

                var banklog = new Banklog
                {
                    CID = club.Data.ID,
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

                bid = banklog.ID;
            }
            catch (Exception ex)
            {
                return RetErr(500, ex.Message, user.ID);
            }

            club.AddCash(arg.BankSum, arg.FundSum, arg.LockSum);

            return Results.Ok(new ClubCashinResp
            {
                BID = bid,
                Bank = bank,
                Fund = fund,
                Lock = lockVal
            });
        }
    }
}
