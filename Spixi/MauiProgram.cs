#if ANDROID
    using Android.App;
    using Android.OS;
#endif

using CommunityToolkit.Maui;
using IXICore.Meta;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls.Compatibility.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace Spixi;

public static class MauiProgram
{
    public static int ActiveActivityCount;
    private static CancellationTokenSource _shutdownCts;

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
                    android.OnCreate((activity, bundle) =>
                    {
                        Interlocked.Increment(ref ActiveActivityCount);
                        Logging.info($"{activity.GetType().Name} created. Active count = {ActiveActivityCount}");

                        // Cancel any pending shutdown if a new activity starts
                        _shutdownCts?.Cancel();
                    });

                    android.OnDestroy((activity) =>
                    {
                        var count = Math.Max(0, Interlocked.Decrement(ref ActiveActivityCount));
                        Logging.info($"{activity.GetType().Name} destroyed. Active count = {count}");

                        if (count <= 0 && !activity.IsChangingConfigurations)
                        {
                            Logging.info("Last activity destroyed - scheduling delayed shutdown");

                            _shutdownCts = new CancellationTokenSource();
                            var token = _shutdownCts.Token;

                            Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(1000, token); // 1 second debounce
                                    if (!token.IsCancellationRequested)
                                    {
                                        Logging.info("No new activity started - shutting down Node");
                                        await App.Shutdown();
                                    }
                                }
                                catch (TaskCanceledException)
                                {
                                    Logging.info("Shutdown canceled - new activity started");
                                }
                            }, token);
                        }
                        else
                        {
                            Logging.info($"OnDestroy ignored - remaining activities: {count}, " +
                                         $"IsChangingConfigurations={activity.IsChangingConfigurations}");
                        }
                    });

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
