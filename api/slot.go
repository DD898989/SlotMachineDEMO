package api

import (
	"encoding/json"
	"errors"
	"log"
	"math"
	"math/rand/v2"

	"github.com/gin-gonic/gin"

	cfg "github.com/slotopol/server/config"
	coregame "github.com/slotopol/server/game"
	"github.com/slotopol/server/game/slot"
)

func ApiSlotSelSet(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		GID uint64
		Sel int
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
	var game slot.SlotGame
	if game, ok = scene.Game.(slot.SlotGame); !ok {
		Ret403(c, ErrNotSlot)
		return
	}

	var admin, al = MustAdmin(c, scene.CID)
	if admin.ID != scene.UID && al&ALdealer == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	if err = game.SetSel(arg.Sel); err != nil {
		Ret403(c, err)
		return
	}

	Ret204(c)
}

func ApiSlotSpin(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		GID   uint64
		Bet   float64
		Sel   int
		Cheat []int
	}
	var ret struct {
		SID    uint64
		Game   slot.SlotGeneric
		Wins   slot.Wins
		Wallet float64
		JpFund float64
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
	var game slot.SlotGeneric
	if game, ok = scene.Game.(slot.SlotGeneric); !ok {
		Ret403(c, ErrNotSlot)
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

	var props *Props
	if props, ok = user.props.Get(scene.CID); !ok {
		Ret500(c, ErrNoProps)
		return
	}

	if arg.Bet != 0 {
		if err = game.SetBet(arg.Bet); err != nil {
			Ret403(c, err)
			return
		}
	}
	if arg.Sel != 0 {
		if err = game.SetSel(arg.Sel); err != nil {
			Ret403(c, err)
			return
		}
	}

	var cost float64
	if !game.FreeMode() {
		cost = game.Cost()
		if props.Wallet < cost {
			Ret403(c, ErrNoMoney)
			return
		}
	}
	var mrtp = GetRTP(user, club)
	var isjp = len(game.JackFreq(mrtp)) > 0
	var jprate float64
	if isjp {
		jprate = club.Rate()
	}
	var bank, fund, _ = club.GetCash()
	var jprent = math.Round(cost*jprate) / 100

	var wins slot.Wins
	var gain, debit, jack float64
	defer wins.Reset()
	var n = 0
	game.Prepare()
	for {
		if n++; n > cfg.MaxSpinAttempts {
			Ret500(c, ErrBadBank)
			return
		}
		game.Spin(mrtp-jprate, &arg.Cheat)
		if game.Scanner(&wins) != nil {
			continue
		}
		if _, err = slot.CascadeGain(game, wins, fund, mrtp-jprate); err != nil {
			continue
		}
		game.Spawn(wins, fund, mrtp-jprate)
		gain = wins.Gain()
		jack = wins.Jackpot()
		debit = math.Round((cost-jprent-gain)*100) / 100
		if (bank+debit >= 0 || debit > 0) && (jack == 0 || jack > cfg.MinJackpot) {
			break
		}
		wins.Reset()
	}

	var walletDelta = math.Round((gain+jack-cost)*100) / 100 // = 玩家個人錢包餘額變動量 = 常規中獎+彩池中獎-下注金額
	var fundDelta = math.Round((jprent-jack)*100) / 100      // = 樂部彩金池變動量 = 非常少的抽成金額-彩池中獎

	go BankBat[scene.CID].PutSlot(cfg.XormStorage, scene.UID, debit, fundDelta, walletDelta)

	club.AddCash(debit, jprent-jack, 0)
	props.Wallet = math.Round((props.Wallet+walletDelta)*100) / 100
	game.Apply(wins)

	var sid = SpinCounter.Add(1)
	scene.SID = sid
	var rec = Spinlog{
		ID:     sid,
		GID:    arg.GID,
		Bet:    cost,
		Gain:   gain,
		Jack:   jack,
		Wallet: props.Wallet,
		MRTP:   mrtp,
	}

	var b []byte
	if b, err = json.Marshal(scene.Game); err != nil {
		return
	}
	rec.Game = string(b)

	if len(wins) > 0 {
		if b, err = json.Marshal(wins); err != nil {
			return
		}
		rec.Wins = string(b)
	}

	go func() {
		if err = SpinBuf.Put(cfg.XormStorage, rec); err != nil {
			log.Printf("can not write to spin log: %s", err.Error())
		}
	}()

	if len(arg.Cheat) > 0 {
		var effRtp = mrtp - jprate
		if gi, ok := coregame.InfoMap[scene.Story.Game]; ok {
			effRtp = gi.FindClosest(mrtp - jprate)
		}
		var rawReels = game.GetReels(mrtp - jprate)
		var reelsInt = make([][]int, len(rawReels))
		for i, col := range rawReels {
			reelsInt[i] = make([]int, len(col))
			for j, s := range col {
				reelsInt[i][j] = int(s)
			}
		}
		var audit = &SpinAudit{
			SID:        sid,
			GID:        arg.GID,
			CID:        scene.CID,
			Attempts:   n,
			TargetRTP:  mrtp,
			EffRTP:     effRtp,
			FundBefore: fund,
			FundAfter:  fund + jprent - jack,
			JackWin:    jack,
			JackRate:   jprate,
			JackRent:   jprent,
			Reels:      reelsInt,
		}
		scene.Audit = audit
		SpinAudits.Set(sid, audit)
	} else {
		scene.Audit = nil
	}

	ret.SID = sid
	ret.Game = game
	ret.Wins = wins
	ret.Wallet = props.Wallet
	if isjp {
		ret.JpFund = fund
	}

	RetOk(c, ret)
}

func ApiSlotAdminSpinInfo(c *gin.Context) {
	var err error
	var arg struct {
		GID uint64
		SID uint64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}

	var audit *SpinAudit
	var cid uint64

	if arg.SID != 0 {
		if a, exists := SpinAudits.Get(arg.SID); exists {
			audit = a
			cid = a.CID
		}
	}

	if audit == nil && arg.GID != 0 {
		var scene *Scene
		if scene, err = GetScene(arg.GID); err != nil {
			Ret404(c, err)
			return
		}
		audit = scene.Audit
		cid = scene.CID
	}

	if audit == nil {
		Ret404(c, errors.New("audit not found"))
		return
	}

	var _, al = MustAdmin(c, cid)
	if al&ALdealer == 0 && al&ALadmin == 0 {
		Ret403(c, ErrNoAccess)
		return
	}

	RetOk(c, audit)
}

func ApiSlotDoubleup(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		GID   uint64
		Mult  float64
		Half  bool
		Cheat int
	}
	var ret struct {
		ID     uint64
		Win    bool
		Risk   float64
		Gain   float64
		Wallet float64
	}

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}
	if arg.Mult <= 1 || arg.Mult > 10 {
		Ret400(c, ErrTooBig)
		return
	}

	var scene *Scene
	if scene, err = GetScene(arg.GID); err != nil {
		Ret404(c, err)
		return
	}
	var game slot.SlotGame
	if game, ok = scene.Game.(slot.SlotGame); !ok {
		Ret403(c, ErrNotSlot)
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

	var props *Props
	if props, ok = user.props.Get(scene.CID); !ok {
		Ret500(c, ErrNoProps)
		return
	}

	var oldgain = game.GetGain()
	var risk = oldgain
	if risk == 0 {
		Ret403(c, ErrNoGain)
		return
	}
	if arg.Half {
		risk /= 2
	}

	var bank = club.Bank()
	var mrtp = GetRTP(user, club)

	var upgainResult float64 = 0
	var upgainTemp float64 = risk * arg.Mult
	if bank >= upgainTemp {

		//mrtp = 96（即 96% RTP）
		//* 若玩家選 4 倍比倍（Mult = 4）
		//side = (1)/(4) × (96)/(100) = 0.25 × 0.96 = 0.24 (24%)
		//玩家勝率為 24%，期望回報為 0.24 × 4 = 96%。
		var side = (1 / arg.Mult) * (mrtp / 100)

		if arg.Cheat == 1 {
			side = 1.0
		}
		if arg.Cheat == -1 {
			side = -0.1
		}

		if rand.Float64() < side { //0 <= rand.Float64() < 1
			upgainResult = upgainTemp
		}
	}
	var debit = risk - upgainResult
	var newgain = oldgain - risk + upgainResult

	go BankBat[scene.CID].Put(cfg.XormStorage, scene.UID, debit)

	club.AddBank(debit)
	props.Wallet -= debit

	game.SetGain(newgain)

	var id = MultCounter.Add(1)

	var rec = Multlog{
		ID:     id,
		GID:    arg.GID,
		MRTP:   mrtp,
		Mult:   arg.Mult,
		Risk:   risk,
		Win:    upgainResult > 0,
		Gain:   newgain,
		Wallet: props.Wallet,
	}

	go func() {
		if err = MultBuf.Put(cfg.XormStorage, rec); err != nil {
			log.Printf("can not write to mult log: %s", err.Error())
		}
	}()

	ret.ID = id
	ret.Win = rec.Win
	ret.Risk = risk
	ret.Gain = newgain
	ret.Wallet = props.Wallet

	RetOk(c, ret)
}
