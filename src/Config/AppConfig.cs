namespace ServerMain.Config
{
    public static class AppConfig
    {
        public const double DefMRTP = 95.0;

        // Authentication
        public static readonly TimeSpan AccessTTL = TimeSpan.FromDays(1);
        public static readonly TimeSpan RefreshTTL = TimeSpan.FromDays(3);
        public const string AccessKey = "skJgM4NsbP3fs4k7vh0gfdkgGl8dJTszdLxZ1sQ9ksFnxbgvw2RsGH8xxddUV479";
        public const string RefreshKey = "zxK4dUnuq3Lhd1Gzhpr3usI5lAzgvy2t3fmxld2spzz7a5nfv0hsksm9cheyutie";
        public static readonly TimeSpan NonceTimeout = TimeSpan.FromSeconds(150);

        public static string PortHTTP { get; set; } = ":8080";

        // Database
        public static readonly TimeSpan SqlFlushTick = TimeSpan.FromMilliseconds(2500);
        public const int ClubUpdateBuffer = 200;
        public const int ClubInsertBuffer = 150;
        public const int SpinInsertBuffer = 250;

        // Gameplay
        public const double AdjunctLimit = 100000;
        public const double MinJackpot = 10000;
        public const int MaxSpinAttempts = 300;

        public static void SetPort(string port)
        {
            PortHTTP = port;
        }
    }
}
