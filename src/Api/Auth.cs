using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ServerMain.Config;
using ServerMain.Data;

namespace ServerMain.Api
{
    public static class Auth
    {
        public static string MakeToken(ulong uid, string key, TimeSpan ttl)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var now = DateTime.UtcNow;

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = "slotopol",
                NotBefore = now,
                Expires = now.Add(ttl),
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("uid", uid.ToString())
                }),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public static (ulong? uid, string? error) ParseToken(string tokenString, string key)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var keyBytes = Encoding.UTF8.GetBytes(key);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "slotopol",
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(5)
                };

                var principal = tokenHandler.ValidateToken(tokenString, validationParameters, out var validatedToken);
                var uidClaim = principal.FindFirst("uid")?.Value;
                if (ulong.TryParse(uidClaim, out var uid))
                {
                    return (uid, null);
                }
                return (null, "bad token payload");
            }
            catch
            {
                return (null, "authorization is required");
            }
        }

        public static User? GetUserFromContext(HttpContext context, out string? error)
        {
            error = null;
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                error = "authorization is required";
                return null;
            }

            var tokenString = authHeader.Substring("Bearer ".Length).Trim();
            var (uid, parseError) = ParseToken(tokenString, AppConfig.AccessKey);
            if (parseError != null || uid == null)
            {
                error = parseError ?? "authorization is required";
                return null;
            }

            if (!DatabaseManager.Users.TryGetValue(uid.Value, out var user))
            {
                error = "user with given ID does not found";
                return null;
            }

            return user;
        }

        public static bool CheckAdminAccess(User user, ulong cid)
        {
            if (user.GAL.HasFlag(AccessLevel.Admin)) return true;
            return user.GetAL(cid).HasFlag(AccessLevel.Admin);
        }

        public static bool CheckMasterAccess(User user, ulong cid)
        {
            if (user.GAL.HasFlag(AccessLevel.Admin) || user.GAL.HasFlag(AccessLevel.Master)) return true;
            var al = user.GetAL(cid);
            return al.HasFlag(AccessLevel.Admin) || al.HasFlag(AccessLevel.Master);
        }

        public static bool CheckBookerAccess(User user, ulong cid)
        {
            if (user.GAL.HasFlag(AccessLevel.Admin) || user.GAL.HasFlag(AccessLevel.Master) || user.GAL.HasFlag(AccessLevel.Booker)) return true;
            var al = user.GetAL(cid);
            return al.HasFlag(AccessLevel.Admin) || al.HasFlag(AccessLevel.Master) || al.HasFlag(AccessLevel.Booker);
        }

        public static bool CheckClubAccess(User user, ulong cid)
        {
            if (user.GAL.HasFlag(AccessLevel.Admin)) return true;
            return user.Props.ContainsKey(cid);
        }
    }
}
