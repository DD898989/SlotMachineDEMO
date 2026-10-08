namespace ServerMain.Game.Slot.Novomatic
{
    public class BookOfRaGame : Grid5x3, ISlotGeneric
    {
        public const int sn = 10;
        public const int wsc = 10;

        public static readonly double[][] LinePay = new double[sn][]
        {
            new double[] {0, 10, 100, 1000, 5000},
            new double[] {0, 5, 40, 400, 2000},
            new double[] {0, 5, 30, 100, 750},
            new double[] {0, 5, 30, 100, 750},
            new double[] {0, 0, 5, 40, 150},
            new double[] {0, 0, 5, 40, 150},
            new double[] {0, 0, 5, 25, 100},
            new double[] {0, 0, 5, 25, 100},
            new double[] {0, 0, 5, 25, 100},
            new double[] {0, 0, 0, 0, 0},
        };

        public static readonly double[] ScatPay = new double[] { 0, 0, 2, 20, 200 };
        public static readonly int[] ScatFreespin = new int[] { 0, 0, 10, 10, 10 };

        public static readonly int[][] BetLines = ServerMain.Game.Slot.BetLines.BetLinesNvm10;

        public static readonly int[][] ReelsBon = new int[][]
        {
            new int[] {8, 2, 6, 8, 1, 6, 5, 3, 9, 6, 8, 5, 2, 7, 9, 10, 5, 8, 2, 7, 9, 10, 6, 8, 2, 7, 6, 5, 4, 7, 8, 3, 6, 5, 4, 7, 5},
            new int[] {7, 3, 6, 9, 4, 8, 10, 6, 7, 5, 9, 7, 1, 8, 5, 4, 8, 7, 10, 5, 9, 6, 3, 5, 8, 7, 9, 2, 8, 5, 7, 8, 4, 9, 5, 6, 9},
            new int[] {5, 1, 7, 9, 1, 6, 8, 7, 3, 8, 6, 4, 7, 5, 1, 9, 5, 6, 2, 7, 5, 8, 6, 9, 8, 5, 10, 9, 6, 8, 2, 9, 7, 3, 6, 8},
            new int[] {10, 9, 6, 7, 4, 6, 2, 7, 8, 1, 5, 7, 3, 6, 7, 4, 9, 6, 3, 7, 9, 4, 5, 8, 9, 1, 8, 5, 10, 6, 9, 3, 6, 9, 4, 7, 8},
            new int[] {7, 4, 9, 10, 6, 2, 7, 5, 3, 9, 8, 10, 6, 8, 2, 5, 3, 8, 5, 1, 8, 9, 3, 5, 1, 6, 4, 9, 2, 7, 3, 6, 4, 7, 2, 9, 1},
        };

        public static readonly Dictionary<double, int[][]> ReelsMap = new()
        {
            [90.044039] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 8, 9, 2, 8, 7, 5, 9, 4, 7, 8, 9, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {4, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 3, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 3, 7, 6, 3, 9, 5, 4, 8, 9, 7, 6, 2, 9, 8, 7, 3, 9, 5},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 2, 5, 7, 6, 3, 7, 4, 5, 7, 9, 4, 6, 2, 7, 1, 8, 9, 3, 6, 7, 8, 1, 5, 6, 7, 5, 6},
            },
            [92.130383] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 8, 9, 2, 8, 7, 5, 9, 4, 7, 8, 9, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {4, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 3, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 8, 7, 6, 3, 9, 5, 4, 8, 9, 7, 6, 2, 9, 8, 7, 3, 9, 5},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 2, 5, 6, 3, 7, 4, 5, 9, 4, 6, 2, 7, 1, 8, 9, 3, 6, 8, 1, 5, 6, 7, 5, 6},
            },
            [94.265078] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 8, 9, 2, 8, 7, 5, 9, 4, 7, 8, 9, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {7, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 3, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 8, 7, 6, 3, 9, 5, 4, 8, 9, 7, 6, 2, 9, 8, 7, 3, 9, 5},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 2, 5, 6, 3, 7, 4, 5, 9, 4, 6, 2, 7, 1, 8, 9, 3, 6, 8, 1, 5, 6},
            },
            [95.111967] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 8, 9, 2, 8, 7, 5, 9, 4, 7, 8, 9, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {7, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 3, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 8, 7, 6, 3, 9, 5, 4, 8, 9, 7, 6, 2, 9, 8, 7, 3, 9, 5},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 2, 5, 6, 3, 7, 4, 5, 9, 6, 2, 7, 1, 5, 9, 3, 6, 8, 1, 5, 6},
            },
            [96.077825] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 8, 9, 2, 8, 7, 5, 9, 4, 7, 8, 5, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {7, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 3, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 8, 7, 6, 3, 9, 5, 4, 8, 9, 7, 6, 2, 9, 8, 7, 3, 9, 5},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 2, 5, 6, 3, 7, 4, 9, 6, 2, 7, 1, 5, 9, 3, 6, 8, 1, 5, 6},
            },
            [97.009164] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 8, 7, 5, 8, 7, 5, 9, 4, 7, 8, 5, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {7, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 2, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 8, 7, 6, 3, 9, 5, 4, 8, 9, 7, 6, 2, 9, 8, 7, 3, 9, 5},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 2, 6, 3, 7, 4, 9, 6, 2, 7, 1, 5, 9, 3, 6, 8, 1, 7, 6},
            },
            [176.804975] = new int[][]
            {
                new int[] {7, 9, 10, 5, 8, 3, 5, 7, 1, 6, 5, 3, 7, 6, 4, 5, 8, 6, 7, 2, 9, 6, 3, 7, 6, 3, 8, 5, 10, 6, 7, 4, 6, 8, 3, 7, 9, 5, 8, 2, 6, 7, 3, 5, 9, 1, 5, 7, 6, 8, 5},
                new int[] {8, 7, 10, 5, 9, 8, 2, 6, 5, 8, 4, 6, 9, 4, 8, 6, 2, 7, 9, 6, 5, 8, 9, 10, 6, 8, 9, 6, 4, 8, 9, 6, 1, 7, 8, 9, 4, 8, 6, 5, 3, 8, 9, 6, 7, 9},
                new int[] {8, 5, 10, 9, 6, 2, 5, 7, 2, 9, 7, 1, 8, 9, 3, 7, 5, 3, 6, 5, 1, 7, 9, 2, 8, 7, 5, 9, 4, 7, 8, 9, 2, 6, 5, 9, 4, 8, 7, 3, 9, 5, 4, 8, 7, 9},
                new int[] {7, 8, 10, 9, 6, 1, 5, 7, 2, 5, 3, 6, 8, 1, 9, 7, 3, 6, 5, 3, 7, 6, 10, 7, 5, 4, 7, 9, 2, 6, 4, 5, 8, 9, 6, 3, 9, 5, 4, 8, 9},
                new int[] {9, 8, 10, 6, 8, 3, 7, 5, 3, 6, 7, 9, 4, 7, 5, 4, 8, 2, 7, 3, 5, 9, 10, 7, 8, 1, 5, 6, 3, 7, 4, 5, 9, 4, 6, 2, 7, 1, 8, 9, 3, 6, 8, 1, 5, 6},
            },
        };

        public Slotx Slotx { get; set; } = new();
        public int ES { get; set; }

        public BookOfRaGame() : this(10) { }

        public BookOfRaGame(int sel)
        {
            Slotx.Sel = sel;
            Slotx.Bet = 1;
            List<int>? noCheat = null;
            SpinReels(GetReels(WinsExtensions.InitRTP), ref noCheat);
        }

        public ISlotGeneric Clone()
        {
            var clone = new BookOfRaGame(Slotx.Sel)
            {
                ES = this.ES,
                Slotx = new Slotx
                {
                    Sel = Slotx.Sel,
                    Bet = Slotx.Bet,
                    Gain = Slotx.Gain,
                    FSN = Slotx.FSN,
                    FSR = Slotx.FSR
                }
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

        private static int[][] LineES(int[] line, int[][] xye)
        {
            var list = new List<int[]>();
            for (int i = 0; i < xye.Length; i++)
            {
                if (xye[i][0] > 0)
                {
                    list.Add(new[] { xye[i][0], line[xye[i][0] - 1] });
                }
            }
            return list.ToArray();
        }

        public void ScanLined(List<WinItem> wins)
        {
            for (int li = 0; li < Slotx.Sel && li < BetLines.Length; li++)
            {
                var line = BetLines[li];
                int numw = 0;
                int numl = 5;
                int syml = 0;

                for (int x = 1; x <= 5; x++)
                {
                    int sx = LX(x, line);
                    if (sx == wsc)
                    {
                        if (syml == 0)
                        {
                            numw = x;
                        }
                    }
                    else if (syml == 0)
                    {
                        if (sx == ES && numw == 0)
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

                if (numl >= 2 && syml > 0)
                {
                    double payl = LinePay[syml - 1][numl - 1];
                    if (payl > 0)
                    {
                        wins.Add(new WinItem
                        {
                            Pay = Slotx.Bet * payl,
                            MP = 1,
                            Sym = syml,
                            Num = numl,
                            LI = li + 1,
                            XY = line.HitxL(numl)
                        });
                    }
                }
            }

            if (ES == 0) return;
            int nume = SymNum(ES);
            if (nume < 2) return;
            double paye = LinePay[ES - 1][nume - 1];
            if (paye == 0) return;
            int[][] xye = SymPos(ES);
            for (int li = 0; li < Slotx.Sel && li < BetLines.Length; li++)
            {
                var line = BetLines[li];
                wins.Add(new WinItem
                {
                    Pay = Slotx.Bet * paye,
                    MP = 1,
                    Sym = ES,
                    Num = nume,
                    LI = li + 1,
                    XY = LineES(line, xye)
                });
            }
        }

        public void ScanScatters(List<WinItem> wins)
        {
            int count = SymNum(wsc);
            if (count >= 3)
            {
                double pay = ScatPay[count - 1];
                int fs = ScatFreespin[count - 1];
                wins.Add(new WinItem
                {
                    Pay = Slotx.Bet * Slotx.Sel * pay,
                    MP = 1,
                    Sym = wsc,
                    Num = count,
                    XY = SymPos(wsc),
                    FS = fs
                });
            }
        }

        public int[][] GetReels(double mrtp)
        {
            if (Slotx.FSR == 0)
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
            return ReelsBon;
        }

        public void Spin(double mrtp, ref List<int>? cheat)
        {
            SpinReels(GetReels(mrtp), ref cheat);
        }

        public void Prepare()
        {
            if (Slotx.FSR == 0)
                ES = 0;
        }

        public void Apply(List<WinItem> wins)
        {
            Slotx.Apply(wins);
            if (Slotx.FSR > 0 && ES == 0)
                ES = Random.Shared.Next(1, 10);
        }

        public void SetSel(int sel) => Slotx.SetSelNum(sel, BetLines.Length);

        // ISlotGame forwarding
        public double Cost() => Slotx.Cost();
        public List<double>? JackFreq(double mrtp) => Slotx.JackFreq(mrtp);
        public bool FreeMode() => Slotx.FreeMode();
        public void Spawn(List<WinItem> wins, double fund, double mrtp) => Slotx.Spawn(wins, fund, mrtp);
        public double GetGain() => Slotx.GetGain();
        public void SetGain(double gain) => Slotx.SetGain(gain);
        public void SetBet(double bet) => Slotx.SetBet(bet);

        // Property accessors for JSON serialization to match Go embedded struct
        public int Sel { get => Slotx.Sel; set => Slotx.Sel = value; }
        public double Bet { get => Slotx.Bet; set => Slotx.Bet = value; }
        public double Gain { get => Slotx.Gain; set => Slotx.Gain = value; }
        public int FSN { get => Slotx.FSN; set => Slotx.FSN = value; }
        public int FSR { get => Slotx.FSR; set => Slotx.FSR = value; }
    }
}
