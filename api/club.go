package api

import (
	"github.com/gin-gonic/gin"
	cfg "github.com/slotopol/server/config"
)

func ApiClubInfo(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		CID uint64
	}
	var ret struct {
		ClubData
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}

	var club *Club
	if club, ok = Clubs.Get(arg.CID); !ok {
		Ret500(c, ErrNoClub)
		return
	}

	var _, al = MustAdmin(c, arg.CID)
	if al&ALmaster == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	ret.ClubData = club.Get()

	RetOk(c, ret)
}

func ApiClubCashin(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		CID     uint64
		BankSum float64
		FundSum float64
		LockSum float64
	}
	var ret struct {
		BID  uint64
		Bank float64
		Fund float64
		Lock float64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}
	if arg.BankSum == 0 && arg.FundSum == 0 && arg.LockSum == 0 {
		Ret400(c, ErrNoAddSum)
		return
	}

	var club *Club
	if club, ok = Clubs.Get(arg.CID); !ok {
		Ret500(c, ErrNoClub)
		return
	}

	var _, al = MustAdmin(c, arg.CID)
	if al&ALmaster == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	var bank, fund, lock = club.GetCash()
	bank += arg.BankSum
	fund += arg.FundSum
	lock += arg.LockSum

	if bank < 0 {
		Ret403(c, ErrBankOut)
		return
	}
	if fund < 0 {
		Ret403(c, ErrFundOut)
		return
	}
	if lock < 0 {
		Ret403(c, ErrLockOut)
		return
	}

	var rec = Banklog{
		CID:     club.data.ID,
		Bank:    bank,
		Fund:    fund,
		Lock:    lock,
		BankSum: arg.BankSum,
		FundSum: arg.FundSum,
		LockSum: arg.LockSum,
	}
	if err = SafeTransaction(cfg.XormStorage, func(session *Session) (err error) {
		if _, err = session.ID(club.data.ID).
			Incr("Bank", arg.BankSum).
			Incr("Fund", arg.FundSum).
			Incr("Lock", arg.LockSum).
			Update(&ClubData{}); err != nil {
			Ret500(c, err)
			return
		}
		if _, err = session.Insert(&rec); err != nil {
			Ret500(c, err)
			return
		}
		return
	}); err != nil {
		return
	}

	club.AddCash(arg.BankSum, arg.FundSum, arg.LockSum)

	ret.BID = rec.ID
	ret.Bank = bank
	ret.Fund = fund
	ret.Lock = lock

	RetOk(c, ret)
}
