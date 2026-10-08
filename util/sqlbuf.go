package util

import (
	"sync"
	"time"

	"xorm.io/xorm"
)

type SqlBuf[T any] struct {
	buf []T
	lft time.Time
	mux sync.Mutex
}

func (sb *SqlBuf[T]) insertAll(engine *xorm.Engine) (err error) {
	for i := range sb.buf {
		if _, e := engine.Insert(&sb.buf[i]); e != nil {
			err = e
		}
	}
	sb.buf = sb.buf[:0]
	sb.lft = time.Now()
	return
}

func (sb *SqlBuf[T]) Flush(engine *xorm.Engine) error {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	if len(sb.buf) == 0 {
		return nil
	}
	return sb.insertAll(engine)
}

func (sb *SqlBuf[T]) Put(engine *xorm.Engine, val T) error {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	sb.buf = append(sb.buf, val)
	if len(sb.buf) == cap(sb.buf) {
		return sb.insertAll(engine)
	}
	return nil
}
