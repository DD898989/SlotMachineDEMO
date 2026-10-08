package gonzosquest

import (
	"github.com/slotopol/server/game/slot"
)

const (
	sn         = 9
	wild, scat = 1, 2
)

var LinePay = [sn][5]float64{
	{},
	{},
	{0, 0, 50, 250, 2500},
	{0, 0, 20, 100, 1000},
	{0, 0, 15, 50, 500},
	{0, 0, 10, 25, 200},
	{0, 0, 5, 20, 100},
	{0, 0, 4, 15, 75},
	{0, 0, 3, 10, 50},
}

var BetLines = slot.BetLinesNetEnt5x3[:]

type Game struct {
	slot.Cascade5x3
	slot.Slotx
}

var _ slot.SlotCascade = (*Game)(nil)

func NewGame() *Game {
	var g = &Game{
		Slotx: slot.Slotx{
			Sel: 20,
			Bet: 1,
		},
	}
	g.SpinReels(g.GetReels(slot.InitRTP), nil)
	return g
}

func (g *Game) Clone() slot.SlotGeneric {
	var clone = *g
	return &clone
}

func (g *Game) FreeMode() bool {
	return g.FSR != 0 || g.Cascade()
}

func (g *Game) Scanner(wins *slot.Wins) error {
	g.ScanLined(wins)
	g.ScanScatters(wins)
	return nil
}

func (g *Game) ScanLined(wins *slot.Wins) {
	for li, line := range BetLines[:g.Sel] {
		var numl slot.Pos = 5
		var syml = g.LX(1, line)
		var x slot.Pos
		for x = 2; x <= 5; x++ {
			var sx = g.LX(x, line)
			if sx == wild {
				continue
			} else if sx != syml {
				numl = x - 1
				break
			}
		}
		if numl >= 3 && syml > scat {
			var fm = float64(min(g.CFN+1, 5))
			var mm float64 = 1
			if g.FSR > 0 {
				mm = 3
			}
			var pay = LinePay[syml-1][numl-1]
			*wins = append(*wins, slot.WinItem{
				Pay: g.Bet * pay,
				MP:  fm * mm,
				Sym: syml,
				Num: numl,
				LI:  li + 1,
				XY:  line.HitxL(numl),
			})
		}
	}
}

func (g *Game) ScanScatters(wins *slot.Wins) {
	if count := g.SymNum(scat); count >= 3 {
		*wins = append(*wins, slot.WinItem{
			Sym: scat,
			Num: count,
			XY:  g.SymPos(scat),
			FS:  10,
		})
	}
}

func (g *Game) GetReels(mrtp float64) slot.Reelx {
	var reels, _ = ReelsMap.FindClosest(mrtp)
	return reels
}

func (g *Game) Spin(mrtp float64, cheat *[]int) {
	g.SpinReels(g.GetReels(mrtp), cheat)
}

func (g *Game) Prepare() {
	g.UntoFall()
}

func (g *Game) Apply(wins slot.Wins) {
	g.Slotx.Apply(wins)
	g.Strike(wins)
}

func (g *Game) SetSel(sel int) error {
	return slot.ErrNoFeature
}
