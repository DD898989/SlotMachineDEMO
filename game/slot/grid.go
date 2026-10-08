package slot

import (
	"math/rand/v2"
)

type Grider interface {
	Dim() (Pos, Pos)
	At(x, y Pos) Sym
	LX(x Pos, line Linex) Sym
	SetSym(x, y Pos, sym Sym)
	SetCol(x Pos, reel []Sym, pos int)
	SpinReels(reels Reelx, cheat *[]int)
	SymNum(sym Sym) (n Pos)
	SymPos(sym Sym) Hitx
}

type Grid5x3 struct {
	Grid [5][3]Sym
}

var _ Grider = (*Grid5x3)(nil)

func (s *Grid5x3) Dim() (Pos, Pos) {
	return 5, 3
}

func (s *Grid5x3) At(x, y Pos) Sym {
	return s.Grid[x-1][y-1]
}

func (s *Grid5x3) LX(x Pos, line Linex) Sym {
	return s.Grid[x-1][line[x-1]-1]
}

func (s *Grid5x3) SetSym(x, y Pos, sym Sym) {
	s.Grid[x-1][y-1] = sym
}

func (s *Grid5x3) SetCol(x Pos, reel []Sym, pos int) {
	var sr = &s.Grid[x-1]
	var n = len(reel)
	pos = (n + pos%n) % n
	for y := range sr {
		sr[y] = reel[(pos+y)%n]
	}
}

func (s *Grid5x3) SpinReels(reels Reelx, cheat *[]int) {
	for x, reel := range reels {
		var hit = rand.N(len(reel))
		if cheat != nil && len(*cheat) > x {
			hit = (*cheat)[x]
		}
		s.SetCol(Pos(x+1), reel, hit)
	}
	if cheat != nil && len(*cheat) > len(reels) {
		*cheat = (*cheat)[len(reels):]
	}
}

func (s *Grid5x3) SymNum(sym Sym) (n Pos) {
	for _, sr := range s.Grid {
		for _, sy := range sr {
			if sy == sym {
				n++
			}
		}
	}
	return

}

func (s *Grid5x3) SymPos(sym Sym) (c Hitx) {
	var i Pos
	for x, sr := range s.Grid {
		for y, sy := range sr {
			if sy == sym {
				c[i][0], c[i][1] = Pos(x+1), Pos(y+1)
				i++
			}
		}
	}
	return
}

type Grid5x4 struct {
	Grid [5][4]Sym
}

var _ Grider = (*Grid5x4)(nil)

func (s *Grid5x4) Dim() (Pos, Pos) {
	return 5, 4
}

func (s *Grid5x4) At(x, y Pos) Sym {
	return s.Grid[x-1][y-1]
}

func (s *Grid5x4) LX(x Pos, line Linex) Sym {
	return s.Grid[x-1][line[x-1]-1]
}

func (s *Grid5x4) SetSym(x, y Pos, sym Sym) {
	s.Grid[x-1][y-1] = sym
}

func (s *Grid5x4) SetCol(x Pos, reel []Sym, pos int) {
	var sr = &s.Grid[x-1]
	var n = len(reel)
	pos = (n + pos%n) % n
	for y := range sr {
		sr[y] = reel[(pos+y)%n]
	}
}

func (s *Grid5x4) SpinReels(reels Reelx, cheat *[]int) {
	for x, reel := range reels {
		var hit = rand.N(len(reel))
		if cheat != nil && len(*cheat) > x {
			hit = (*cheat)[x]
		}
		s.SetCol(Pos(x+1), reel, hit)
	}
	if cheat != nil && len(*cheat) > len(reels) {
		*cheat = (*cheat)[len(reels):]
	}
}

func (s *Grid5x4) SymNum(sym Sym) (n Pos) {
	for _, sr := range s.Grid {
		for _, sy := range sr {
			if sy == sym {
				n++
			}
		}
	}
	return
}

func (s *Grid5x4) SymPos(sym Sym) (c Hitx) {
	var i Pos
	for x, sr := range s.Grid {
		for y, sy := range sr {
			if sy == sym {
				c[i][0], c[i][1] = Pos(x+1), Pos(y+1)
				i++
			}
		}
	}
	return
}
