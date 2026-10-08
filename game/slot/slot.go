package slot

import (
	"errors"
)

type WinItem struct {
	Pay float64
	MP  float64
	Sym Sym
	Num Pos
	LI  int
	XY  Hitx
	FS  int
	BID int
	Bon any
	JID int
	JR  float64
}

const InitRTP = 95.0

const JackBasis = 250000000

type Wins []WinItem

func (wins *Wins) Reset() {
	*wins = (*wins)[:0]
}

func (wins Wins) Gain() float64 {
	var sum float64
	for _, wi := range wins {
		sum += wi.Pay * wi.MP
	}
	return sum
}

func (wins Wins) Jackpot() float64 {
	var sum float64
	for _, wi := range wins {
		sum += wi.JR
	}
	return sum
}

type SlotGame interface {
	Scanner(*Wins) error
	Cost() float64
	JackFreq(float64) []float64
	FreeMode() bool
	GetReels(float64) Reelx
	Spin(float64, *[]int)
	Spawn(Wins, float64, float64)
	Prepare()
	Apply(Wins)
	GetGain() float64
	SetGain(float64) error
	SetBet(float64) error
	SetSel(int) error
}

type SlotGeneric interface {
	Grider
	SlotGame
	Clone() SlotGeneric
}

var (
	ErrBadParam  = errors.New("wrong parameter")
	ErrNoFeature = errors.New("feature unavailable")
	ErrDisabled  = errors.New("feature is disabled")
)

type Slotx struct {
	Sel int
	Bet float64

	Gain float64

	FSN int

	FSR int
}

func (g *Slotx) Cost() float64 {
	return g.Bet * float64(g.Sel)
}

func (g *Slotx) JackFreq(mrtp float64) []float64 {
	return nil
}

func (g *Slotx) FreeMode() bool {
	return g.FSR != 0
}

func (g *Slotx) Spawn(wins Wins, fund, mrtp float64) {
}

func (g *Slotx) Prepare() {
}

func (g *Slotx) Apply(wins Wins) {
	if g.FSR != 0 {
		g.Gain += wins.Gain()
		g.FSN++
	} else {
		g.Gain = wins.Gain()
		g.FSN = 0
	}

	if g.FSR > 0 {
		g.FSR--
	}
	for _, wi := range wins {
		if wi.FS > 0 {
			g.FSR += wi.FS
		}
	}
}

func (g *Slotx) GetGain() float64 {
	return g.Gain
}

func (g *Slotx) SetGain(gain float64) error {
	g.Gain = gain
	return nil
}

func (g *Slotx) SetBet(bet float64) error {
	if bet <= 0 {
		return ErrBadParam
	}
	if bet == g.Bet {
		return nil
	}
	if g.FSR != 0 {
		return ErrDisabled
	}
	g.Bet = bet
	return nil
}

func (g *Slotx) SetSelNum(sel int, bln int) error {
	if sel < 1 || sel > bln {
		return ErrBadParam
	}
	if sel == g.Sel {
		return nil
	}
	if g.FSR != 0 {
		return ErrDisabled
	}
	g.Sel = sel
	return nil
}
