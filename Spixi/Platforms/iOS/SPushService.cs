﻿using Foundation;
using IXICore.Meta;
using IXICore.Streaming;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core.Debug;
using SPIXI;
using UIKit;
using UserNotifications;
using OneSignalNative = Com.OneSignal.iOS.OneSignal;

namespace Spixi
{
    public class SPushService
    {
        private static bool isInitializing = false;
        private static bool isInitialized = false;

        private static bool clearNotificationsAfterInit = false;
        public class NotificationDelegate : UNUserNotificationCenterDelegate
        {
            public override void DidReceiveNotificationResponse(
                UNUserNotificationCenter center,
                UNNotificationResponse response,
                Action completionHandler)
            {
                try
                {
                    var userInfo = response.Notification.Request.Content.UserInfo;

                    if (userInfo.ContainsKey((NSString)"fa"))
                    {
                        var fa = userInfo[(NSString)"fa"];
                        if (fa != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                App.startingScreen = Convert.ToString(fa);
                                HomePage.Instance().updateScreen();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.error("Exception occured in DidReceiveNotificationResponse: {0}", ex);
                }
                finally
                {
                    completionHandler();
                }
            }

            public override void WillPresentNotification(
                UNUserNotificationCenter center,
                UNNotification notification,
                Action<UNNotificationPresentationOptions> completionHandler)
            {
                // Show the notification even when the app is in the foreground
                completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound);
            }
        }

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

            UNUserNotificationCenter.Current.Delegate = new NotificationDelegate();

            OneSignal.Notifications.Clicked += handleNotificationOpened;
            OneSignal.Notifications.WillDisplay += handleNotificationReceived;

            OneSignal.Initialize(SPIXI.Meta.Config.oneSignalAppId);

            OneSignal.Notifications.RequestPermissionAsync(true).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Logging.error("RequestPermissionAsync failed: {0}", task.Exception?.Flatten().InnerException?.Message);
                    return Task.CompletedTask;
                }
                else if (task.IsCanceled)
                {
                    Logging.warn("RequestPermissionAsync was canceled.");
                    return Task.CompletedTask;
                }
                else
                {
                    Logging.info("RequestPermissionAsync succeeded.");
                }

                isInitialized = true;

                if (clearNotificationsAfterInit)
                {
                    clearNotificationsAfterInit = false;
                    clearNotifications();
                }
                return Task.CompletedTask;
            });
        }

        public static void setTag(string tag)
        {
            OneSignal.User.AddTag("ixi", tag);
        }

        public static void clearNotifications()
        {
            if (!isInitialized)
            {
                clearNotificationsAfterInit = true;
                Logging.warn("Cannot clear notifications, OneSignal is not initialized yet.");
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    OneSignalNative.Notifications.ClearAll();
                    UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
                }
                catch (Exception e)
                {
                    Logging.error("Exception while clearing all notifications: {0}.", e);
                }
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

        static void handleNotificationReceived(object? sender, OneSignalSDK.DotNet.Core.Notifications.NotificationWillDisplayEventArgs e)
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

        static void handleNotificationOpened(object? sender, OneSignalSDK.DotNet.Core.Notifications.NotificationClickedEventArgs e)
        {
            try
            {
                if (e.Notification.AdditionalData.ContainsKey("fa"))
                {
                    var fa = e.Notification.AdditionalData["fa"];
                    if (fa != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            App.startingScreen = Convert.ToString(fa);
                            HomePage.Instance().popToRootAsync();
                        });
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
