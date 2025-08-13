using Foundation;
using SPIXI.Interfaces;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spixi
{
    public class SPlatformUtils
    {
        public static Stream getAsset(string path)
        {
            return new FileStream(Path.Combine(getAssetsPath(), path), FileMode.Open, FileAccess.Read);
        }

        public static string getAssetsBaseUrl()
        {
            return NSBundle.MainBundle.BundlePath + "/";
        }

        public static string getAssetsPath()
        {
            return NSBundle.MainBundle.BundlePath + "/Contents/Resources/";
        }

        public static string getHtmlBaseUrl()
        {
            return Config.spixiUserFolder + "/html/";
        }

        public static string getHtmlPath()
        {
            return Config.spixiUserFolder + "/html";
        }

        public static void startRinging()
        {
        }

        public static void stopRinging()
        {
        }

        public static void startDialtone(DialtoneType type)
        {
        }

        public static void stopDialtone()
        {
        }

        public static void setEdgeToEdge()
        {

        }
    }
}
