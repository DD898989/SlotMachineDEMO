package americankeno

import (
	"github.com/slotopol/server/game/keno"
)

var Paytable = keno.Paytable{
	{0},
	{0, 0},
	{1, 0, 5},
	{1, 0, 2, 15},
	{1, 0, 1, 5, 50},
	{1, 0, 0, 3, 12, 400},
	{1, 0, 0, 2, 6, 50, 1000},
	{1, 0, 0, 1, 2, 25, 300, 3000},
	{1, 0, 0, 1, 2, 10, 40, 800, 6000},
	{1, 0, 0, 0, 1, 5, 45, 400, 2000, 8000},
	{1, 0, 0, 0, 1, 5, 18, 100, 500, 2500, 10000},
}

type Game struct {
	keno.Keno80
}

var _ keno.KenoGame = (*Game)(nil)

func NewGame() *Game {
	var g = &Game{
		Keno80: keno.Keno80{
			Bet: 1,
		},
	}
	g.Spin(keno.InitRTP, nil)
	return g
}

func (g *Game) Scanner(wins *keno.Wins) error {
	return Paytable.Scanner(&g.Grid, wins, g.Bet)
}

func (g *Game) SetSel(sel keno.Bitset) error {
	return g.CheckSel(sel, &Paytable)
}
