package api

import (
	"encoding/json"
	"net/http"

	"github.com/gin-gonic/gin"
	"xorm.io/xorm"
)

type Session = xorm.Session

func Negotiate(c *gin.Context, status int, data any) {
	c.JSON(status, data)
	c.Abort()
}

func RetOk(c *gin.Context, data any) {
	Negotiate(c, http.StatusOK, data)
}

func Ret204(c *gin.Context) {
	c.Status(http.StatusNoContent)
}

type jerr struct {
	error
}

func (err jerr) MarshalJSON() ([]byte, error) {
	return json.Marshal(err.Error())
}

type ajaxerr struct {
	What jerr
	UID  uint64
}

func RetErr(c *gin.Context, status int, err error) {
	var uid uint64
	if uv, ok := c.Get(userKey); ok {
		uid = uv.(*User).ID
	}
	Negotiate(c, status, ajaxerr{
		What: jerr{err},
		UID:  uid,
	})
}

func Ret400(c *gin.Context, err error) {
	RetErr(c, http.StatusBadRequest, err)
}

func Ret401(c *gin.Context, err error) {
	RetErr(c, http.StatusUnauthorized, err)
}

func Ret403(c *gin.Context, err error) {
	RetErr(c, http.StatusForbidden, err)
}

func Ret404(c *gin.Context, err error) {
	RetErr(c, http.StatusNotFound, err)
}

func Ret500(c *gin.Context, err error) {
	RetErr(c, http.StatusInternalServerError, err)
}

func SetupRouter(r *gin.Engine) {

	r.POST("/signin", ApiSignin)

	var ra = r.Group("/", Auth())

	var rg = ra.Group("/game")
	rg.POST("/new", ApiGameNew)

	var rs = ra.Group("/slot")
	rs.POST("/sel/set", ApiSlotSelSet)
	rs.POST("/spin", ApiSlotSpin)
	rs.POST("/admin/spin/info", ApiSlotAdminSpinInfo)
	rs.POST("/doubleup", ApiSlotDoubleup)

	var rk = ra.Group("/keno")
	rk.POST("/spin", ApiKenoSpin)

	var rp = ra.Group("/prop")
	rp.POST("/wallet/get", ApiPropsWalletGet)
	rp.POST("/wallet/add", ApiPropsWalletAdd)
	rp.POST("/rtp/set", ApiPropsRtpSet)

	var rc = ra.Group("/club")
	rc.POST("/info", ApiClubInfo)
	rc.POST("/cashin", ApiClubCashin)
}
