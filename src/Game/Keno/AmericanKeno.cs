using ServerMain.Util;

namespace ServerMain.Game.Keno
{
    [Flags]
    public enum KenoState : byte
    {
        Empty = 0,
        Sel = 0x1,
        Hit = 0x2,
        SelHit = Sel | Hit
    }

    public class KenoWins
    {
        public int Sel { get; set; }
        public int Num { get; set; }
        public double Pay { get; set; }
    }

    public interface IKenoGame : Gamble
    {
        void Scanner(KenoWins wins);
        double GetBet();
        void SetSel(Bitset128 sel);
    }

    public class Keno80 : IKenoGame
    {
        public const double InitRTP = 95.0;

        public KenoState[] Grid { get; set; } = new KenoState[80];
        public double Bet { get; set; }
        public Bitset128 Sel { get; set; }

        public virtual void Scanner(KenoWins wins) { }

        public void Spin(double mrtp, ref List<int>? cheat)
        {
            var hits = new int[80];
            for (int i = 0; i < 80; i++)
            {
                hits[i] = i + 1;
            }

            if (cheat != null && cheat.Count > 0)
            {
                var used = new HashSet<int>();
                int cCount = Math.Min(cheat.Count, 20);
                for (int i = 0; i < cCount; i++)
                {
                    hits[i] = cheat[i];
                    used.Add(cheat[i]);
                }
                int fill = cCount;
                for (int i = 1; i <= 80 && fill < 20; i++)
                {
                    if (!used.Contains(i))
                    {
                        hits[fill] = i;
                        fill++;
                    }
                }
                if (cheat.Count > 20)
                {
                    cheat = cheat.Skip(20).ToList();
                }
            }
            else
            {
                // Shuffle first 20 or entire 80
                Random.Shared.Shuffle(hits);
            }

            Array.Clear(Grid, 0, 80);
            foreach (var n in Sel.Bits())
            {
                if (n >= 1 && n <= 80)
                {
                    Grid[n - 1] = KenoState.Sel;
                }
            }
            for (int i = 0; i < 20; i++)
            {
                int idx = hits[i] - 1;
                if (idx >= 0 && idx < 80)
                {
                    Grid[idx] |= KenoState.Hit;
                }
            }
        }

        public double GetBet() => Bet;

        public void SetBet(double bet)
        {
            if (bet <= 0) throw new ArgumentException("wrong parameter");
            Bet = bet;
        }

        public void CheckSel(Bitset128 sel, double[][] paytable)
        {
            int num = sel.Num();
            if (num > paytable.Length - 1) throw new ArgumentException("too many numbers selected, not more than 10 expected");
            bool hasSel = false;
            if (num < paytable.Length)
            {
                foreach (var pay in paytable[num])
                {
                    if (pay > 0)
                    {
                        hasSel = true;
                        break;
                    }
                }
            }
            if (!hasSel) throw new ArgumentException("no pays with this selected numbers");
            foreach (var n in sel.Bits())
            {
                if (n < 1 || n > 80) throw new ArgumentOutOfRangeException("some of given number is out of range 1..80");
            }
            Sel = sel;
        }

        public virtual void SetSel(Bitset128 sel) { }
    }

    public class AmericanKenoGame : Keno80
    {
        public static readonly double[][] Paytable = new double[][]
        {
            new double[] {0},
            new double[] {0, 0},
            new double[] {1, 0, 5},
            new double[] {1, 0, 2, 15},
            new double[] {1, 0, 1, 5, 50},
            new double[] {1, 0, 0, 3, 12, 400},
            new double[] {1, 0, 0, 2, 6, 50, 1000},
            new double[] {1, 0, 0, 1, 2, 25, 300, 3000},
            new double[] {1, 0, 0, 1, 2, 10, 40, 800, 6000},
            new double[] {1, 0, 0, 0, 1, 5, 45, 400, 2000, 8000},
            new double[] {1, 0, 0, 0, 1, 5, 18, 100, 500, 2500, 10000},
        };

        public AmericanKenoGame()
        {
            Bet = 1;
            List<int>? noCheat = null;
            Spin(InitRTP, ref noCheat);
        }

        public override void Scanner(KenoWins wins)
        {
            wins.Sel = 0;
            wins.Num = 0;
            for (int i = 0; i < 80; i++)
            {
                if ((Grid[i] & KenoState.Sel) != 0)
                {
                    wins.Sel++;
                    if ((Grid[i] & KenoState.Hit) != 0)
                    {
                        wins.Num++;
                    }
                }
            }
            double mult = 0;
            if (wins.Sel < Paytable.Length && wins.Num < Paytable[wins.Sel].Length)
            {
                mult = Paytable[wins.Sel][wins.Num];
            }
            wins.Pay = mult * Bet;
        }

        public override void SetSel(Bitset128 sel)
        {
            CheckSel(sel, Paytable);
        }
    }
}
