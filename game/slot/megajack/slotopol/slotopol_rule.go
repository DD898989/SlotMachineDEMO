package slotopol

import (
	"math"

	"github.com/slotopol/server/game/slot"
)

const (
	sn         = 13
	wild, scat = 11, 1
	bon1, bon2 = 12, 13
	mjj        = 1

	mje9 = 4
	mjm  = 5
)

var LinePay = [sn][5]float64{
	{},
	{0, 2, 5, 15, 100},
	{0, 2, 5, 15, 100},
	{0, 0, 5, 15, 100},
	{0, 0, 5, 15, 100},
	{0, 0, 5, 15, 100},
	{0, 0, 5, 15, 100},
	{0, 0, 5, 15, 100},
	{0, 2, 5, 15, 100},
	{0, 2, 5, 15, 100},
	{0, 10, 100, 2000, 10000},
	{},
	{},
}

var ScatPay = [5]float64{0, 5, 8, 20, 1000}

var LineBonus = [13][5]int{
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, 0},
	{0, 0, 0, 0, mje9},
	{0, 0, 0, 0, mjm},
}

var BetLines = slot.BetLinesMgj[:]

type Game struct {
	slot.Grid5x3
	slot.Slotx
	cheatBonus []int
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
		var mw float64 = 1
		var numw, numl slot.Pos = 0, 5
		var syml slot.Sym
		var x slot.Pos
		for x = 1; x <= 5; x++ {
			var sx = g.LX(x, line)
			if sx == wild {
				if syml == 0 {
					numw = x
				} else if syml == bon1 || syml == bon2 {
					numl = x - 1
					break
				}
				mw = 2
			} else if syml == 0 {
				if numw > 0 && (sx == bon1 || sx == bon2) {
					break
				}
				syml = sx
			} else if sx != syml {
				numl = x - 1
				break
			}
		}

		var payw, payl float64
		if numw >= 2 {
			payw = LinePay[wild-1][numw-1]
		}
		if numl >= 2 && syml > 0 {
			payl = LinePay[syml-1][numl-1]
		}
		if payl*mw > payw {
			*wins = append(*wins, slot.WinItem{
				Pay: g.Bet * payl,
				MP:  mw,
				Sym: syml,
				Num: numl,
				LI:  li + 1,
				XY:  line.HitxL(numl),
			})
		} else if payw > 0 {
			var jid int
			if numw == 5 {
				jid = mjj
			}
			*wins = append(*wins, slot.WinItem{
				Pay: g.Bet * payw,
				MP:  1,
				Sym: wild,
				Num: numw,
				LI:  li + 1,
				XY:  line.HitxL(numw),
				JID: jid,
			})
		} else if syml > 0 && LineBonus[syml-1][numl-1] > 0 {
			*wins = append(*wins, slot.WinItem{
				MP:  1,
				Sym: syml,
				Num: numl,
				LI:  li + 1,
				XY:  line.HitxL(numl),
				BID: LineBonus[syml-1][numl-1],
			})
		}
	}
}

func (g *Game) ScanScatters(wins *slot.Wins) {
	if count := g.SymNum(scat); count >= 2 {
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

func (g *Game) JackFreq(mrtp float64) []float64 {
	var bulk, _ = JackMap.FindClosest(mrtp)
	return []float64{bulk}
}

func (g *Game) GetReels(mrtp float64) slot.Reelx {
	var reels, _ = ReelsMap.FindClosest(mrtp)
	return reels
}

func (g *Game) Spin(mrtp float64, cheat *[]int) {
	if cheat != nil && len(*cheat) > 5 {
		g.cheatBonus = (*cheat)[5:]
	} else {
		g.cheatBonus = nil
	}
	g.SpinReels(g.GetReels(mrtp), cheat)
}

func (g *Game) Spawn(wins slot.Wins, fund, mrtp float64) {
	for i, wi := range wins {
		switch wi.BID {
		case mje9:
			wins[i].Bon, wins[i].Pay = EldoradoSpawn(g.Bet, 9, g.cheatBonus) //輪盤小遊戲 轉9次
		case mjm:
			wins[i].Bon, wins[i].Pay = MonopolySpawn(g.Bet, g.cheatBonus) //大富翁小遊戲
		}
		if wi.JID != 0 {
			var bulk, _ = JackMap.FindClosest(mrtp)
			var jf = min(bulk*g.Bet/slot.JackBasis, 1)
			wins[i].JR = math.Round(jf*fund*100) / 100
		}
	}
}

func (g *Game) SetSel(sel int) error {
	return g.SetSelNum(sel, len(BetLines))
}
