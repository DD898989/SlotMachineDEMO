package slot

import (
	"math"
)

func ReelAt(reel []Sym, pos int) Sym {
	var n = len(reel)
	return reel[(n+pos%n)%n]
}

type Reelx [][]Sym

type ReelsMap[T any] map[float64]T

func (m ReelsMap[T]) FindClosest(mrtp float64) (val T, rtp float64) {
	rtp = -1000
	for p, v := range m {
		if math.Abs(mrtp-p) < math.Abs(mrtp-rtp) {
			val, rtp = v, p
		}
	}
	return
}
