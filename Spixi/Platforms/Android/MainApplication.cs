using Android.App;
using Android.OS;
using Android.Runtime;
using IXICore;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using System;

namespace Spixi;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
        DeviceStorage.PlatformGetAvailableDiskSpace = (path) =>
        {
            var stat = new StatFs(path);
            return Math.Max(stat.AvailableBytes, 0);
        };
    }

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
