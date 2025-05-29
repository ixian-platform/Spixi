using IXICore;
using IXICore.Meta;

namespace SPIXI.Meta
{
    public class Config
    {
        // Providing pre-defined values
        // Can be read from a file later, or read from the command line

        public static NetworkType networkType = NetworkType.main;

        public static bool enablePushNotifications = true;

        public static string walletFile = "wallet.ixi";

        public static string spixiUserFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "Spixi");

        public static int encryptionRetryPasswordAttempts = 3;   // How many allowed attempts in the LaunchRetry page before throwing the user back to Launch Page

        // Read-only values
        public static readonly string aboutUrl = "https://www.spixi.io";
        public static readonly string guideUrl = "https://www.spixi.io/howitworks";
        public static readonly string explorerUrl = "https://explorer.ixian.io/";

        public static readonly string pushServiceUrl = "https://ipn.ixian.io/v2";
        public static readonly string priceServiceUrl = "https://resources.ixian.io/ixiprice.txt";

        public static readonly int checkPriceSeconds = 1800; // 30 minutes

        public static readonly int packetDataSize = 102400; // 100 Kb per packet for file transfers
        public static readonly long packetRequestTimeout = 60; // Time in seconds to re-request packets

        public static readonly string version = "spixi-0.9.6"; // Spixi version

        public static readonly string checkVersionUrl = "https://resources.ixian.io/spixi-update.txt";
        public static readonly int checkVersionSeconds = 1 * 60 * 60; // 1 hour

        public static readonly string supportEmailUrl = "mailto:support@spixi.io?subject=Spixi%20Feedback";
        public static readonly string ratingAndroidUrl = "https://play.google.com/store/apps/details?id=com.ixilabs.spixi&reviewId=0";
        public static readonly string ratingiOSUrl = "https://apps.apple.com/app/id6667121792?action=write-review";



        // Default SPIXI settings
        public static bool defaultXamarinAnimations = false;
        public static uint messagesToLoad = 100; // Number of chat messages to load in each chunk
        public static ulong txConfirmationBlocks = 10; // Number of blocks until transaction is confirmed

        // Push notifications OneSignal AppID
        public static string oneSignalAppId = "af20710d-7d68-4038-94a4-2896f3029263";

        // Temporary variables for bh sync recovery
        // Note: Always round last block height to 1000 and subtract 1 (i.e. if last block height is 33234, the correct value is 32999)
        public static ulong bakedRecoveryBlockHeight = 4199999;
        public static byte[] bakedRecoveryBlockChecksum = Crypto.stringToHash("f17fe6d63acac3efa071e2e99122099d6ff97b9a453126ae410e7987ccb71c759d24b80d7ac694588a07628fb83e3ec4bb8a3bdf9770342e1cc5efa04d36b236");

        // VoIP settings, don't change
        public static readonly int VoIP_sampleRate = 16000;
        public static readonly int VoIP_bitRate = 16;
        public static readonly int VoIP_channels = 1;

        public static readonly long backupReminder = 86400 * 30; // 1 month
    }
}