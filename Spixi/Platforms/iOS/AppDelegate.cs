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
       /* if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
        {
            // Ask the user for permission to get notifications on iOS 10.0+
            UNUserNotificationCenter.Current.RequestAuthorization(
                    UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
                    (approved, error) => { });
        }
        else if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
        {
            // Ask the user for permission to get notifications on iOS 8.0+
            var settings = UIUserNotificationSettings.GetSettingsForTypes(
                    UIUserNotificationType.Alert | UIUserNotificationType.Badge | UIUserNotificationType.Sound,
                    new NSSet());

            UIApplication.SharedApplication.RegisterUserNotificationSettings(settings);
        }*/
        UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalMinimum);

        prepareStorage();

     //   NSNotificationCenter.DefaultCenter.AddObserver(MPMusicPlayerController.VolumeDidChangeNotification, onVolumeChanged);

        SpixiLocalization.addCustomString("Platform", "Xamarin-iOS");

    //    LoadApplication(App.Instance());

    //    prepareBackgroundService();

        return base.FinishedLaunching(app, options);
    }

    private void prepareStorage()
    {
        string source_html = Path.Combine(NSBundle.MainBundle.BundlePath, "html");
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
