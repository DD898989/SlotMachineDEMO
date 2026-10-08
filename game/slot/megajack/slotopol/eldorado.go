package slotopol

import "math/rand/v2"

var Eldorado = []float64{
	1000,
	10,
	25,
	100,
	10,
	50,
	300,
	10,
	50,
	200,
	10,
	25,
	100,
	25,
	10,
	100,
	10,
	50,
	10,
	25,
}

type WinElSeg struct {
	Pos  int
	Pay  float64
	Mult float64
}

func EldoradoSpawn(bet float64, spins int, cheatBonus []int) (any, float64) {
	var res = make([]WinElSeg, spins)
	var pay float64
	for i := range res {
		var n = rand.N(len(Eldorado))
		if len(cheatBonus) > 0 {
			n = cheatBonus[i]
		}
		res[i].Pos = n + 1
		res[i].Pay = bet * Eldorado[n]
		res[i].Mult = Eldorado[n]
		pay += res[i].Pay
	}
	return res, pay
}
