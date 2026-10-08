using System.Text.Json.Serialization;

namespace ServerMain.Game.Slot
{
    public struct WinItem
    {
        public double Pay { get; set; }
        public double MP { get; set; }
        public int Sym { get; set; }
        public int Num { get; set; }
        public int LI { get; set; }
        public int[][] XY { get; set; }
        public int FS { get; set; }
        public int BID { get; set; }
        public object? Bon { get; set; }
        public int JID { get; set; }
        public double JR { get; set; }

        public WinItem()
        {
            Pay = 0;
            MP = 1;
            Sym = 0;
            Num = 0;
            LI = 0;
            XY = Array.Empty<int[]>();
            FS = 0;
            BID = 0;
            Bon = null;
            JID = 0;
            JR = 0;
        }
    }

    public static class WinsExtensions
    {
        public const double InitRTP = 95.0;
        public const double JackBasis = 250000000;

        public static double Gain(this List<WinItem> wins)
        {
            double sum = 0;
            foreach (var wi in wins)
            {
                sum += wi.Pay * wi.MP;
            }
            return sum;
        }

        public static double Jackpot(this List<WinItem> wins)
        {
            double sum = 0;
            foreach (var wi in wins)
            {
                sum += wi.JR;
            }
            return sum;
        }
    }

    public interface IGrider
    {
        (int sx, int sy) Dim();
        int At(int x, int y);
        int LX(int x, int[] line);
        void SetSym(int x, int y, int sym);
        void SetCol(int x, int[] reel, int pos);
        void SpinReels(int[][] reels, ref List<int>? cheat);
        int SymNum(int sym);
        int[][] SymPos(int sym);
    }

    public interface ISlotGame : Gamble
    {
        void Scanner(List<WinItem> wins);
        double Cost();
        List<double>? JackFreq(double mrtp);
        bool FreeMode();
        int[][] GetReels(double mrtp);
        void Spawn(List<WinItem> wins, double fund, double mrtp);
        void Prepare();
        void Apply(List<WinItem> wins);
        double GetGain();
        void SetGain(double gain);
        void SetSel(int sel);
    }

    public interface ISlotGeneric : IGrider, ISlotGame
    {
        ISlotGeneric Clone();
    }

    public interface ICascade
    {
        void UntoFall();
        void PushFall(int[][] reels);
        void Strike(List<WinItem> wins);
    }

    public interface ISlotCascade : ICascade, ISlotGeneric
    {
    }

    public class Slotx
    {
        public int Sel { get; set; }
        public double Bet { get; set; }
        public double Gain { get; set; }
        public int FSN { get; set; }
        public int FSR { get; set; }

        public double Cost()
        {
            return Bet * Sel;
        }

        public virtual List<double>? JackFreq(double mrtp) => null;

        public virtual bool FreeMode() => FSR != 0;

        public virtual void Spawn(List<WinItem> wins, double fund, double mrtp) { }

        public virtual void Prepare() { }

        public virtual void Apply(List<WinItem> wins)
        {
            if (FSR != 0)
            {
                Gain += wins.Gain();
                FSN++;
            }
            else
            {
                Gain = wins.Gain();
                FSN = 0;
            }

            if (FSR > 0)
                FSR--;

            foreach (var wi in wins)
                if (wi.FS > 0)
                    FSR += wi.FS;
        }

        public double GetGain() => Gain;

        public void SetGain(double gain)
        {
            Gain = gain;
        }

        public void SetBet(double bet)
        {
            if (bet <= 0) throw new ArgumentException("wrong parameter");
            if (bet == Bet) return;
            if (FSR != 0) throw new InvalidOperationException("feature is disabled");
            Bet = bet;
        }

        public void SetSelNum(int sel, int bln)
        {
            if (sel < 1 || sel > bln) throw new ArgumentException("wrong parameter");
            if (sel == Sel) return;
            if (FSR != 0) throw new InvalidOperationException("feature is disabled");
            Sel = sel;
        }
    }

    public class Grid5x3 : IGrider
    {
        // 5 columns, 3 rows: Grid[x-1][y-1]
        public int[][] Grid { get; set; } = new int[5][]
        {
            new int[3],
            new int[3],
            new int[3],
            new int[3],
            new int[3]
        };

        public (int sx, int sy) Dim() => (5, 3);

        public int At(int x, int y) => Grid[x - 1][y - 1];

        public int LX(int x, int[] line) => Grid[x - 1][line[x - 1] - 1];

        public void SetSym(int x, int y, int sym) => Grid[x - 1][y - 1] = sym;

        public void SetCol(int x, int[] reel, int pos)
        {
            int n = reel.Length;
            pos = (n + pos % n) % n;
            for (int y = 0; y < 3; y++)
            {
                Grid[x - 1][y] = reel[(pos + y) % n];
            }
        }

        public void SpinReels(int[][] reels, ref List<int>? cheat)
        {
            for (int x = 0; x < reels.Length; x++)
            {
                int hit = Random.Shared.Next(reels[x].Length);
                
                if (cheat?.Count > x)
                    hit = cheat[x];

                SetCol(x + 1, reels[x], hit);
            }
            if (cheat?.Count > reels.Length)
                cheat = cheat.Skip(reels.Length).ToList();
        }

        public int SymNum(int sym)
        {
            int n = 0;
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (Grid[x][y] == sym) n++;
                }
            }
            return n;
        }

        public int[][] SymPos(int sym)
        {
            var list = new List<int[]>();
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (Grid[x][y] == sym)
                    {
                        list.Add(new[] { x + 1, y + 1 });
                    }
                }
            }
            return list.ToArray();
        }
    }

    public class Grid5x4 : IGrider
    {
        public int[][] Grid { get; set; } = new int[5][]
        {
            new int[4],
            new int[4],
            new int[4],
            new int[4],
            new int[4]
        };

        public (int sx, int sy) Dim() => (5, 4);

        public int At(int x, int y) => Grid[x - 1][y - 1];

        public int LX(int x, int[] line) => Grid[x - 1][line[x - 1] - 1];

        public void SetSym(int x, int y, int sym) => Grid[x - 1][y - 1] = sym;

        public void SetCol(int x, int[] reel, int pos)
        {
            int n = reel.Length;
            pos = (n + pos % n) % n;
            for (int y = 0; y < 4; y++)
                Grid[x - 1][y] = reel[(pos + y) % n];
        }

        public void SpinReels(int[][] reels, ref List<int>? cheat)
        {
            for (int x = 0; x < reels.Length; x++)
            {
                int hit = Random.Shared.Next(reels[x].Length);
                if (cheat?.Count > x)
                    hit = cheat[x];
                SetCol(x + 1, reels[x], hit);
            }
            if (cheat?.Count > reels.Length)
                cheat = cheat.Skip(reels.Length).ToList();
        }

        public int SymNum(int sym)
        {
            int n = 0;
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (Grid[x][y] == sym) n++;
                }
            }
            return n;
        }

        public int[][] SymPos(int sym)
        {
            var list = new List<int[]>();
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (Grid[x][y] == sym)
                    {
                        list.Add(new[] { x + 1, y + 1 });
                    }
                }
            }
            return list.ToArray();
        }
    }

    public class Cascade5x3 : Grid5x3, ICascade
    {
        public int[][] Hits { get; set; } = new int[5][]
        {
            new int[3],
            new int[3],
            new int[3],
            new int[3],
            new int[3]
        };

        public int[] Seed { get; set; } = new int[5];

        public int CFN { get; set; }

        public new void SetCol(int x, int[] reel, int pos)
        {
            base.SetCol(x, reel, pos);
            Seed[x - 1] = (reel.Length + pos % reel.Length) % reel.Length;
        }

        public new void SpinReels(int[][] reels, ref List<int>? cheat)
        {
            if (CFN > 0)
            {
                PushFall(reels);
            }
            else
            {
                BaseRoll(reels, ref cheat);
            }
        }

        public void BaseRoll(int[][] reels, ref List<int>? cheat)
        {
            for (int x = 0; x < reels.Length; x++)
            {
                int pos = Random.Shared.Next(reels[x].Length);
                
                if (cheat?.Count > x)
                    pos = cheat[x];

                SetCol(x + 1, reels[x], pos);
            }

            if (cheat?.Count > reels.Length)
                cheat = cheat.Skip(reels.Length).ToList();
        }

        public void PushFall(int[][] reels)
        {
            for (int x = 0; x < 5; x++)
            {
                int n = 0;
                for (int y = 0; y < 3; y++)
                {
                    if (Hits[x][y] > 0)
                    {
                        for (int i = 0; i < y; i++)
                        {
                            Grid[x][y - i] = Grid[x][y - i - 1];
                        }
                        n++;
                    }
                }

                Seed[x] -= n;
                int reelLen = reels[x].Length;
                for (int y = 0; y < n; y++)
                {
                    int pos = (reelLen + (Seed[x] + y) % reelLen) % reelLen;
                    Grid[x][y] = reels[x][pos];
                }
            }
        }

        public bool Cascade()
        {
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (Hits[x][y] > 0) return true;
                }
            }
            return false;
        }

        public void UntoFall()
        {
            if (Cascade())
            {
                CFN++;
            }
            else
            {
                CFN = 0;
            }
        }

        public void Strike(List<WinItem> wins)
        {
            for (int x = 0; x < 5; x++)
            {
                Array.Clear(Hits[x], 0, 3);
            }
            foreach (var wi in wins)
            {
                if (wi.XY != null)
                {
                    foreach (var pt in wi.XY)
                    {
                        if (pt != null && pt.Length >= 2 && pt[0] > 0 && pt[1] > 0)
                        {
                            Hits[pt[0] - 1][pt[1] - 1]++;
                        }
                    }
                }
            }
        }
    }

    public static class CascadeExtensions
    {
        public const int FallLimit = 20;

        public static (double sumgain, bool ok) CascadeGain(ISlotGeneric game, List<WinItem> wins, double fund, double mrtp)
        {
            if (wins == null || wins.Count == 0) return (0, true);
            if (game is not ISlotCascade slotCascade) return (0, true);

            var cas = (ISlotCascade)game.Clone();
            cas.Strike(wins);
            var cw = new List<WinItem>();
            double sumgain = 0;

            for (int step = 0; step < FallLimit; step++)
            {
                cas.UntoFall();
                List<int>? noCheat = null;
                cas.SpinReels(cas.GetReels(mrtp), ref noCheat);
                cw.Clear();
                cas.Scanner(cw);
                if (cw.Count == 0) 
                    return (sumgain, true);

                game.Spawn(cw, fund, mrtp);
                sumgain += cw.Gain();
                cas.Strike(cw);
            }
            return (sumgain, false); // exceeded fall limit
        }
    }
}
