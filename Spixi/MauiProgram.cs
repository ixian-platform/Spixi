#if ANDROID
    using Android.App;
    using Android.OS;
#endif

using CommunityToolkit.Maui;
using IXICore.Meta;
using Microsoft.Maui.Controls.Compatibility.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace Spixi;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCompatibility()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers((handlers) =>
            {
#if ANDROID
                //handlers.AddHandler(typeof(WebView), typeof(Spixi.Platforms.Android.Renderers.MyWebViewHandler));
                handlers.AddCompatibilityRenderer(typeof(WebView), typeof(Spixi.Platforms.Android.Renderers.SpixiWebviewRenderer2));
#endif

#if IOS
                handlers.AddHandler(typeof(WebView), typeof(Spixi.Platforms.iOS.iOSWebViewHandler));
#endif
            })
            .ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android =>
                {
                    // Shutdown logic
                    android.OnDestroy(async (activity) =>
                    {
                        if (!activity.IsChangingConfigurations)
                        {
                            Logging.info("Android OnDestroy real exit, shutting down Node");
                            await App.Shutdown();
                        }
                        else
                        {
                            Logging.info("Android OnDestroy ignored due to IsChangingConfigurations=true");
                        }
                    });

                    // Restart logic when activity comes back to foreground
                    android.OnResume((activity) =>
                    {
                        Logging.info("Android OnResume - ensuring Node is running");
                        App.isInForeground = true;
                        App.EnsureNodeRunning();
                    });
                });
#endif

#if IOS
                events.AddiOS(ios =>
                {
                    ios.WillTerminate(async (app) =>
                    {
                        Logging.info("iOS WillTerminate shutting down Node");
                        await App.Shutdown();
                    });

                    ios.OnActivated((app) =>
                    {
                        Logging.info("iOS OnActivated ensuring Node is running");
                        App.isInForeground = true;
                        App.EnsureNodeRunning();
                    });
                });
#endif

#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnClosed(async (window, args) =>
                    {
                        Logging.info("Windows OnWindowClosed - shutting down Node");
                        await App.Shutdown();
                    });

                    windows.OnWindowCreated((window) =>
                    {
                        Logging.info("Windows OnWindowCreated - ensuring Node is running");
                        App.isInForeground = true;
                        App.EnsureNodeRunning();
                    });
                });
#endif
            });

        return builder.Build();
    }
}
