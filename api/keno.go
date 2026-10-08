package api

import (
	"encoding/json"
	"log"

	"github.com/gin-gonic/gin"

	cfg "github.com/slotopol/server/config"
	"github.com/slotopol/server/game/keno"
)

func ApiKenoSpin(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		GID   uint64
		Bet   float64
		Sel   []int
		Cheat []int
	}
	var ret struct {
		SID    uint64
		Game   keno.KenoGame
		Wins   keno.Wins
		Wallet float64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}

	var scene *Scene
	if scene, err = GetScene(arg.GID); err != nil {
		Ret404(c, err)
		return
	}
	var game keno.KenoGame
	if game, ok = scene.Game.(keno.KenoGame); !ok {
		Ret403(c, ErrNotKeno)
		return
	}

	var club *Club
	if club, ok = Clubs.Get(scene.CID); !ok {
		Ret500(c, ErrNoClub)
		return
	}

	var user *User
	if user, ok = Users.Get(scene.UID); !ok {
		Ret500(c, ErrNoUser)
		return
	}

	var admin, al = MustAdmin(c, scene.CID)
	if admin.ID != scene.UID && al&ALdealer == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	if arg.Bet != 0 {
		if err = game.SetBet(arg.Bet); err != nil {
			Ret403(c, err)
			return
		}
	}
	if len(arg.Sel) > 0 {
		var bs keno.Bitset
		bs.Pack(arg.Sel)
		if err = game.SetSel(bs); err != nil {
			Ret403(c, err)
			return
		}
	}

	var cost = game.GetBet()

	var props *Props
	if props, ok = user.props.Get(scene.CID); !ok {
		Ret500(c, ErrNoProps)
		return
	}
	if props.Wallet < cost {
		Ret403(c, ErrNoMoney)
		return
	}

	var bank = club.Bank()
	var mrtp = GetRTP(user, club)

	var wins keno.Wins
	var debit float64
	var n = 0
	for {
		n++
		if n > cfg.MaxSpinAttempts {
			Ret500(c, ErrBadBank)
			return
		}
		game.Spin(mrtp, &arg.Cheat)
		if game.Scanner(&wins) != nil {
			continue
		}
		debit = cost - wins.Pay
		if bank+debit >= 0 || (bank < 0 && debit > 0) {
			break
		}
	}

	go BankBat[scene.CID].Put(cfg.XormStorage, scene.UID, debit)

	club.AddBank(debit)
	props.Wallet -= debit

	var sid = SpinCounter.Add(1)
	scene.SID = sid
	var rec = Spinlog{
		ID:     sid,
		GID:    arg.GID,
		MRTP:   mrtp,
		Bet:    cost,
		Gain:   wins.Pay,
		Jack:   0,
		Wallet: props.Wallet,
	}
	var b []byte

	if b, err = json.Marshal(scene.Game); err != nil {
		return
	}
	rec.Game = string(b)

	if b, err = json.Marshal(wins); err != nil {
		return
	}
	rec.Wins = string(b)

	go func() {
		if err = SpinBuf.Put(cfg.XormStorage, rec); err != nil {
			log.Printf("can not write to spin log: %s", err.Error())
		}
	}()

	ret.SID = sid
	ret.Game = game
	ret.Wins = wins
	ret.Wallet = props.Wallet

	RetOk(c, ret)
}
