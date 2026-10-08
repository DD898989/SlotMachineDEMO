package api

import (
	"sync"
	"time"

	"xorm.io/xorm"
)

func SafeTransaction(engine *xorm.Engine, f func(*Session) error) (err error) {
	var session = engine.NewSession()
	defer session.Close()

	if err = session.Begin(); err != nil {
		return
	}
	if err = f(session); err != nil {
		session.Rollback()
		return
	}
	err = session.Commit()
	return
}

type SqlBank struct {
	cid     uint64
	banksum float64
	fundsum float64
	usersum map[uint64]float64
	userrtp map[uint64]float64
	log     []Walletlog
	usercap int
	logsize int
	lft     time.Time
	mux     sync.Mutex
}

func (sb *SqlBank) Init(cid uint64, capacity, logsize int) {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	sb.cid = cid
	sb.banksum = 0
	sb.fundsum = 0
	sb.usersum = make(map[uint64]float64, capacity)
	sb.userrtp = make(map[uint64]float64, capacity)
	sb.log = make([]Walletlog, 0, logsize)
	sb.usercap = capacity
	sb.logsize = logsize
}

func (sb *SqlBank) clear() {
	sb.banksum = 0
	sb.fundsum = 0
	clear(sb.usersum)
	clear(sb.userrtp)
	sb.log = sb.log[:0]
	sb.lft = time.Now()
}

func (sb *SqlBank) transaction(session *Session) (err error) {
	if sb.banksum != 0 || sb.fundsum != 0 {
		var q = session.ID(sb.cid)
		if sb.banksum != 0 {
			q = q.Incr("Bank", sb.banksum)
		}
		if sb.fundsum != 0 {
			q = q.Incr("Fund", sb.fundsum)
		}
		if _, err = q.Update(&ClubData{}); err != nil {
			return
		}
	}
	for uid, sum := range sb.usersum {
		if _, err = session.Where("UID = ? AND CID = ?", uid, sb.cid).Incr("Wallet", sum).Update(&Props{}); err != nil {
			return
		}
	}
	for uid, mrtp := range sb.userrtp {
		if _, err = session.Where("UID = ? AND CID = ?", uid, sb.cid).Cols("MRTP").Update(&Props{MRTP: mrtp}); err != nil {
			return
		}
	}
	if len(sb.log) > 0 {
		if _, err = session.InsertMulti(&sb.log); err != nil {
			return
		}
	}
	return
}

func (sb *SqlBank) Put(engine *xorm.Engine, uid uint64, sum float64) (err error) {
	return sb.PutSlot(engine, uid, sum, 0, -sum)
}

func (sb *SqlBank) PutSlot(engine *xorm.Engine, uid uint64, bankDebit, fundDelta, walletDelta float64) (err error) {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	sb.banksum += bankDebit
	sb.fundsum += fundDelta
	sb.usersum[uid] += walletDelta
	if len(sb.usersum) >= sb.usercap {
		if err = SafeTransaction(engine, sb.transaction); err != nil {
			return
		}
		sb.clear()
	}
	return
}

func (sb *SqlBank) Add(engine *xorm.Engine, uid, aid uint64, wallet, sum float64) (err error) {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	sb.usersum[uid] += sum
	sb.log = append(sb.log, Walletlog{
		CID:    sb.cid,
		UID:    uid,
		AID:    aid,
		Wallet: wallet,
		Sum:    sum,
	})
	if len(sb.usersum) >= sb.usercap || len(sb.log) >= sb.logsize {
		if err = SafeTransaction(engine, sb.transaction); err != nil {
			return
		}
		sb.clear()
	}
	return
}

func (sb *SqlBank) MRTP(engine *xorm.Engine, uid uint64, mrtp float64) (err error) {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	sb.userrtp[uid] = mrtp
	if len(sb.userrtp) >= sb.usercap {
		if err = SafeTransaction(engine, sb.transaction); err != nil {
			return
		}
		sb.clear()
	}
	return
}

func (sb *SqlBank) Flush(engine *xorm.Engine, d time.Duration) (err error) {
	sb.mux.Lock()
	defer sb.mux.Unlock()
	if d == 0 || time.Since(sb.lft) >= d {
		if err = SafeTransaction(engine, sb.transaction); err != nil {
			return
		}
		sb.clear()
	}
	return
}

type SqlStory struct {
	log     []*Story
	logsize int
	lft     time.Time
	mux     sync.Mutex
}

func (ss *SqlStory) clear() {
	ss.log = ss.log[:0]
	ss.lft = time.Now()
}

func (ss *SqlStory) transaction(session *Session) (err error) {
	if len(ss.log) > 0 {
		if _, err = session.InsertMulti(&ss.log); err != nil {
			return
		}
	}
	return
}

func (ss *SqlStory) Join(engine *xorm.Engine, s *Story) (err error) {
	ss.mux.Lock()
	defer ss.mux.Unlock()
	ss.log = append(ss.log, s)
	if len(ss.log) >= ss.logsize {
		if err = SafeTransaction(engine, ss.transaction); err != nil {
			return
		}
		ss.clear()
	}
	return
}

func (ss *SqlStory) Flush(engine *xorm.Engine) (err error) {
	ss.mux.Lock()
	defer ss.mux.Unlock()
	if err = SafeTransaction(engine, ss.transaction); err != nil {
		return
	}
	ss.clear()
	return
}
