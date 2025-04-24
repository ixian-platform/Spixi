using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using Spixi;
using System.Text;

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

        public static string amountToHumanFormatString(IxiNumber amount)
        {
            string amount_string = amount.ToString();
            if (amount > 1)
                return amount_string[..^6];
            return amount_string;
        }

        public static void sendUiCommand(SpixiContentPage contentPage, string command, params string[] arguments)
        {
            try
            {
                string cmd_str = "executeUiCommand(" + command;
                StringBuilder sb = new StringBuilder(cmd_str);

                foreach (string arg in arguments)
                {
                    sb.Append(",'");
                    sb.Append(escapeHtmlParameter(arg));
                    sb.Append("'");
                }

                sb.Append(");");
                cmd_str = sb.ToString();
                // Logging.info("JS {0}", cmd_str);
                contentPage.sendMessage(cmd_str);

            }
            catch(Exception e)
            {
                Logging.error("Exception occured in sendUiCommand " + e);
            }
        }

        public static SingleChatPage getChatPage(Friend friend)
        {
            foreach (var item in App.appWindow.Navigation.NavigationStack)
            {
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
    }
}
