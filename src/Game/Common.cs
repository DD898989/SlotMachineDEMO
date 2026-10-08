namespace ServerMain.Game
{
    public interface Gamble
    {
        void Spin(double mrtp, ref List<int>? cheat);
        void SetBet(double bet);
    }

    public enum EGame : uint
    {
        None = 0,
        // Novomatic
        novomatic_bookofra = 1,
        novomatic_bookofradeluxe = 2,
        novomatic_gateofradeluxe = 3,
        novomatic_goldenprophecies = 4,
        novomatic_downunder = 5,
        novomatic_godofsun = 6,
        novomatic_lordoftheocean = 7,
        novomatic_faust = 8,
        novomatic_therealkinggoldrecords = 9,
        novomatic_angrybirds = 10,

        // NetEnt
        netent_gonzosquest = 11,

        // Megajack
        megajack_slotopol = 12,

        // AGT
        agt_100jokers = 13,
        agt_50happysanta = 14,
        agt_40bigfoot = 15,

        // Aristocrat
        aristocrat_americankeno = 16,
    }

    public class GameAlias
    {
        public EGame Game { get; set; }
        public string Prov { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string ID => $"{Prov}/{Name}".ToLowerInvariant();
    }

    public class AlgDescr
    {
        public List<double> RTP { get; set; } = new();

        public double FindClosest(double mrtp)
        {
            double closest = -1000;
            foreach (var p in RTP)
            {
                if (Math.Abs(mrtp - p) < Math.Abs(mrtp - closest))
                {
                    closest = p;
                }
            }
            return closest;
        }
    }

    public class AlgInfo
    {
        public List<GameAlias> Aliases { get; set; } = new();
        public AlgDescr AlgDescr { get; set; } = new();
    }

    public class GameInfo
    {
        public GameAlias GameAlias { get; set; } = new();
        public AlgDescr AlgDescr { get; set; } = new();

        public double FindClosest(double mrtp) => AlgDescr.FindClosest(mrtp);
    }
}
