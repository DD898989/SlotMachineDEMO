package slot

import (
	"errors"
	"math/rand/v2"
)

const FallLimit = 20

var ErrAvalanche = errors.New("too many cascading falls")

type Cascade interface {
	UntoFall()
	PushFall(reels Reelx)
	Strike(wins Wins)
}

type SlotCascade interface {
	Cascade
	SlotGeneric
}

type Cascade5x3 struct {
	Grid5x3
	Hits [5][3]Pos
	Seed [5]int

	CFN int
}

var _ Cascade = (*Cascade5x3)(nil)

func (s *Cascade5x3) SetCol(x Pos, reel []Sym, pos int) {
	var sr = &s.Grid[x-1]
	var n = len(reel)
	pos = (n + pos%n) % n
	for y := range sr {
		sr[y] = reel[(pos+y)%n]
	}
	s.Seed[x-1] = pos
}

func (s *Cascade5x3) SpinReels(reels Reelx, cheat *[]int) {
	if s.CFN > 0 {
		s.PushFall(reels)
	} else {
		s.BaseRoll(reels, cheat)
	}
}

func (s *Cascade5x3) BaseRoll(reels Reelx, cheat *[]int) {
	for x, reel := range reels {
		var pos = rand.N(len(reel))
		if cheat != nil && len(*cheat) > x {
			pos = (*cheat)[x]
		}
		s.SetCol(Pos(x+1), reel, pos)
	}
	if cheat != nil && len(*cheat) > len(reels) {
		*cheat = (*cheat)[len(reels):]
	}
}

func (s *Cascade5x3) PushFall(reels Reelx) {
	for x, hr := range s.Hits {
		var sr = &s.Grid[x]

		var n = 0
		// 第一階段：現有圖標向下墜落遞補
		for y, h := range hr {
			if h > 0 { // h > 0 代表這個位置中獎被消除了
				for i := range y {
					sr[y-i] = sr[y-i-1] // 將當前被消除格子上方所有的圖標，逐一往下移一格
				}
				n++ // 累計該直行總共消除了幾個格子
			}
		}

		// 第二階段：從滾輪上方補入新圖標
		s.Seed[x] -= n
		for y := range n {
			sr[y] = ReelAt(reels[x], s.Seed[x]+y)
		}
	}
}

func (s *Cascade5x3) Cascade() bool {
	for _, hr := range s.Hits {
		for _, h := range hr {
			if h > 0 {
				return true
			}
		}
	}
	return false
}

func (s *Cascade5x3) UntoFall() {
	if s.Cascade() {
		s.CFN++
	} else {
		s.CFN = 0
	}
}

func (s *Cascade5x3) Strike(wins Wins) {
	clear(s.Hits[:])
	for _, wi := range wins {
		for i := 0; wi.XY[i][0] > 0; i++ {
			s.Hits[wi.XY[i][0]-1][wi.XY[i][1]-1]++
		}
	}
}

func CascadeGain(game SlotGeneric, wins Wins, fund, mrtp float64) (sumgain float64, err error) {
	if len(wins) == 0 {
		return
	}
	if _, ok := game.(SlotCascade); !ok {
		return
	}
	var cas = game.Clone().(SlotCascade)
	cas.Strike(wins)
	var cw Wins
	for range FallLimit {
		cas.UntoFall()
		cas.SpinReels(cas.GetReels(mrtp), nil)
		if err = cas.Scanner(&cw); err != nil {
			return
		}
		if len(cw) == 0 {
			return
		}
		game.Spawn(cw, fund, mrtp)
		sumgain += cw.Gain()
		cas.Strike(cw)
		cw.Reset()
	}
	err = ErrAvalanche
	return
}
