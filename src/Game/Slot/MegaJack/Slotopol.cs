namespace ServerMain.Game.Slot.MegaJack
{
    public struct MonCell
    {
        public double Mult { get; set; }
        public int Jump { get; set; }
        public bool Dice { get; set; }

        public MonCell(double mult, int jump, bool dice)
        {
            Mult = mult;
            Jump = jump;
            Dice = dice;
        }
    }

    public struct WinMonCell
    {
        public double Mult { get; set; }
        public int Jump { get; set; }
        public bool Dice { get; set; }
        public int Pos { get; set; }
        public double Pay { get; set; }
    }

    public struct WinElSeg
    {
        public int Pos { get; set; }
        public double Pay { get; set; }
        public double Mult { get; set; }
    }

    public class SlotopolGame : Grid5x3, ISlotGeneric
    {
        public const int sn = 13;
        public const int wild = 11;
        public const int scat = 1;
        public const int bon1 = 12;
        public const int bon2 = 13;
        public const int mjj = 1;
        public const int mje9 = 4;
        public const int mjm = 5;

        public static readonly double[][] LinePay = new double[sn][]
        {
            new double[] {0, 0, 0, 0, 0},
            new double[] {0, 2, 5, 15, 100},
            new double[] {0, 2, 5, 15, 100},
            new double[] {0, 0, 5, 15, 100},
            new double[] {0, 0, 5, 15, 100},
            new double[] {0, 0, 5, 15, 100},
            new double[] {0, 0, 5, 15, 100},
            new double[] {0, 0, 5, 15, 100},
            new double[] {0, 2, 5, 15, 100},
            new double[] {0, 2, 5, 15, 100},
            new double[] {0, 10, 100, 2000, 10000},
            new double[] {0, 0, 0, 0, 0},
            new double[] {0, 0, 0, 0, 0},
        };

        public static readonly double[] ScatPay = new double[] { 0, 5, 8, 20, 1000 };

        public static readonly int[][] LineBonus = new int[13][]
        {
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, 0},
            new int[] {0, 0, 0, 0, mje9},
            new int[] {0, 0, 0, 0, mjm},
        };

        public static readonly int[][] BetLines = ServerMain.Game.Slot.BetLines.BetLinesMgj;

        public static readonly Dictionary<double, double> JackMap = new()
        {
            [99.760556] = 1048576
        };

        public static readonly Dictionary<double, int[][]> ReelsMap = new()
        {
            [99.760556] = new int[][]
            {
                new int[] {13, 1, 5, 12, 13, 11, 12, 11, 13, 8, 2, 12, 13, 3, 4, 6, 13, 2, 5, 10, 13, 9, 7, 8, 13, 10, 7, 9, 13, 3, 4, 6},
                new int[] {9, 5, 10, 13, 9, 6, 3, 4, 13, 2, 12, 8, 12, 13, 11, 12, 11, 13, 5, 7, 10, 6, 3, 4, 13, 2, 12, 8, 13, 7, 1, 12},
                new int[] {12, 13, 11, 12, 11, 13, 5, 10, 9, 7, 1, 12, 13, 3, 8, 6, 12, 13, 8, 4, 12, 2, 5, 10, 13, 7, 2, 13, 6, 3, 4, 9},
                new int[] {12, 1, 2, 13, 6, 5, 12, 4, 8, 12, 13, 3, 10, 9, 7, 13, 11, 11, 11, 11, 13, 5, 12, 9, 8, 6, 13, 3, 10, 2, 7, 4},
                new int[] {13, 11, 13, 12, 6, 4, 12, 3, 2, 5, 12, 10, 7, 12, 8, 1, 9, 12, 8, 9, 12, 4, 3, 12, 2, 5, 12, 10, 7, 13, 12, 6},
            }
        };

        public static readonly MonCell[] Monopoly = new MonCell[]
        {
            new MonCell(5, 0, false),   // 0
            new MonCell(15, 0, false),  // 1
            new MonCell(15, 0, false),  // 2
            new MonCell(30, 0, false),  // 3
            new MonCell(30, 0, false),  // 4
            new MonCell(10, 0, true),   // 5
            new MonCell(0, 0, false),   // 6
            new MonCell(0, 1, false),   // 7
            new MonCell(50, 0, false),  // 8
            new MonCell(50, 0, false),  // 9
            new MonCell(0, 0, false),   // 10
            new MonCell(20, 0, true),   // 11
            new MonCell(0, 4, false),   // 12
            new MonCell(80, 0, false),  // 13
            new MonCell(80, 0, false),  // 14
            new MonCell(30, 0, true),   // 15
            new MonCell(0, 7, false),   // 16
            new MonCell(120, 0, false), // 17
            new MonCell(120, 0, false), // 18
            new MonCell(200, 0, false), // 19
        };

        public static readonly double[] Eldorado = new double[]
        {
            1000, 10, 25, 100, 10, 50, 300, 10, 50, 200, 10, 25, 100, 25, 10, 100, 10, 50, 10, 25
        };

        public Slotx Slotx { get; set; } = new();
        private List<int>? _cheatBonus;

        public SlotopolGame() : this(21) { }

        public SlotopolGame(int sel)
        {
            Slotx.Sel = sel;
            Slotx.Bet = 1;
            List<int>? noCheat = null;
            SpinReels(GetReels(WinsExtensions.InitRTP), ref noCheat);
        }

        public ISlotGeneric Clone()
        {
            var clone = new SlotopolGame(Slotx.Sel)
            {
                Slotx = new Slotx
                {
                    Sel = Slotx.Sel,
                    Bet = Slotx.Bet,
                    Gain = Slotx.Gain,
                    FSN = Slotx.FSN,
                    FSR = Slotx.FSR
                },
                _cheatBonus = this._cheatBonus != null ? new List<int>(this._cheatBonus) : null
            };
            for (int x = 0; x < 5; x++)
            {
                Array.Copy(Grid[x], clone.Grid[x], 3);
            }
            return clone;
        }

        public void Scanner(List<WinItem> wins)
        {
            ScanLined(wins);
            ScanScatters(wins);
        }

        public void ScanLined(List<WinItem> wins)
        {
            for (int li = 0; li < Slotx.Sel && li < BetLines.Length; li++)
            {
                var line = BetLines[li];
                double mw = 1;
                int numw = 0;
                int numl = 5;
                int syml = 0;

                for (int x = 1; x <= 5; x++)
                {
                    int sx = LX(x, line);
                    if (sx == wild)
                    {
                        if (syml == 0)
                        {
                            numw = x;
                        }
                        else if (syml == bon1 || syml == bon2)
                        {
                            numl = x - 1;
                            break;
                        }
                        mw = 2;
                    }
                    else if (syml == 0)
                    {
                        if (numw > 0 && (sx == bon1 || sx == bon2))
                        {
                            break;
                        }
                        syml = sx;
                    }
                    else if (sx != syml)
                    {
                        numl = x - 1;
                        break;
                    }
                }

                double payw = 0;
                double payl = 0;
                if (numw >= 2)
                {
                    payw = LinePay[wild - 1][numw - 1];
                }
                if (numl >= 2 && syml > 0)
                {
                    payl = LinePay[syml - 1][numl - 1];
                }

                if (payl * mw > payw)
                {
                    wins.Add(new WinItem
                    {
                        Pay = Slotx.Bet * payl,
                        MP = mw,
                        Sym = syml,
                        Num = numl,
                        LI = li + 1,
                        XY = line.HitxL(numl)
                    });
                }
                else if (payw > 0)
                {
                    int jid = (numw == 5) ? mjj : 0;
                    wins.Add(new WinItem
                    {
                        Pay = Slotx.Bet * payw,
                        MP = 1,
                        Sym = wild,
                        Num = numw,
                        LI = li + 1,
                        XY = line.HitxL(numw),
                        JID = jid
                    });
                }
                else if (syml > 0 && LineBonus[syml - 1][numl - 1] > 0)
                {
                    wins.Add(new WinItem
                    {
                        MP = 1,
                        Sym = syml,
                        Num = numl,
                        LI = li + 1,
                        XY = line.HitxL(numl),
                        BID = LineBonus[syml - 1][numl - 1]
                    });
                }
            }
        }

        public void ScanScatters(List<WinItem> wins)
        {
            int count = SymNum(scat);
            if (count >= 2)
            {
                double pay = ScatPay[count - 1];
                wins.Add(new WinItem
                {
                    Pay = Slotx.Bet * Slotx.Sel * pay,
                    MP = 1,
                    Sym = scat,
                    Num = count,
                    XY = SymPos(scat)
                });
            }
        }

        public bool FreeMode() => Slotx.FreeMode();

        public List<double>? JackFreq(double mrtp)
        {
            double closest = -1000;
            double bulk = 0;
            foreach (var (p, v) in JackMap)
            {
                if (Math.Abs(mrtp - p) < Math.Abs(mrtp - closest))
                {
                    closest = p;
                    bulk = v;
                }
            }
            return new List<double> { bulk };
        }

        public int[][] GetReels(double mrtp)
        {
            double closest = -1000;
            int[][]? res = null;
            foreach (var (p, v) in ReelsMap)
            {
                if (Math.Abs(mrtp - p) < Math.Abs(mrtp - closest))
                {
                    closest = p;
                    res = v;
                }
            }
            return res!;
        }

        public void Spin(double mrtp, ref List<int>? cheat)
        {
            if (cheat != null && cheat.Count > 5)
            {
                _cheatBonus = cheat.Skip(5).ToList();
            }
            else
            {
                _cheatBonus = null;
            }
            SpinReels(GetReels(mrtp), ref cheat);
        }

        public static (object bon, double pay) MonopolySpawn(double bet, List<int>? cheatBonus)
        {
            var res = new WinMonCell[7];
            double cash = 0;
            int pos = 0;

            for (int i = 0; i < 7; i++)
            {
                int dice = Random.Shared.Next(1, 7);
                
                if (cheatBonus?.Count > i)
                    dice = cheatBonus[i];

                pos = (pos + dice) % Monopoly.Length;
                double mult;
                if (Monopoly[pos].Jump > 0)
                {
                    mult = Monopoly[Monopoly[pos].Jump - 1].Mult;
                }
                else
                {
                    mult = Monopoly[pos].Mult;
                }
                if (Monopoly[pos].Dice)
                {
                    mult *= dice;
                }
                res[i].Mult = mult;
                res[i].Jump = Monopoly[pos].Jump;
                res[i].Dice = Monopoly[pos].Dice;
                res[i].Pos = pos + 1;
                res[i].Pay = bet * mult;
                cash += mult;
                if (Monopoly[pos].Jump > 0)
                    pos = Monopoly[pos].Jump - 1;
            }

            if (cash == 0)
                cash = 5000;

            return (res, bet * cash);
        }

        public static (object bon, double pay) EldoradoSpawn(double bet, int spins, List<int>? cheatBonus)
        {
            var res = new WinElSeg[spins];
            double pay = 0;
            for (int i = 0; i < spins; i++)
            {
                int n = Random.Shared.Next(Eldorado.Length);
                if (cheatBonus?.Count > i)
                    n = cheatBonus[i];
                res[i].Pos = n + 1;
                res[i].Pay = bet * Eldorado[n];
                res[i].Mult = Eldorado[n];
                pay += res[i].Pay;
            }
            return (res, pay);
        }

        public void Spawn(List<WinItem> wins, double fund, double mrtp)
        {
            for (int i = 0; i < wins.Count; i++)
            {
                var wi = wins[i];
                switch (wi.BID)
                {
                    case mje9:
                        var (ebon, epay) = EldoradoSpawn(Slotx.Bet, 9, _cheatBonus);
                        wi.Bon = ebon;
                        wi.Pay = epay;
                        break;
                    case mjm:
                        var (mbon, mpay) = MonopolySpawn(Slotx.Bet, _cheatBonus);
                        wi.Bon = mbon;
                        wi.Pay = mpay;
                        break;
                }
                if (wi.JID != 0)
                {
                    double bulk = 0;
                    double closest = -1000;
                    foreach (var (p, v) in JackMap)
                    {
                        if (Math.Abs(mrtp - p) < Math.Abs(mrtp - closest))
                        {
                            closest = p;
                            bulk = v;
                        }
                    }
                    double jf = Math.Min(bulk * Slotx.Bet / WinsExtensions.JackBasis, 1.0);
                    wi.JR = Math.Round(jf * fund * 100.0, MidpointRounding.AwayFromZero) / 100.0;
                }
                wins[i] = wi;
            }
        }

        public void SetSel(int sel) => Slotx.SetSelNum(sel, BetLines.Length);

        public double Cost() => Slotx.Cost();
        public void Prepare() => Slotx.Prepare();
        public void Apply(List<WinItem> wins) => Slotx.Apply(wins);
        public double GetGain() => Slotx.GetGain();
        public void SetGain(double gain) => Slotx.SetGain(gain);
        public void SetBet(double bet) => Slotx.SetBet(bet);

        public int Sel { get => Slotx.Sel; set => Slotx.Sel = value; }
        public double Bet { get => Slotx.Bet; set => Slotx.Bet = value; }
        public double Gain { get => Slotx.Gain; set => Slotx.Gain = value; }
        public int FSN { get => Slotx.FSN; set => Slotx.FSN = value; }
        public int FSR { get => Slotx.FSR; set => Slotx.FSR = value; }
    }
}
