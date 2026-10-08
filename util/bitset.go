package util

import (
	"iter"
	"math/bits"
)

type Bitset128 [2]uint64

func (bs *Bitset128) Num() (count int) {
	for _, u := range bs {
		count += bits.OnesCount64(u)
	}
	return
}

func (bs *Bitset128) Bits() iter.Seq[int] {
	return func(yield func(int) bool) {
		for i, u := range bs {
			var bit uint64 = 1
			for pos := range 64 {
				if u&bit > 0 && !yield(i*64+pos) {
					return
				}
				bit <<= 1
			}
		}
	}
}

func (bs *Bitset128) Pack(idx []int) *Bitset128 {
	for _, n := range idx {
		bs[n/64] |= 1 << (n % 64)
	}
	return bs
}
