package keno

import (
	"errors"
	"math/rand/v2"

	"github.com/slotopol/server/util"
)

type Paytable [11][11]float64

func (kp *Paytable) HasSel(sel int) bool {
	for _, pay := range kp[sel] {
		if pay > 0 {
			return true
		}
	}
	return false
}

func (kp *Paytable) Scanner(grid *Grid, wins *Wins, bet float64) error {
	wins.Sel = 0
	wins.Num = 0
	for i := range 80 {
		if grid[i]&KSsel > 0 {
			wins.Sel++
			if grid[i]&KShit > 0 {
				wins.Num++
			}
		}
	}
	wins.Pay = kp[wins.Sel][wins.Num] * bet
	return nil
}

type KS byte

const (
	KSempty  KS = 0
	KSsel    KS = 0x1
	KShit    KS = 0x2
	KSselhit KS = KSsel | KShit
)

const InitRTP = 95.0

type Grid [80]KS

type Bitset = util.Bitset128

type Wins struct {
	Sel int
	Num int
	Pay float64
}

type KenoGame interface {
	Scanner(*Wins) error
	Spin(float64, *[]int)
	GetBet() float64
	SetBet(float64) error
	SetSel(Bitset) error
}

var (
	ErrBadParam      = errors.New("wrong parameter")
	ErrKenoNotEnough = errors.New("no pays with this selected numbers")
	ErrKenoTooMany   = errors.New("too many numbers selected, not more than 10 expected")
	ErrKenoOutRange  = errors.New("some of given number is out of range 1..80")
)

type Keno80 struct {
	Grid Grid
	Bet  float64
	Sel  Bitset
}

func (g *Keno80) Spin(_ float64, cheat *[]int) {
	var hits [80]int
	for i := range 80 {
		hits[i] = i + 1
	}
	if cheat != nil && len(*cheat) > 0 {
		var used = make(map[int]bool)
		for i, c := range *cheat {
			if i >= 20 {
				break
			}
			hits[i] = c
			used[c] = true
		}
		var fill = len(*cheat)
		if fill > 20 {
			fill = 20
		}
		for i := 1; i <= 80 && fill < 20; i++ {
			if !used[i] {
				hits[fill] = i
				fill++
			}
		}
		if len(*cheat) > 20 {
			*cheat = (*cheat)[20:]
		}
	} else {
		rand.Shuffle(80, func(i, j int) {
			hits[i], hits[j] = hits[j], hits[i]
		})
	}

	clear(g.Grid[:])
	for n := range g.Sel.Bits() {
		g.Grid[n-1] = KSsel
	}
	for i := range 20 {
		g.Grid[hits[i]-1] |= KShit
	}
}

func (g *Keno80) GetBet() float64 {
	return g.Bet
}

func (g *Keno80) SetBet(bet float64) error {
	if bet <= 0 {
		return ErrBadParam
	}
	g.Bet = bet
	return nil
}

func (g *Keno80) CheckSel(sel Bitset, kp *Paytable) error {
	if sel.Num() > len(kp)-1 {
		return ErrKenoTooMany
	}
	if !kp.HasSel(sel.Num()) {
		return ErrKenoNotEnough
	}
	for n := range sel.Bits() {
		if n < 1 || n > 80 {
			return ErrKenoOutRange
		}
	}
	g.Sel = sel
	return nil
}
