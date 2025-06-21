using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Core.App;
using IXICore.Meta;
using IXICore.Streaming;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core.Debug;
using SPIXI;
using OneSignalNative = Com.OneSignal.Android.OneSignal;

namespace Spixi
{
    public class SPushService
    {
        const string channelId = "default";
        const string channelName = "Default";
        const string channelDescription = "Spixi local notifications channel.";
        const int pendingIntentId = 0;

        static bool channelInitialized = false;
        static int messageId = -1;
        static NotificationManager manager;
        public const string TitleKey = "title";
        public const string MessageKey = "message";

        private static bool isInitializing = false;
        private static bool isInitialized = false;

        public static void initialize()
        {
            if (isInitializing
                || isInitialized)
            {
                return;
            }

            isInitializing = true;

            OneSignal.Debug.LogLevel = LogLevel.WARN;
            OneSignal.Debug.AlertLevel = LogLevel.NONE;

            OneSignal.Initialize(SPIXI.Meta.Config.oneSignalAppId);

            OneSignal.Notifications.Clicked += handleNotificationOpened;
            OneSignal.Notifications.WillDisplay += handleNotificationReceived;

            // RequestPermissionAsync will show the notification permission prompt.
            OneSignal.Notifications.RequestPermissionAsync(true);

            isInitialized = true;
        }

        public static void setTag(string tag)
        {
            OneSignal.User.AddTag("ixi", tag);
        }

        public static void clearNotifications()
        {
            var notificationManager = NotificationManagerCompat.From(Android.App.Application.Context);
            notificationManager.CancelAll();

            try
            {
                if (isInitialized)
                {
                    OneSignalNative.Notifications.ClearAllNotifications();
                }
                else
                {
                    Logging.warn("Cannot clear notifications, OneSignal is not initialized yet.");
                }
            }
            catch (Exception e)
            {
                Logging.error("Exception while clearing all notifications: {0}.", e);
            }
        }

        public static void showLocalNotification(string title, string message, string data)
        {
            if (!channelInitialized)
            {
                CreateNotificationChannel();
            }

            messageId++;

            Intent intent = new Intent(Android.App.Application.Context, typeof(MainActivity));
            intent.PutExtra(TitleKey, title);
            intent.PutExtra(MessageKey, message);
            intent.PutExtra("fa", data);

            PendingIntent pendingIntent = PendingIntent.GetActivity(Android.App.Application.Context, pendingIntentId, intent, PendingIntentFlags.Immutable);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(Android.App.Application.Context, channelId)
                .SetContentIntent(pendingIntent)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetPriority(1)
                .SetLargeIcon(BitmapFactory.DecodeResource(Android.App.Application.Context.Resources, Resource.Drawable.statusicon))
                .SetSmallIcon(Resource.Drawable.statusicon)
                .SetDefaults((int)NotificationDefaults.Sound | (int)NotificationDefaults.Vibrate);

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                builder.SetGroup("NEWMSGL");
            }

            var notification = builder.Build();
            manager.Notify(messageId, notification);
        }

        static void CreateNotificationChannel()
        {
            manager = (NotificationManager)Android.App.Application.Context.GetSystemService("notification");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                NotificationChannelGroup group = new("NEWMSGL", "New Message");
                manager.CreateNotificationChannelGroup(group);

                var channelNameJava = new Java.Lang.String(channelName);
                var channel = new NotificationChannel(channelId, channelNameJava, NotificationImportance.High)
                {
                    Description = channelDescription,
                    Group = "NEWMSGL"
                };
                manager.CreateNotificationChannel(channel);
            }

            channelInitialized = true;
        }

        static void handleNotificationReceived(object sender, OneSignalSDK.DotNet.Core.Notifications.NotificationWillDisplayEventArgs e)
        {
            try
            {
                e.PreventDefault();

                if (OfflinePushMessages.fetchPushMessages(true, true))
                {
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
