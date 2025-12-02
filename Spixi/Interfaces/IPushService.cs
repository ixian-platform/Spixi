
namespace SPIXI.Interfaces
{
    public interface IPushService
    {
        void initialize();
        void setTag(string tag);
        void clearNotifications(int unreadCount);
        void clearRemoteNotifications(int unreadCount);
        void showLocalNotification(int messageId, string title, string message, string data, bool alert, int unreadCount);
    }
}
