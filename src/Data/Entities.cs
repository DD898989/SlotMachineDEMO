using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ServerMain.Game;

namespace ServerMain.Data
{
    [Flags]
    public enum AccessLevel : uint
    {
        None = 0,
        Member = 1 << 0,  // 1
        Dealer = 1 << 1,  // 2
        Booker = 1 << 2,  // 4
        Master = 1 << 3,  // 8
        Admin = 1 << 4    // 16
    }

    [Table(nameof(ClubData))]
    public class ClubData
    {
        public ulong ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Bank { get; set; }
        public double Fund { get; set; }
        public double Lock { get; set; }
        public double Rate { get; set; } = 2.5;
        public double MRTP { get; set; }
    }

    public class User
    {
        public ulong ID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccessLevel GAL { get; set; }
    }

    public class Props
    {
        public ulong ID { get; set; }
        public ulong CID { get; set; }
        public ulong UID { get; set; }
        public double Wallet { get; set; }
        public AccessLevel Access { get; set; }
        public double MRTP { get; set; }
    }

    public class Story
    {
        public ulong ID { get; set; }
        public ulong CID { get; set; }
        public ulong UID { get; set; }
        public EGame Game { get; set; }
        public string State { get; set; } = "{}";
    }

    public class Spinlog
    {
        public ulong ID { get; set; }
        public ulong GID { get; set; }
        public double Bet { get; set; }
        public double Gain { get; set; }
        public double Jack { get; set; }
        public double Wallet { get; set; }
        public double MRTP { get; set; }
        public string Game { get; set; } = string.Empty;
        public string? Wins { get; set; }
    }

    public class Multlog
    {
        public ulong ID { get; set; }
        public ulong GID { get; set; }
        public double MRTP { get; set; }
        public double Mult { get; set; }
        public double Risk { get; set; }
        public bool Win { get; set; }
        public double Gain { get; set; }
        public double Wallet { get; set; }
    }

    public class Walletlog
    {
        public ulong ID { get; set; }
        public ulong CID { get; set; }
        public ulong UID { get; set; }
        public ulong AID { get; set; }
        public double Wallet { get; set; }
        public double Sum { get; set; }
    }

    public class Banklog
    {
        public ulong ID { get; set; }
        public ulong CID { get; set; }
        public double Bank { get; set; }
        public double Fund { get; set; }
        public double Lock { get; set; }
        public double BankSum { get; set; }
        public double FundSum { get; set; }
        public double LockSum { get; set; }
    }
}
