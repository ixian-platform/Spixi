using Foundation;
using IXICore.Meta;
using IXICore.Streaming;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core.Debug;
using SPIXI;
using UIKit;
using UserNotifications;

namespace Spixi
{
    public class SPushService
    {
        public class NotificationDelegate : UNUserNotificationCenterDelegate
        {
            public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
            {
                try
                {
                    if (response.Notification.Request.Content.UserInfo.ContainsKey((NSString)"fa"))
                    {
                        var fa = response.Notification.Request.Content.UserInfo[(NSString)"fa"];
                        if (fa != null)
                        {
                            App.startingScreen = Convert.ToString(fa);
                            HomePage.Instance().onChat(App.startingScreen, null);
                            App.startingScreen = "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.error("Exception occured in DidReceiveNotificationResponse: {0}", ex);
                }
            }
        }

        public static void initialize()
        {
            OneSignal.Debug.LogLevel = LogLevel.WARN;
            OneSignal.Debug.AlertLevel = LogLevel.NONE;

            OneSignal.Initialize(SPIXI.Meta.Config.oneSignalAppId);

            OneSignal.Notifications.RequestPermissionAsync(true);

            OneSignal.Notifications.Clicked += handleNotificationOpened;
            OneSignal.Notifications.WillDisplay += handleNotificationReceived;
        }

        public static void setTag(string tag)
        {
            OneSignal.User.AddTag("ixi", tag);
        }

        public static void clearNotifications()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (UIDevice.CurrentDevice.CheckSystemVersion(16, 0))
                {
                    // For iOS 16+, use UNUserNotificationCenter
                    UNUserNotificationCenter.Current.SetBadgeCount(0, (err) =>
                    {
                        if (err != null)
                        {
                            Logging.warn("Set badge count failed");
                            Logging.warn(err.ToString());
                        }
                    });
                }
                else
                {
                    // For older versions, use UIApplication
                    UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
                }
            });
        }

        public static void showLocalNotification(string title, string message, string data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var content = new UNMutableNotificationContent
                {
                    Title = title,
                    Body = message,
                    Badge = 1,
                    Sound = UNNotificationSound.Default
                };

                content.UserInfo = new NSMutableDictionary
                {
                    { (NSString) "fa", (NSString) data }
                };

                var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(0.25, false);
                var request = UNNotificationRequest.FromIdentifier(data, content, trigger);
                UNUserNotificationCenter.Current.Delegate = new NotificationDelegate();

                UNUserNotificationCenter.Current.AddNotificationRequest(request, (err) =>
                {
                    if (err != null)
                    {
                        Logging.warn("Local notification add request failed");
                        Logging.warn(err.ToString());
                    }
                });
            });
        }

        static void handleNotificationReceived(object sender, OneSignalSDK.DotNet.Core.Notifications.NotificationWillDisplayEventArgs e)
        {
            try
            {
                e.PreventDefault();

                if (OfflinePushMessages.fetchPushMessages(true, true))
                {
                    //OneSignal.Current.ClearAndroidOneSignalNotifications();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logging.error("Exception occured in handleNotificationReceived: {0}", ex);
            }
            e.Notification.display();
        }

        static void handleNotificationOpened(object sender, OneSignalSDK.DotNet.Core.Notifications.NotificationClickedEventArgs e)
        {
            try
            {
                if (e.Notification.AdditionalData.ContainsKey("fa"))
                {
                    var fa = e.Notification.AdditionalData["fa"];
                    if (fa != null)
                    {
                        App.startingScreen = Convert.ToString(fa);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.error("Exception occured in handleNotificationOpened: {0}", ex);
            }
        }
    }
}
