package api

import (
	"github.com/gin-gonic/gin"

	cfg "github.com/slotopol/server/config"
	"github.com/slotopol/server/game"
	"github.com/slotopol/server/util"
)

var (
	SpinBuf util.SqlBuf[Spinlog]
	MultBuf util.SqlBuf[Multlog]
	BankBat = map[uint64]*SqlBank{}
	JoinBuf = SqlStory{}
)

func ApiGameNew(c *gin.Context) {
	var err error
	var ok bool
	var arg struct {
		CID  uint64
		UID  uint64
		Game game.EGame
	}
	var ret struct {
		GID    uint64
		Game   any
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
	if (al&ALmember == 0) || (admin != user && al&ALdealer == 0) {
		Ret403(c, ErrNoAccess)
		return
	}

	var anygame game.Gamble
	if anygame, err = game.NewGame(arg.Game); err != nil {
		Ret400(c, ErrNoAliase)
		return
	}

	var gid = StoryCounter.Add(1)
	var scene = &Scene{
		Story: Story{
			ID:   gid,
			CID:  arg.CID,
			UID:  arg.UID,
			Game: arg.Game,
		},
		Game: anygame,
	}

	go JoinBuf.Join(cfg.XormStorage, &scene.Story)

	Scenes.Set(scene.ID, scene)

	ret.GID = scene.ID
	ret.Game = scene.Game
	ret.Wallet = user.GetWallet(arg.CID)

	RetOk(c, ret)
}
