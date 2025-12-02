using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spixi
{
    public partial class SPlatformUtils
    {
        public static Stream getAsset(string path);
        public static string getAssetsBaseUrl();
        public static string getAssetsPath();
        public static string getHtmlBaseUrl();
        public static string getHtmlPath();
    }

    public partial class SPowerManager
    {
        public static bool AquireLock(string lock_type = "screenDim");
        public static bool ReleaseLock(string lock_type = "screenDim");
    }

    public partial class SSystemAlert
    {
        public static void displayAlert(string title, string message, string cancel);
        public static void flash();
    }

    public partial class SFileOperations
    {
        public static Task share(string filepath, string title);
        public static void open(string filepath);
    }

    public class SpixiImageData
    {
        public Stream stream = null;
        public string name = null;
        public string path = null;
    }

    public partial class SFilePicker
    {
        public static Task<SpixiImageData> PickImageAsync();
        public static Task<SpixiImageData> PickFileAsync();
        public static byte[] ResizeImage(byte[] image_data, int width, int height, int quality);
    }

    public partial class SPushService
    {
        public static void initialize();
        public static void setTag(string tag);
        public static void clearNotifications(int unreadCount);
        public static void clearRemoteNotifications(int unreadCount);
        public static void showLocalNotification(int messageId, string title, string message, string data, bool alert, int unreadCount);
    }

    public partial class SSpixiCodecInfo
    {
        public static List<string> getSupportedAudioCodecs();
        public static bool isCodecSupported(string codec_name);
    }
}
