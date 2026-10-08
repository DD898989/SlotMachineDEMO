package util

import (
	"iter"
	"sync"
)

type kvpair[K comparable, T any] struct {
	key K
	val T
}

type RWMap[K comparable, T any] struct {
	m   map[K]T
	mux sync.RWMutex
}

func (rwm *RWMap[K, T]) Init(capacity int) {
	rwm.mux.Lock()
	defer rwm.mux.Unlock()
	rwm.m = make(map[K]T, capacity)
}

func (rwm *RWMap[K, T]) Len() int {
	rwm.mux.RLock()
	defer rwm.mux.RUnlock()
	return len(rwm.m)
}

func (rwm *RWMap[K, T]) Has(key K) (ok bool) {
	rwm.mux.RLock()
	defer rwm.mux.RUnlock()
	_, ok = rwm.m[key]
	return
}

func (rwm *RWMap[K, T]) Get(key K) (ret T, ok bool) {
	rwm.mux.RLock()
	defer rwm.mux.RUnlock()
	ret, ok = rwm.m[key]
	return
}

func (rwm *RWMap[K, T]) Set(key K, val T) {
	rwm.mux.Lock()
	defer rwm.mux.Unlock()
	rwm.m[key] = val
}

func (rwm *RWMap[K, T]) Items() iter.Seq2[K, T] {
	return func(yield func(K, T) bool) {
		var buf []kvpair[K, T]
		func() {
			rwm.mux.RLock()
			defer rwm.mux.RUnlock()
			buf = make([]kvpair[K, T], len(rwm.m))
			var i int
			for k, v := range rwm.m {
				buf[i].key, buf[i].val = k, v
				i++
			}
		}()
		for _, pair := range buf {
			if !yield(pair.key, pair.val) {
				return
			}
		}
	}
}
