using Foundation;
using IXICore.Meta;
using SPIXI.Lang;
using SPIXI.Meta;
using UIKit;

namespace Spixi;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalMinimum);

        prepareStorage();

        SpixiLocalization.addCustomString("Platform", "Xamarin-Mac");

        return base.FinishedLaunching(app, options);
    }

    private void prepareStorage()
    {
        string source_html = Path.Combine(NSBundle.MainBundle.BundlePath, "Contents/Resources/html");
        string dest_html = Path.Combine(Config.spixiUserFolder, "html");

        if (!Directory.Exists(dest_html))
        {
            Directory.CreateDirectory(dest_html);
        }

        prepareSymbolicLinks(new DirectoryInfo(source_html), new DirectoryInfo(dest_html));
    }

    // Cleans up and links contents of the source directory to target directory.
    private static void prepareSymbolicLinks(DirectoryInfo source, DirectoryInfo target)
    {
        var fm = new NSFileManager();
        fm.ChangeCurrentDirectory(target.FullName);

        NSError ns_error = new NSError();

        foreach (DirectoryInfo dir in source.GetDirectories())
        {
            var tmp_path = Path.Combine(target.FullName, dir.Name);
            if (Directory.Exists(tmp_path))
            {
                Directory.Delete(tmp_path, true);
            }
            if (File.Exists(tmp_path))
            {
                File.Delete(tmp_path);
            }
            fm.CreateSymbolicLink(dir.Name, dir.FullName, out ns_error);
        }

        foreach (FileInfo file in source.GetFiles())
        {
            var tmp_path = Path.Combine(target.FullName, file.Name);
            if (Directory.Exists(tmp_path))
            {
                Directory.Delete(tmp_path, true);
            }
            if (File.Exists(tmp_path))
            {
                File.Delete(tmp_path);
            }
            fm.CreateSymbolicLink(file.Name, file.FullName, out ns_error);
        }
    }

    public override void WillTerminate(UIApplication uiApplication)
    {
        IxianHandler.shutdown();
        base.WillTerminate(uiApplication);
    }

}
