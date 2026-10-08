using ServerMain.Game.Keno;
using ServerMain.Game.Slot.Agt;
using ServerMain.Game.Slot.MegaJack;
using ServerMain.Game.Slot.NetEnt;
using ServerMain.Game.Slot.Novomatic;

namespace ServerMain.Game
{
    public static class GameRegistry
    {
        public static readonly AlgInfo BookOfRaInfo = new()
        {
            Aliases = new List<GameAlias>
            {
                new() { Game = EGame.novomatic_bookofra, Prov = "Novomatic", Name = "Book of Ra" },
                new() { Game = EGame.novomatic_bookofradeluxe, Prov = "Novomatic", Name = "Book of Ra Deluxe" },
                new() { Game = EGame.novomatic_gateofradeluxe, Prov = "Novomatic", Name = "Gate of Ra Deluxe" },
                new() { Game = EGame.novomatic_goldenprophecies, Prov = "Novomatic", Name = "Golden Prophecies" },
                new() { Game = EGame.novomatic_downunder, Prov = "Novomatic", Name = "Down Under" },
                new() { Game = EGame.novomatic_godofsun, Prov = "Novomatic", Name = "God of Sun" },
                new() { Game = EGame.novomatic_lordoftheocean, Prov = "Novomatic", Name = "Lord of the Ocean" },
                new() { Game = EGame.novomatic_faust, Prov = "Novomatic", Name = "Faust" },
                new() { Game = EGame.novomatic_therealkinggoldrecords, Prov = "Novomatic", Name = "The Real King Gold Records" },
                new() { Game = EGame.novomatic_angrybirds, Prov = "Novomatic", Name = "Angry Birds" },
            },
            AlgDescr = new AlgDescr { RTP = BookOfRaGame.ReelsMap.Keys.OrderBy(k => k).ToList() }
        };

        public static readonly AlgInfo GonzosQuestInfo = new()
        {
            Aliases = new List<GameAlias>
            {
                new() { Game = EGame.netent_gonzosquest, Prov = "NetEnt", Name = "Gonzo's Quest" }
            },
            AlgDescr = new AlgDescr { RTP = GonzosQuestGame.ReelsMap.Keys.OrderBy(k => k).ToList() }
        };

        public static readonly AlgInfo SlotopolInfo = new()
        {
            Aliases = new List<GameAlias>
            {
                new() { Game = EGame.megajack_slotopol, Prov = "Megajack", Name = "Slotopol" }
            },
            AlgDescr = new AlgDescr { RTP = SlotopolGame.ReelsMap.Keys.OrderBy(k => k).ToList() }
        };

        public static readonly AlgInfo Jokers100Info = new()
        {
            Aliases = new List<GameAlias>
            {
                new() { Game = EGame.agt_100jokers, Prov = "AGT", Name = "100 Jokers" },
                new() { Game = EGame.agt_50happysanta, Prov = "AGT", Name = "50 Happy Santa" },
                new() { Game = EGame.agt_40bigfoot, Prov = "AGT", Name = "40 Bigfoot" },
            },
            AlgDescr = new AlgDescr { RTP = Jokers100Game.ReelsMap.Keys.OrderBy(k => k).ToList() }
        };

        public static readonly AlgInfo AmericanKenoInfo = new()
        {
            Aliases = new List<GameAlias>
            {
                new() { Game = EGame.aristocrat_americankeno, Prov = "Aristocrat", Name = "American Keno" }
            },
            AlgDescr = new AlgDescr { RTP = new List<double> { 89.250235 } }
        };

        public static readonly List<AlgInfo> AlgList = new()
        {
            BookOfRaInfo,
            GonzosQuestInfo,
            SlotopolInfo,
            Jokers100Info,
            AmericanKenoInfo
        };

        public static readonly Dictionary<EGame, GameInfo> InfoMap = MakeInfoMap(AlgList);

        private static Dictionary<EGame, GameInfo> MakeInfoMap(List<AlgInfo> list)
        {
            var map = new Dictionary<EGame, GameInfo>();
            foreach (var ai in list)
            {
                foreach (var ga in ai.Aliases)
                {
                    map[ga.Game] = new GameInfo
                    {
                        GameAlias = ga,
                        AlgDescr = ai.AlgDescr
                    };
                }
            }
            return map;
        }

        public static Gamble NewGame(EGame g)
        {
            return g switch
            {
                EGame.novomatic_bookofra or
                EGame.novomatic_downunder or
                EGame.novomatic_godofsun => new BookOfRaGame(9),

                EGame.novomatic_bookofradeluxe or
                EGame.novomatic_gateofradeluxe or
                EGame.novomatic_goldenprophecies or
                EGame.novomatic_lordoftheocean or
                EGame.novomatic_faust or
                EGame.novomatic_therealkinggoldrecords or
                EGame.novomatic_angrybirds => new BookOfRaGame(10),

                EGame.netent_gonzosquest => new GonzosQuestGame(),

                EGame.megajack_slotopol => new SlotopolGame(21),

                EGame.agt_100jokers => new Jokers100Game(100),
                EGame.agt_50happysanta => new Jokers100Game(50),
                EGame.agt_40bigfoot => new Jokers100Game(40),

                EGame.aristocrat_americankeno => new AmericanKenoGame(),

                _ => throw new KeyNotFoundException("no game alias")
            };
        }
    }
}
