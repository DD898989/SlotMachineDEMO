package jokers100

import (
	"github.com/slotopol/server/game/slot"
)

const (
	sn         = 10
	wild, scat = 1, 2
)

var LinePay = [sn][5]float64{
	{},
	{},
	{0, 4, 40, 100, 1000},
	{0, 0, 30, 100, 300},
	{0, 0, 12, 60, 200},
	{0, 0, 12, 60, 160},
	{0, 0, 10, 40, 120},
	{0, 0, 10, 40, 120},
	{0, 0, 6, 30, 80},
	{0, 0, 6, 30, 80},
}

var ScatPay = [5]float64{0, 0, 3, 20, 500}

var BetLines = slot.BetLinesAgt5x4[:]

type Game struct {
	slot.Grid5x4
	slot.Slotx
}

var _ slot.SlotGeneric = (*Game)(nil)

func NewGame(sel int) *Game {
	var g = &Game{
		Slotx: slot.Slotx{
			Sel: sel,
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
			if sx != syml && sx != wild {
				numl = x - 1
				break
			}
		}

		if pay := LinePay[syml-1][numl-1]; pay > 0 {
			*wins = append(*wins, slot.WinItem{
				Pay: g.Bet * pay,
				MP:  1,
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
		var pay = ScatPay[count-1]
		*wins = append(*wins, slot.WinItem{
			Pay: g.Bet * float64(g.Sel) * pay,
			MP:  1,
			Sym: scat,
			Num: count,
			XY:  g.SymPos(scat),
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

func (g *Game) SetSel(sel int) error {
	return g.SetSelNum(sel, len(BetLines))
}
