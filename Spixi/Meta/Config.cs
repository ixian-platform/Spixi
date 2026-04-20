using IXICore.Meta;
using IXICore.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        public static string activityFolderPath = "";
        public static string headersFolderPath = "";
        public static string logFolderPath = "";

        public static int encryptionRetryPasswordAttempts = 3;   // How many allowed attempts in the LaunchRetry page before throwing the user back to Launch Page

        // Read-only values
        public static readonly string aboutUrl = "https://www.spixi.io";
        public static readonly string guideUrl = "https://www.spixi.io/help-center.html";
        public static readonly string explorerUrl = "https://explorer.ixian.io/";
        public static readonly string spixiAppsUrl = "https://apps.spixi.io/";

        public static readonly string pushServiceUrl = "https://ipn.ixian.io/v2";
        public static readonly string priceServiceUrl = "https://resources.ixian.io/ixiprice.txt";

        public static readonly int checkPriceSeconds = 1800; // 30 minutes

        public static readonly int packetDataSize = 102400; // 100 Kb per packet for file transfers
        public static readonly long packetRequestTimeout = 60; // Time in seconds to re-request packets

        public static readonly string version = "spixi-0.9.17"; // Spixi version

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

        // VoIP settings, don't change
        public static readonly int VoIP_sampleRate = 16000;
        public static readonly int VoIP_bitsPerSample = 16;
        public static readonly int VoIP_channels = 1;
        public static readonly long backupReminder = 86400 * 30; // 1 month

        public static int maxRelaySectorNodesToConnectTo = 3;


        public static int maxLogSize = 5;
        public static int maxLogCount = 1;

        public static int logVerbosity = (int)LogSeverity.info + (int)LogSeverity.warn + (int)LogSeverity.error;

        // Store the device id in a cache for reuse in later instances
        public static string externalIp = "";

        public static string configFilename = "ixian.cfg";

        public static byte[]? checksumLock = null;

        public static int maxConnectedStreamingNodes = 6;

        public static Dictionary<string, string> apiUsers = new Dictionary<string, string>();

        public static List<string> apiAllowedIps = new List<string>();
        public static List<string> apiBinds = new List<string>();

        public static ulong activityDbCacheSize = 8 << 20; // 8MB
        public static ulong blocksDbCacheSize = 8 << 20;

        public static long minRequiredDiskSpace = 50 << 20; // 50MB


        private static NetworkType parseNetworkTypeValue(string value)
        {
            NetworkType netType;
            value = value.ToLower();
            switch (value)
            {
                case "mainnet":
                    netType = NetworkType.main;
                    break;
                case "testnet":
                    netType = NetworkType.test;
                    break;
                case "regtest":
                    netType = NetworkType.reg;
                    break;
                default:
                    throw new Exception(string.Format("Unknown network type '{0}'. Possible values are 'mainnet', 'testnet', 'regtest'", value));
            }
            return netType;
        }

        private static void readConfigFile(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }
            Logging.info("Reading config file: " + filename);
            bool foundAddPeer = false;
            bool foundAddTestPeer = false;
            List<string> lines = File.ReadAllLines(filename).ToList();
            foreach (string line in lines)
            {
                string[] option = line.Split('=');
                if (option.Length < 2)
                {
                    continue;
                }
                string key = option[0].Trim([' ', '\t', '\r', '\n']);
                string value = option[1].Trim([' ', '\t', '\r', '\n']);

                if (key.StartsWith(";"))
                {
                    continue;
                }
                Logging.info("Processing config parameter '" + key + "' = '" + value + "'");
                switch (key)
                {
                    case "apiAllowIp":
                        apiAllowedIps.Add(value);
                        break;
                    case "apiBind":
                        apiBinds.Add(value);
                        break;
                    case "addApiUser":
                        string[] credential = value.Split(':');
                        if (credential.Length == 2)
                        {
                            apiUsers.Add(credential[0], credential[1]);
                        }
                        break;
                    case "externalIp":
                        externalIp = value;
                        break;
                    case "addPeer":
                        if (!foundAddPeer)
                        {
                            NetworkUtils.seedNodes.Clear();
                        }
                        foundAddPeer = true;
                        NetworkUtils.seedNodes.Add([value, null]);
                        break;
                    case "addTestnetPeer":
                        if (!foundAddTestPeer)
                        {
                            NetworkUtils.seedTestNetNodes.Clear();
                        }
                        foundAddTestPeer = true;
                        NetworkUtils.seedTestNetNodes.Add([value, null]);
                        break;
                    case "maxLogSize":
                        maxLogSize = int.Parse(value);
                        break;
                    case "maxLogCount":
                        maxLogCount = int.Parse(value);
                        break;
                    case "logVerbosity":
                        logVerbosity = int.Parse(value);
                        break;
                    case "checksumLock":
                        checksumLock = Encoding.UTF8.GetBytes(value);
                        break;
                    case "networkType":
                        networkType = parseNetworkTypeValue(value);
                        break;
                    case "logFolderPath":
                        logFolderPath = value;
                        break;
                    case "activityFolderPath":
                        activityFolderPath = value;
                        break;
                    case "headersFolderPath":
                        headersFolderPath = value;
                        break;
                    case "dataFolderPath":
                        spixiUserFolder = value;
                        break;
                    case "wallet":
                        walletFile = value;
                        break;
                    case "blocksDbCache":
                        blocksDbCacheSize = ulong.Parse(value);
                        break;
                    case "activityDbCache":
                        activityDbCacheSize = ulong.Parse(value);
                        break;
                    case "spixiUserFolder":
                        spixiUserFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), value);
                        break;
                    default:
                        // unknown key
                        Logging.warn("Unknown config parameter was specified '" + key + "'");
                        break;
                }
            }
        }

        public static void init()
        {
            readConfigFile(configFilename);

            if (headersFolderPath == "")
            {
                if (networkType == NetworkType.main)
                {
                    headersFolderPath = Path.Combine(spixiUserFolder, "headers");
                }
                else
                {
                    headersFolderPath = Path.Combine(spixiUserFolder, "testnet-headers");
                }
            }

            if (activityFolderPath == "")
            {
                if (networkType == NetworkType.main)
                {
                    activityFolderPath = Path.Combine(spixiUserFolder, "activity");
                }
                else
                {
                    activityFolderPath = Path.Combine(spixiUserFolder, "testnet-activity");
                }
            }

            if (logFolderPath == "")
            {
                logFolderPath = spixiUserFolder;
            }
        }
    }
}