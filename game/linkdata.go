package game

import (
	"errors"
	"math"
	"sort"
	"strings"

	"github.com/slotopol/server/game/keno/americankeno"
	"github.com/slotopol/server/game/slot/agt/jokers100"
	"github.com/slotopol/server/game/slot/megajack/slotopol"
	"github.com/slotopol/server/game/slot/netent/gonzosquest"
	"github.com/slotopol/server/game/slot/novomatic/bookofra"
)

type Gamble interface {
	Spin(float64, *[]int)
	SetBet(float64) error
}

type EGame uint

const (
	EGameNone EGame = iota
	// Novomatic
	EGame_novomatic_bookofra
	EGame_novomatic_bookofradeluxe
	EGame_novomatic_gateofradeluxe
	EGame_novomatic_goldenprophecies
	EGame_novomatic_downunder
	EGame_novomatic_godofsun
	EGame_novomatic_lordoftheocean
	EGame_novomatic_faust
	EGame_novomatic_therealkinggoldrecords
	EGame_novomatic_angrybirds
	// NetEnt
	EGame_netent_gonzosquest
	// Megajack
	EGame_megajack_slotopol
	// AGT
	EGame_agt_100jokers
	EGame_agt_50happysanta
	EGame_agt_40bigfoot
	// Aristocrat
	EGame_aristocrat_americankeno
)

type (
	GameAlias struct {
		Game EGame
		Prov string
		Name string
	}

	AlgDescr struct {
		RTP []float64
	}

	AlgInfo struct {
		Aliases []GameAlias
		AlgDescr
	}

	GameInfo struct {
		GameAlias
		*AlgDescr
	}
)

var ErrNoAliase = errors.New("no game alias")

var BookOfRaInfo = AlgInfo{
	Aliases: []GameAlias{
		{Game: EGame_novomatic_bookofra, Prov: "Novomatic", Name: "Book of Ra"},
		{Game: EGame_novomatic_bookofradeluxe, Prov: "Novomatic", Name: "Book of Ra Deluxe"},
		{Game: EGame_novomatic_gateofradeluxe, Prov: "Novomatic", Name: "Gate of Ra Deluxe"},
		{Game: EGame_novomatic_goldenprophecies, Prov: "Novomatic", Name: "Golden Prophecies"},
		{Game: EGame_novomatic_downunder, Prov: "Novomatic", Name: "Down Under"},
		{Game: EGame_novomatic_godofsun, Prov: "Novomatic", Name: "God of Sun"},
		{Game: EGame_novomatic_lordoftheocean, Prov: "Novomatic", Name: "Lord of the Ocean"},
		{Game: EGame_novomatic_faust, Prov: "Novomatic", Name: "Faust"},
		{Game: EGame_novomatic_therealkinggoldrecords, Prov: "Novomatic", Name: "The Real King Gold Records"},
		{Game: EGame_novomatic_angrybirds, Prov: "Novomatic", Name: "Angry Birds"},
	},
}

var GonzosQuestInfo = AlgInfo{
	Aliases: []GameAlias{
		{Game: EGame_netent_gonzosquest, Prov: "NetEnt", Name: "Gonzo's Quest"},
	},
}

var SlotopolInfo = AlgInfo{
	Aliases: []GameAlias{
		{Game: EGame_megajack_slotopol, Prov: "Megajack", Name: "Slotopol"},
	},
}

var Jokers100Info = AlgInfo{
	Aliases: []GameAlias{
		{Game: EGame_agt_100jokers, Prov: "AGT", Name: "100 Jokers"},
		{Game: EGame_agt_50happysanta, Prov: "AGT", Name: "50 Happy Santa"},
		{Game: EGame_agt_40bigfoot, Prov: "AGT", Name: "40 Bigfoot"},
	},
}

var AmericanKenoInfo = AlgInfo{
	Aliases: []GameAlias{
		{Game: EGame_aristocrat_americankeno, Prov: "Aristocrat", Name: "American Keno"},
	},
	AlgDescr: AlgDescr{
		RTP: []float64{89.250235},
	},
}

var AlgList = []*AlgInfo{
	&BookOfRaInfo,
	&GonzosQuestInfo,
	&SlotopolInfo,
	&Jokers100Info,
	&AmericanKenoInfo,
}

var InfoMap map[EGame]*GameInfo

func init() {
	BookOfRaInfo.RTP = MakeRtpList(bookofra.ReelsMap)
	GonzosQuestInfo.RTP = MakeRtpList(gonzosquest.ReelsMap)
	SlotopolInfo.RTP = MakeRtpList(slotopol.ReelsMap)
	Jokers100Info.RTP = MakeRtpList(jokers100.ReelsMap)
	InfoMap = makeInfoMap(AlgList)
}

func makeInfoMap(list []*AlgInfo) map[EGame]*GameInfo {
	var m = make(map[EGame]*GameInfo)
	for _, ai := range list {
		for _, ga := range ai.Aliases {
			m[ga.Game] = &GameInfo{
				GameAlias: ga,
				AlgDescr:  &ai.AlgDescr,
			}
		}
	}
	return m
}

func MakeRtpList[T any](reelsmap map[float64]T) []float64 {
	var list = make([]float64, 0, len(reelsmap))
	for rtp := range reelsmap {
		list = append(list, rtp)
	}
	sort.Float64s(list)
	return list
}

func (ga *GameAlias) ID() string {
	return strings.ToLower(ga.Prov + "/" + ga.Name)
}

func (ad *AlgDescr) FindClosest(mrtp float64) (rtp float64) {
	rtp = -1000
	for _, p := range ad.RTP {
		if math.Abs(mrtp-p) < math.Abs(mrtp-rtp) {
			rtp = p
		}
	}
	return
}

func NewGame(g EGame) (Gamble, error) {
	switch g {
	// Novomatic - Book of Ra & variants
	case EGame_novomatic_bookofra,
		EGame_novomatic_downunder,
		EGame_novomatic_godofsun:
		return bookofra.NewGame(9), nil

	case EGame_novomatic_bookofradeluxe,
		EGame_novomatic_gateofradeluxe,
		EGame_novomatic_goldenprophecies,
		EGame_novomatic_lordoftheocean,
		EGame_novomatic_faust,
		EGame_novomatic_therealkinggoldrecords,
		EGame_novomatic_angrybirds:
		return bookofra.NewGame(10), nil

	// NetEnt - Gonzo's Quest
	case EGame_netent_gonzosquest:
		return gonzosquest.NewGame(), nil

	// Megajack - Slotopol
	case EGame_megajack_slotopol:
		return slotopol.NewGame(21), nil

	// AGT - Jokers
	case EGame_agt_100jokers:
		return jokers100.NewGame(100), nil
	case EGame_agt_50happysanta:
		return jokers100.NewGame(50), nil
	case EGame_agt_40bigfoot:
		return jokers100.NewGame(40), nil

	// Aristocrat - American Keno
	case EGame_aristocrat_americankeno:
		return americankeno.NewGame(), nil

	default:
		return nil, ErrNoAliase
	}
}
