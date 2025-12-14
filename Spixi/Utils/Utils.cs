using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using Spixi;
using System.Text;
using System.Text.RegularExpressions;

namespace SPIXI
{
    public class Utils
    {
        public static DateTime unixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static string unixTimeStampToString(double unixTimeStamp)
        {
            DateTime datetime = unixTimeStampToDateTime(unixTimeStamp);
            return datetime.ToString("MM/dd/yyyy HH:mm:ss");
        }

        public static string unixTimeStampToHumanFormatString(double unixTimeStamp)
        {
            DateTime datetime = unixTimeStampToDateTime(unixTimeStamp);
            return datetime.ToString("dd MMM, yyyy, h:mm tt");
        }

        public static string escapeHtmlParameter(string str)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
        }

        public static string escapeHtmlParameter(byte[] str)
        {
            return Convert.ToBase64String(str);
        }

        public static string amountToHumanFormatString(IxiNumber amount)
        {
            string amount_string = amount.ToString();
            if (amount > 1)
                return amount_string[..^6];
            return amount_string;
        }

        public static string bytesToHumanFormatString(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{(bytes / 1024.0):0.##} kB";
            return $"{(bytes / 1024.0 / 1024.0):0.##} MB";
        }

        public static void sendUiCommand(SpixiContentPage contentPage, string command, params string[] arguments)
        {
            try
            {
                string cmd_str = "executeUiCommand(" + command;
                StringBuilder sb = new StringBuilder(cmd_str);

                foreach (string arg in arguments)
                {
                    if (arg != null)
                    {
                        sb.Append(",");
                        sb.Append("'" + escapeHtmlParameter(arg) + "'");
                    }
                    else
                    {
                        sb.Append(",null");
                    }
                }

                sb.Append(");");
                cmd_str = sb.ToString();
                contentPage.sendMessage(cmd_str);

            }
            catch (Exception e)
            {
                Logging.error("Exception occured in sendUiCommand " + e);
            }
        }

        public static SingleChatPage getChatPage(Friend friend)
        {
            
            foreach (var item in App.Current.MainPage.Navigation.NavigationStack)
            {
                if (item is SingleChatPage)
                {
                    if (((SingleChatPage)item).friend == friend)
                    {
                        return (SingleChatPage)item;
                    }
                }
            }

            if (HomePage.Instance() != null
                && HomePage.Instance().getDetailContent() != null)
            {
                var item = HomePage.Instance().getDetailContent();
                if (item is SingleChatPage)
                {
                    if (((SingleChatPage)item).friend == friend)
                    {
                        return (SingleChatPage)item;
                    }
                }
            }

            return null;
        }

        public static List<SingleChatPage> getChatPages()
        {
            List<SingleChatPage> chatPages = new();
            foreach (var item in App.Current.MainPage.Navigation.NavigationStack)
            {
                if (item is SingleChatPage)
                {
                    chatPages.Add((SingleChatPage)item);
                }
            }
            return chatPages;
        }

        public static bool IsAllowedURL(string url)
        {
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                string rx_pattern = @"^https://[A-Za-z0-9]+\.(tenor|giphy)\.com/[A-Za-z0-9_/=%\?\-\.\&]+$";

                if (Regex.IsMatch(url, rx_pattern))
                {
                    // Allow tenor and giphy URLs
                    return true;
                }

                return false;
            }

            return true;
        }
    }
}
