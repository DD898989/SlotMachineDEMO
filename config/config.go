package cfg

import (
	"time"

	"xorm.io/xorm"
)

var (
	XormStorage *xorm.Engine
)

const DefMRTP = 95.0

// Authentication
var (
	AccessTTL    = 1 * 24 * time.Hour
	RefreshTTL   = 3 * 24 * time.Hour
	AccessKey    = "skJgM4NsbP3fs4k7vh0gfdkgGl8dJTszdLxZ1sQ9ksFnxbgvw2RsGH8xxddUV479"
	RefreshKey   = "zxK4dUnuq3Lhd1Gzhpr3usI5lAzgvy2t3fmxld2spzz7a5nfv0hsksm9cheyutie"
	NonceTimeout = 150 * time.Second
)

var (
	PortHTTP = ":8080"
)

// Database
var (
	SqlFlushTick     = 2500 * time.Millisecond
	ClubUpdateBuffer = 200
	ClubInsertBuffer = 150
	SpinInsertBuffer = 250
)

// Gameplay
var (
	AdjunctLimit    float64 = 100000
	MinJackpot      float64 = 10000
	MaxSpinAttempts         = 300
)

func SetPort(port string) {
	PortHTTP = port
}
