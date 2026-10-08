package main

import (
	"context"
	"errors"
	"flag"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/slotopol/server/api"
	cfg "github.com/slotopol/server/config"

	_ "github.com/go-sql-driver/mysql"
	_ "github.com/lib/pq"
	"xorm.io/xorm"
	"xorm.io/xorm/names"
)

func main() {
	var portFlag = flag.String("port", "", "HTTP port to listen on (e.g. :8080 or 8080)")
	var dbdnsFlag = flag.String("dbdns", "", "Database connection string to connect to")
	var syncOnlyFlag = flag.Bool("sync-only", false, "Only synchronize database tables schema and exit")
	flag.Parse()

	cfg.SetPort(*portFlag)
	if *syncOnlyFlag {
		if err := SyncStorage(*dbdnsFlag); err != nil {
			log.Fatalln(err)
		}
		return
	}

	var exitctx = Startup()

	if err := InitStorage(*dbdnsFlag); err != nil {
		log.Fatalln(err.Error())
		return
	}

	go SqlLoop(exitctx)

	var r = gin.New()
	api.SetupRouter(r)

	var srv = http.Server{
		Addr:    cfg.PortHTTP,
		Handler: r.Handler(),
	}

	srv.ListenAndServe()
}

func Startup() (exitctx context.Context) {
	exitctx, cancel := context.WithCancel(context.Background())
	go func() {
		defer cancel()

		var sigint = make(chan os.Signal, 1)
		var sigterm = make(chan os.Signal, 1)

		signal.Notify(sigint, syscall.SIGINT)
		signal.Notify(sigterm, syscall.SIGTERM)

		select {
		case <-exitctx.Done():
			if errors.Is(exitctx.Err(), context.DeadlineExceeded) {
				log.Println("shutting down by timeout")
			} else if errors.Is(exitctx.Err(), context.Canceled) {
				log.Println("shutting down by cancel")
			} else {
				log.Printf("shutting down by %s\n", exitctx.Err().Error())
			}
		case <-sigint:
			log.Println("shutting down by break")
		case <-sigterm:
			log.Println("shutting down by process termination")
		}
		signal.Stop(sigint)
		signal.Stop(sigterm)
	}()
	return
}

func SyncTables(session *xorm.Session) error {
	return session.Sync(
		&api.ClubData{}, &api.User{}, &api.Props{},
		&api.Story{}, api.Walletlog{}, api.Banklog{},
		&api.Spinlog{}, &api.Multlog{},
	)
}

func SyncStorage(dsn string) (err error) {
	engine, err := xorm.NewEngine("mysql", dsn)
	if err != nil {
		return
	}
	defer engine.Close()

	engine.SetMapper(names.SameMapper{})

	var session = engine.NewSession()
	defer session.Close()

	return SyncTables(session)
}

func InitStorage(dsn string) (err error) {
	if cfg.XormStorage, err = xorm.NewEngine("mysql", dsn); err != nil {
		return
	}
	cfg.XormStorage.SetMapper(names.SameMapper{})

	var session = cfg.XormStorage.NewSession()
	defer session.Close()

	if err = SyncTables(session); err != nil {
		return
	}

	if err = SeedDatabase(session); err != nil {
		return
	}

	return SyncMemory(session)
}

func SyncMemory(session *xorm.Session) error {
	const limit = 256

	var offset = 0
	for {
		var chunk []api.ClubData
		if err := session.Limit(limit, offset).Find(&chunk); err != nil {
			return err
		}
		offset += limit
		for _, cd := range chunk {
			api.Clubs.Set(cd.ID, api.MakeClub(cd))
			var bat = &api.SqlBank{}
			bat.Init(cd.ID, cfg.ClubUpdateBuffer, cfg.ClubInsertBuffer)
			api.BankBat[cd.ID] = bat
		}
		if limit > len(chunk) {
			break
		}
	}
	log.Printf("loaded %d clubs\n", api.Clubs.Len())

	offset = 0
	for {
		var chunk []*api.User
		if err := session.Limit(limit, offset).Find(&chunk); err != nil {
			return err
		}
		offset += limit
		for _, user := range chunk {
			user.Init()
			api.Users.Set(user.ID, user)
		}
		if limit > len(chunk) {
			break
		}
	}
	log.Printf("loaded %d users\n", api.Users.Len())

	offset = 0
	for {
		var chunk []*api.Props
		if err := session.Limit(limit, offset).Find(&chunk); err != nil {
			return err
		}
		offset += limit
		for _, props := range chunk {
			if !api.Clubs.Has(props.CID) {
				return fmt.Errorf("found props without club linkage, UID=%d, CID=%d, value=%g", props.UID, props.CID, props.Wallet)
			}
			var user, ok = api.Users.Get(props.UID)
			if !ok {
				return fmt.Errorf("found props without user linkage, UID=%d, CID=%d, value=%g", props.UID, props.CID, props.Wallet)
			}
			user.InsertProps(props)
		}
		if limit > len(chunk) {
			break
		}
	}

	var i64 int64
	var err error
	if i64, err = session.Count(&api.Story{}); err != nil {
		return err
	}
	api.StoryCounter.Store(uint64(i64))

	if i64, err = session.Count(&api.Spinlog{}); err != nil {
		return err
	}
	api.SpinCounter.Store(uint64(i64))
	if i64, err = session.Count(&api.Multlog{}); err != nil {
		return err
	}
	api.MultCounter.Store(uint64(i64))

	return nil
}

func SqlLoop(exitctx context.Context) {
	var fd = cfg.SqlFlushTick
	var flush = time.Tick(fd)
	for {
		select {
		case <-flush:
			for cid, bat := range api.BankBat {
				if err := bat.Flush(cfg.XormStorage, fd); err != nil {
					log.Printf("can not update bank for cid=%d: %s", cid, err.Error())
				}
			}
			if err := api.JoinBuf.Flush(cfg.XormStorage); err != nil {
				log.Printf("can not write to story log: %s", err.Error())
			}
			if err := api.SpinBuf.Flush(cfg.XormStorage); err != nil {
				log.Printf("can not write to spin log: %s", err.Error())
			}
			if err := api.MultBuf.Flush(cfg.XormStorage); err != nil {
				log.Printf("can not write to mult log: %s", err.Error())
			}
		case <-exitctx.Done():
			return
		}
	}
}

func SeedDatabase(session *xorm.Session) error {
	//	// Initialize default props for new user creation
	//	api.PropMaster = []api.Props{
	//		{CID: 1, Wallet: 1000, Access: 1, MRTP: 0},
	//		{CID: 2, Wallet: 0, Access: 0, MRTP: 0},
	//	}
	//
	//	empty, err := session.IsTableEmpty(&api.ClubData{})
	//	if err != nil {
	//		return err
	//	}
	//	if !empty {
	//		return nil
	//	}
	//
	//	log.Println("Seeding initial database data...")
	//
	//	clubs := []api.ClubData{
	//		{ID: 1, Name: "virtual", Bank: 10000, Fund: 1000000, Lock: 0, Rate: 2.5, MRTP: 95},
	//	}
	//	if _, err := session.Insert(&clubs); err != nil {
	//		return err
	//	}
	//
	//	users := []api.User{
	//		{ID: 1, Email: "admin@example.org", Secret: "0YBoaT", Name: "admin", GAL: 31},
	//		{ID: 2, Email: "dealer@example.org", Secret: "LtpkAr", Name: "dealer", GAL: 3},
	//		{ID: 3, Email: "player@example.org", Secret: "iVI05M", Name: "player", GAL: 1},
	//		{ID: 4, Email: "player2@example.org", Secret: "iVI05M", Name: "player2", GAL: 1},
	//	}
	//	if _, err := session.Insert(&users); err != nil {
	//		return err
	//	}
	//
	//	props := []api.Props{
	//		{CID: 1, UID: 1, Wallet: 10000, Access: 1, MRTP: 0},
	//		{CID: 2, UID: 1, Wallet: 0, Access: 0, MRTP: 0},
	//		{CID: 1, UID: 2, Wallet: 10000, Access: 13, MRTP: 0},
	//		{CID: 2, UID: 2, Wallet: 0, Access: 0, MRTP: 0},
	//		{CID: 1, UID: 3, Wallet: 1000, Access: 1, MRTP: 98},
	//		{CID: 2, UID: 3, Wallet: 0, Access: 0, MRTP: 98},
	//		{CID: 1, UID: 4, Wallet: 1000, Access: 1, MRTP: 98},
	//		{CID: 2, UID: 4, Wallet: 0, Access: 0, MRTP: 98},
	//	}
	//	if _, err := session.Insert(&props); err != nil {
	//		return err
	//	}
	//
	//	log.Println("Database seed completed successfully.")
	return nil
}
