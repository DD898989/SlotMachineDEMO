package api

import (
	"crypto/hmac"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"strings"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/golang-jwt/jwt/v5"
	cfg "github.com/slotopol/server/config"
)

const (
	jwtIssuer = "slotopol"
	userKey   = "user"
)

var (
	ErrNoJwtID  = errors.New("jwt-token does not have user id")
	ErrBadJwtID = errors.New("jwt-token id does not refer to registered user")
	ErrNoAuth   = errors.New("authorization is required")
	ErrNoScheme = errors.New("authorization does not have expected scheme")
	ErrNoSecret = errors.New("expected password or SHA256 hash on it and current time as a nonce")
	ErrSmallKey = errors.New("password too small")
	ErrNoCred   = errors.New("user with given credentials does not registered")
	ErrNotPass  = errors.New("password is incorrect")
	ErrSigTime  = errors.New("signing time can not been recognized (time in RFC3339 expected)")
	ErrSigOut   = errors.New("nonce is expired")
	ErrBadHash  = errors.New("hash cannot be decoded in hexadecimal")
)

type Claims struct {
	jwt.RegisteredClaims
	UID uint64
}

func (c *Claims) Validate() error {
	if c.UID == 0 {
		return ErrNoJwtID
	}
	return nil
}

func Auth() gin.HandlerFunc {
	return func(c *gin.Context) {
		user, err := UserFromHeader(c)
		if err != nil {
			Ret401(c, err)
			return
		}

		if user == nil {
			Ret401(c, ErrNoAuth)
			return
		}

		c.Set(userKey, user)
		c.Next()
	}
}

func UserFromHeader(c *gin.Context) (*User, error) {
	if hdr := c.Request.Header.Get("Authorization"); hdr != "" {
		if strings.HasPrefix(hdr, "Bearer ") {
			return GetBearerAuth(hdr[7:])
		} else {
			return nil, ErrNoScheme
		}
	}
	return nil, nil
}

func GetBearerAuth(tokenstr string) (user *User, err error) {
	var claims Claims
	_, err = jwt.ParseWithClaims(tokenstr, &claims, func(*jwt.Token) (any, error) {
		var keys = jwt.VerificationKeySet{
			Keys: []jwt.VerificationKey{
				[]byte(cfg.AccessKey),
				[]byte(cfg.RefreshKey),
			},
		}
		return keys, nil
	}, jwt.WithExpirationRequired(), jwt.WithIssuer(jwtIssuer), jwt.WithLeeway(5*time.Second))

	if err != nil {
		return nil, err
	}
	var ok bool
	if user, ok = Users.Get(claims.UID); !ok {
		return nil, ErrBadJwtID
	}
	return user, nil
}

type AuthResp struct {
	UID    uint64
	Email  string
	Access string
	Refrsh string
	Expire string
	Living string
}

func (r *AuthResp) Setup(user *User) {
	var err error
	var token *jwt.Token
	var now = jwt.NewNumericDate(time.Now())
	var exp = jwt.NewNumericDate(time.Now().Add(cfg.AccessTTL))
	var age = jwt.NewNumericDate(time.Now().Add(cfg.RefreshTTL))
	token = jwt.NewWithClaims(jwt.SigningMethodHS256, &Claims{
		RegisteredClaims: jwt.RegisteredClaims{
			NotBefore: now,
			ExpiresAt: exp,
			Issuer:    jwtIssuer,
		},
		UID: user.ID,
	})
	if r.Access, err = token.SignedString([]byte(cfg.AccessKey)); err != nil {
		panic(err)
	}
	r.Expire = exp.Format(time.RFC3339)
	token = jwt.NewWithClaims(jwt.SigningMethodHS256, &Claims{
		RegisteredClaims: jwt.RegisteredClaims{
			NotBefore: now,
			ExpiresAt: age,
			Issuer:    jwtIssuer,
		},
		UID: user.ID,
	})
	if r.Refrsh, err = token.SignedString([]byte(cfg.AccessKey)); err != nil {
		panic(err)
	}
	r.Living = age.Format(time.RFC3339)
	r.UID = user.ID
	r.Email = user.Email
}

func ApiSignin(c *gin.Context) {
	var err error
	var arg struct {
		UID     uint64
		Email   string
		Secret  string
		HS256   string
		SigTime string
	}
	var ret AuthResp

	if err = c.ShouldBind(&arg); err != nil {
		Ret400(c, err)
		return
	}
	if len(arg.SigTime) == 0 && len(arg.Secret) == 0 {
		Ret400(c, ErrNoSecret)
		return
	}
	if len(arg.Secret) > 0 && len(arg.Secret) < 6 {
		Ret400(c, ErrSmallKey)
		return
	}

	var email = strings.ToLower(arg.Email)

	var user *User
	if arg.UID != 0 {
		user, _ = Users.Get(arg.UID)
	} else {
		for _, u := range Users.Items() {
			if u.Email == email {
				user = u
				break
			}
		}
	}
	if user == nil {
		Ret403(c, ErrNoCred)
		return
	}

	if len(arg.Secret) > 0 {
		if arg.Secret != user.Secret {
			Ret403(c, ErrNotPass)
			return
		}
	} else {
		var sigtime time.Time
		if sigtime, err = time.Parse(time.RFC3339, arg.SigTime); err != nil {
			Ret400(c, ErrSigTime)
			return
		}
		if time.Since(sigtime) > cfg.NonceTimeout {
			Ret403(c, ErrSigOut)
			return
		}

		var hs256 []byte
		if hs256, err = hex.DecodeString(arg.HS256); err != nil {
			Ret400(c, ErrBadHash)
			return
		}

		var h = hmac.New(sha256.New, []byte(arg.SigTime))
		h.Write([]byte(user.Secret))
		var master = h.Sum(nil)
		if !hmac.Equal(master, hs256) {
			Ret403(c, ErrNotPass)
			return
		}
	}

	ret.Setup(user)
	RetOk(c, ret)
}
