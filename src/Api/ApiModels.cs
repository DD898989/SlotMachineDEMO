using System.Text.Json.Serialization;
using ServerMain.Data;
using ServerMain.Game;
using ServerMain.Game.Keno;
using ServerMain.Game.Slot;

namespace ServerMain.Api
{
    public class ErrorResponse
    {
        public string What { get; set; } = string.Empty;
        public ulong UID { get; set; }
    }

    public class SigninArg
    {
        public string? Email { get; set; }
        public string? Secret { get; set; }
        public string? SigTime { get; set; }
        public string? HS256 { get; set; }
    }

    public class SigninResp
    {
        public string Access { get; set; } = string.Empty;
        public string Refrsh { get; set; } = string.Empty;
        public string Expire { get; set; } = string.Empty;
        public string Living { get; set; } = string.Empty;
        public ulong UID { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class GameNewArg
    {
        public ulong CID { get; set; }
        public ulong UID { get; set; }
        public EGame Game { get; set; }
    }

    public class GameNewResp
    {
        public ulong GID { get; set; }
        public object? Game { get; set; }
        public double Wallet { get; set; }
    }

    public class SlotSelArg
    {
        public ulong GID { get; set; }
        public int Sel { get; set; }
    }

    public class SlotSpinArg
    {
        public ulong GID { get; set; }
        public double Bet { get; set; }
        public int Sel { get; set; }
        public List<int>? Cheat { get; set; }
    }

    public class SlotSpinResp
    {
        public ulong SID { get; set; }
        public object? Game { get; set; }
        public List<WinItem> Wins { get; set; } = new();
        public double Wallet { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double JpFund { get; set; }
    }

    public class SlotSpinInfoArg
    {
        public ulong SID { get; set; }
    }

    public class SlotDoubleupArg
    {
        public ulong GID { get; set; }
        public double Mult { get; set; } = 2.0;
        public int Cheat { get; set; }
        public bool Half { get; set; }
    }

    public class SlotDoubleupResp
    {
        public ulong ID { get; set; }
        public bool Win { get; set; }
        public double Risk { get; set; }
        public double Gain { get; set; }
        public double Wallet { get; set; }
    }

    public class KenoSpinArg
    {
        public ulong GID { get; set; }
        public double Bet { get; set; }
        public List<int>? Sel { get; set; }
        public List<int>? Cheat { get; set; }
    }

    public class KenoSpinResp
    {
        public ulong SID { get; set; }
        public object? Game { get; set; }
        public KenoWins Wins { get; set; } = new();
        public double Wallet { get; set; }
    }

    public class PropWalletGetArg
    {
        public ulong CID { get; set; }
        public ulong UID { get; set; }
    }

    public class PropWalletGetResp
    {
        public double Wallet { get; set; }
        public AccessLevel Access { get; set; }
        public double MRTP { get; set; }
    }

    public class PropWalletAddArg
    {
        public ulong CID { get; set; }
        public ulong UID { get; set; }
        public double Sum { get; set; }
    }

    public class PropWalletAddResp
    {
        public double Wallet { get; set; }
    }

    public class PropRtpSetArg
    {
        public ulong CID { get; set; }
        public ulong UID { get; set; }
        public double MRTP { get; set; }
    }

    public class ClubInfoArg
    {
        public ulong CID { get; set; }
    }

    public class ClubInfoResp
    {
        public string Name { get; set; } = string.Empty;
        public double Bank { get; set; }
        public double Fund { get; set; }
        public double Lock { get; set; }
        public double Rate { get; set; }
        public double MRTP { get; set; }
    }

    public class ClubCashinArg
    {
        public ulong CID { get; set; }
        public double BankSum { get; set; }
        public double FundSum { get; set; }
        public double LockSum { get; set; }
    }

    public class ClubCashinResp
    {
        public ulong BID { get; set; }
        public double Bank { get; set; }
        public double Fund { get; set; }
        public double Lock { get; set; }
    }
}
