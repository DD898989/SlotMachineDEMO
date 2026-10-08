package slotopol

import "math/rand/v2"

type MonCell struct {
	Mult float64
	Jump int
	Dice bool
}

var Monopoly = []MonCell{
	{5, 0, false},  // 索引 0 (格 1): 5x
	{15, 0, false}, // 索引 1 (格 2): 15x
	{15, 0, false}, // 索引 2 (格 3): 15x
	{30, 0, false}, // 索引 3 (格 4): 30x
	{30, 0, false}, // 索引 4 (格 5): 30x
	{10, 0, true},  // 索引 5 (格 6): 10x, Dice=true (骰子倍乘)
	{0, 0, false},  // 索引 6 (格 7): 0x
	{0, 1, false},  // 索引 7 (格 8): Jump=1 (跳轉至格 1)
	{50, 0, false},
	{50, 0, false},
	{0, 0, false},
	{20, 0, true},
	{0, 4, false},
	{80, 0, false},
	{80, 0, false},
	{30, 0, true},
	{0, 7, false},
	{120, 0, false},
	{120, 0, false},
	{200, 0, false},
}

type WinMonCell struct {
	MonCell
	Pos int
	Pay float64
}

func MonopolySpawn(bet float64, cheatBonus []int) (any, float64) {
	var res [7]WinMonCell
	var cash float64

	var pos = 0
	for i := range res {
		var dice = rand.N(6) + 1
		if len(cheatBonus) > 0 {
			dice = cheatBonus[i]
		}
		pos = (pos + dice) % len(Monopoly)
		var mult float64
		if Monopoly[pos].Jump > 0 {
			mult = Monopoly[Monopoly[pos].Jump-1].Mult
		} else {
			mult = Monopoly[pos].Mult
		}
		if Monopoly[pos].Dice {
			mult *= float64(dice)
		}
		res[i].Mult = mult
		res[i].Jump = Monopoly[pos].Jump
		res[i].Dice = Monopoly[pos].Dice
		res[i].Pos = pos + 1
		res[i].Pay = bet * mult
		cash += mult
		if Monopoly[pos].Jump > 0 {
			pos = Monopoly[pos].Jump - 1
		}
	}
	if cash == 0 {
		cash = 5000
	}
	return res, bet * cash
}
