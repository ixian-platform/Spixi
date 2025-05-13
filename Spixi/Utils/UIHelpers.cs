using IXICore;
using IXICore.Streaming;

namespace SPIXI
{
    public static class UIHelpers
    {
        public static bool shouldRefreshContacts = false;

        public static void setContactStatus(Address address, bool online, int unread, string excerpt, long timestamp)
        {
            Page page = Application.Current.MainPage.Navigation.NavigationStack.Last();
            if (page != null && page is HomePage)
            {
                ((HomePage)page).setContactStatus(address, online, unread, excerpt, timestamp);
            }else
            {
                shouldRefreshContacts = true;
            }
        }

        // Reload the webview contents on all pages in the navigation stack
        // On iOS it will also pop the current page in the navigation stack
        public static void reloadAllPages()
        {
            var stack = Application.Current.MainPage.Navigation.NavigationStack;
            foreach (Page p in stack)
            {
                ((SpixiContentPage)p).reload();
            }
        }

        public static void updateMessage(Friend friend, int channel, FriendMessage msg)
        {
            Utils.getChatPage(friend).updateMessage(msg, channel);
        }

        public static void insertMessage(Friend friend, int channel, FriendMessage msg)
        {
            Utils.getChatPage(friend).insertMessage(msg, channel);
        }

        public static void deleteMessage(Friend friend, int channel, byte[] msgId)
        {
            Utils.getChatPage(friend).deleteMessage(msgId, channel);
        }

        public static void updateReactions(Friend friend, int channel, byte[] msgId)
        {
            Utils.getChatPage(friend).updateReactions(msgId, channel);
        }

        public static void updateGroupChatNicks(Friend friend, Address realSenderAddress, string nick)
        {
            Utils.getChatPage(friend).updateGroupChatNicks(realSenderAddress, nick);
        }

        public static bool isChatScreenDisplayed(Friend friend)
        {
            return Utils.getChatPage(friend) != null ? true : false;
        }
    }
}
