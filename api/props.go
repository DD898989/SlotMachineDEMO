package api

import (
	"github.com/gin-gonic/gin"
	cfg "github.com/slotopol/server/config"
)

func ApiPropsWalletGet(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		CID uint64
		UID uint64
	}
	var ret struct {
		Wallet float64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}

	var club *Club
	if club, ok = Clubs.Get(arg.CID); !ok {
		Ret404(c, ErrNoClub)
		return
	}
	_ = club

	var user *User
	if user, ok = Users.Get(arg.UID); !ok {
		Ret404(c, ErrNoUser)
		return
	}

	var admin, al = MustAdmin(c, arg.CID)
	if admin != user && al&ALbooker == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	ret.Wallet = user.GetWallet(arg.CID)

	RetOk(c, ret)
}

func ApiPropsWalletAdd(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		CID uint64
		UID uint64
		Sum float64
	}
	var ret struct {
		Wallet float64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}
	if arg.Sum > cfg.AdjunctLimit || arg.Sum < -cfg.AdjunctLimit {
		Ret400(c, ErrTooBig)
		return
	}

	var user *User
	if user, ok = Users.Get(arg.UID); !ok {
		Ret404(c, ErrNoUser)
		return
	}

	var admin, al = MustAdmin(c, arg.CID)
	if al&ALbooker == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	var props *Props
	if props, ok = user.props.Get(arg.CID); !ok {
		Ret500(c, ErrNoProps)
		return
	}
	if props.Wallet+arg.Sum < 0 {
		Ret403(c, ErrNoMoney)
		return
	}

	go BankBat[arg.CID].Add(cfg.XormStorage, arg.UID, admin.ID, props.Wallet+arg.Sum, arg.Sum)

	props.Wallet += arg.Sum

	ret.Wallet = props.Wallet

	RetOk(c, ret)
}

func ApiPropsRtpSet(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		CID  uint64
		UID  uint64
		MRTP float64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}

	var club *Club
	if club, ok = Clubs.Get(arg.CID); !ok {
		Ret404(c, ErrNoClub)
		return
	}
	_ = club

	var user *User
	if user, ok = Users.Get(arg.UID); !ok {
		Ret404(c, ErrNoUser)
		return
	}

	var admin, al = MustAdmin(c, arg.CID)
	if al&ALbooker == 0 {
		Ret403(c, ErrNoAccess)
		return
	}
	_ = admin

	var props *Props
	if props, ok = user.props.Get(arg.CID); !ok {
		Ret500(c, ErrNoProps)
		return
	}

	go BankBat[arg.CID].MRTP(cfg.XormStorage, arg.UID, arg.MRTP)

	props.MRTP = arg.MRTP

	Ret204(c)
}
