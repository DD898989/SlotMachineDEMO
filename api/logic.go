package api

import (
	"encoding/json"
	"sync"
	"sync/atomic"

	"github.com/gin-gonic/gin"

	cfg "github.com/slotopol/server/config"
	"github.com/slotopol/server/game"
	"github.com/slotopol/server/util"
)

type ClubData struct {
	ID   uint64  `xorm:"pk autoincr"`
	Name string  `xorm:"notnull unique index"`
	Bank float64 `xorm:"notnull default 0"`
	Fund float64 `xorm:"notnull default 0"`
	Lock float64 `xorm:"notnull default 0"`
	Rate float64 `xorm:"notnull default 2.5"`
	MRTP float64 `xorm:"notnull default 0"`
}

type Club struct {
	data ClubData
	mux  sync.RWMutex
}

type User struct {
	ID     uint64 `xorm:"pk autoincr"`
	Email  string `xorm:"notnull unique index"`
	Secret string `xorm:"notnull"`
	Name   string `xorm:"notnull"`
	GAL    AL     `xorm:"notnull default 0"`
	props  util.RWMap[uint64, *Props]
}

type Story struct {
	ID   uint64     `xorm:"pk autoincr"` //=GID
	CID  uint64     `xorm:"notnull index"`
	UID  uint64     `xorm:"notnull index"`
	Game game.EGame `xorm:"notnull"`
}

var StoryCounter atomic.Uint64

type Scene struct {
	Story
	SID   uint64
	Game  game.Gamble
	Audit *SpinAudit
}

type SpinAudit struct {
	SID        uint64
	GID        uint64
	CID        uint64
	Attempts   int
	TargetRTP  float64
	EffRTP     float64
	FundBefore float64
	FundAfter  float64
	JackWin    float64
	JackRate   float64
	JackRent   float64
	Reels      [][]int
}

type AL uint

const (
	ALmember AL = 1 << iota
	ALdealer
	ALbooker
	ALmaster
	ALadmin
)

type Props struct {
	ID     uint64  `xorm:"pk autoincr"`
	CID    uint64  `xorm:"notnull unique(cid_uid) index"`
	UID    uint64  `xorm:"notnull unique(cid_uid) index"`
	Wallet float64 `xorm:"notnull default 0"`
	Access AL      `xorm:"notnull default 0"`
	MRTP   float64 `xorm:"notnull default 0"`
}

var PropMaster []Props

type Spinlog struct {
	ID     uint64  `xorm:"pk autoincr"`
	GID    uint64  `xorm:"notnull index"` // 關聯 Story.ID
	Bet    float64 `xorm:"notnull default 0"`
	Gain   float64 `xorm:"notnull default 0"`
	Jack   float64 `xorm:"notnull default 0"`
	Wallet float64 `xorm:"notnull"`
	MRTP   float64 `xorm:"notnull"`
	Game   string  `xorm:"text notnull"` // 盤面 JSON
	Wins   string  `xorm:"text"`         // 中獎明細 JSON
}

var SpinCounter atomic.Uint64

type Multlog struct {
	ID     uint64  `xorm:"pk autoincr"`
	GID    uint64  `xorm:"notnull index"` // 關聯 Story.ID
	MRTP   float64 `xorm:"notnull"`
	Mult   float64 `xorm:"notnull"`
	Risk   float64 `xorm:"notnull"`
	Win    bool    `xorm:"notnull"`
	Gain   float64 `xorm:"notnull"`
	Wallet float64 `xorm:"notnull"`
}

var MultCounter atomic.Uint64

type Walletlog struct {
	ID     uint64  `xorm:"pk autoincr"`
	CID    uint64  `xorm:"notnull index"`
	UID    uint64  `xorm:"notnull index"`
	AID    uint64  `xorm:"notnull"`
	Wallet float64 `xorm:"notnull"`
	Sum    float64 `xorm:"notnull"`
}

type Banklog struct {
	ID      uint64  `xorm:"pk autoincr"`
	CID     uint64  `xorm:"notnull index"`
	Bank    float64 `xorm:"notnull"`
	Fund    float64 `xorm:"notnull"`
	Lock    float64 `xorm:"notnull"`
	BankSum float64 `xorm:"notnull"`
	FundSum float64 `xorm:"notnull"`
	LockSum float64 `xorm:"notnull"`
}

var Clubs util.RWMap[uint64, *Club]

var Users util.RWMap[uint64, *User]

var Scenes util.RWMap[uint64, *Scene]

var SpinAudits util.RWMap[uint64, *SpinAudit]

func MakeClub(cd ClubData) *Club {
	return &Club{data: cd}
}

func (club *Club) Get() ClubData {
	club.mux.RLock()
	defer club.mux.RUnlock()
	return club.data
}

func (club *Club) Bank() float64 {
	club.mux.RLock()
	defer club.mux.RUnlock()
	return club.data.Bank
}

func (club *Club) GetCash() (bank, fund, deposit float64) {
	club.mux.RLock()
	defer club.mux.RUnlock()
	return club.data.Bank, club.data.Fund, club.data.Lock
}

func (club *Club) AddCash(bank, fund, deposit float64) {
	club.mux.Lock()
	defer club.mux.Unlock()
	club.data.Bank += bank
	club.data.Fund += fund
	club.data.Lock += deposit
}

func (club *Club) AddBank(bank float64) {
	club.mux.Lock()
	defer club.mux.Unlock()
	club.data.Bank += bank
}

func (club *Club) Rate() float64 {
	club.mux.RLock()
	defer club.mux.RUnlock()
	return club.data.Rate
}

func (club *Club) MRTP() float64 {
	club.mux.RLock()
	defer club.mux.RUnlock()
	return club.data.MRTP
}

func (user *User) Init() {
	user.props.Init(0)
}

func (user *User) GetWallet(cid uint64) float64 {
	if props, ok := user.props.Get(cid); ok {
		return props.Wallet
	}
	return 0
}

func (user *User) GetAL(cid uint64) AL {
	if props, ok := user.props.Get(cid); ok {
		return props.Access
	}
	return 0
}

func (user *User) InsertProps(props *Props) {
	user.props.Set(props.CID, props)
}

func MustAdmin(c *gin.Context, cid uint64) (*User, AL) {
	var admin = c.MustGet(userKey).(*User)
	return admin, admin.GAL | admin.GetAL(cid)
}

func GetRTP(user *User, club *Club) float64 {
	if user != nil {
		if props, ok := user.props.Get(club.data.ID); ok && props.MRTP != 0 {
			return props.MRTP
		}
	}
	if mrtp := club.MRTP(); mrtp != 0 {
		return mrtp
	}
	return cfg.DefMRTP
}

func GetScene(gid uint64) (scene *Scene, err error) {
	var ok bool
	if scene, ok = Scenes.Get(gid); ok {
		return
	}

	var tmp Scene
	if ok, _ = cfg.XormStorage.ID(gid).Get(&tmp.Story); !ok {
		err = ErrNotOpened
		return
	}
	if tmp.Game, err = game.NewGame(tmp.Story.Game); err != nil {
		err = ErrNoAliase
		return
	}

	scene = &tmp
	Scenes.Set(gid, scene)

	var rec Spinlog
	if ok, _ = cfg.XormStorage.Where("GID = ?", gid).Desc("ID").Get(&rec); !ok {
		return
	}
	scene.SID = rec.ID
	err = json.Unmarshal([]byte(rec.Game), scene.Game)
	return
}

func init() {
	Clubs.Init(0)
	Users.Init(0)
	Scenes.Init(0)
	SpinAudits.Init(0)
}
