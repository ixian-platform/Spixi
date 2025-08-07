using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Compatibility.Hosting;
using Microsoft.Maui.Handlers;

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
            });

		return builder.Build();
	}
}
