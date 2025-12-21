using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using IXICore.Meta;
using Plugin.Fingerprint;
using SPIXI;
using SPIXI.Interfaces;
using SPIXI.Lang;
using View = Android.Views.View;
namespace Spixi;

[Activity(Label = "Spixi",
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_round_launcher",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    SupportsPictureInPicture = true,
    ResizeableActivity = true,
    LaunchMode = LaunchMode.SingleInstance,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                        ConfigChanges.Orientation |
                        ConfigChanges.UiMode |
                        ConfigChanges.ScreenLayout |
                        ConfigChanges.SmallestScreenSize |
                        ConfigChanges.Density,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : MauiAppCompatActivity
{
    public const int PickImageId = 1000;
    public const int SaveFileId = 1001;
    public string SaveFilePath { get; set; }

    public TaskCompletionSource<SpixiImageData> PickImageTaskCompletionSource { set; get; }
    internal static MainActivity Instance { get; private set; }
    protected override void OnCreate(Bundle? bundle)
    {
        Instance = this;

        base.OnCreate(bundle);

        CrossFingerprint.SetCurrentActivityResolver(() => this);

        SpixiLocalization.addCustomString("Platform", "Xamarin-Droid");

        // Opt into edge-to-edge drawing
        WindowCompat.SetDecorFitsSystemWindows(Window, false);
        var rootView = FindViewById(Android.Resource.Id.Content);

        if (rootView != null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(rootView, new InsetsListener());
            SPlatformUtils.setEdgeToEdge();
        }

        if (ContextCompat.CheckSelfPermission(Instance, Manifest.Permission.Camera) != Permission.Granted)
        {           
            Permissions.RequestAsync<Permissions.Camera>();
            //Permissions.RequestAsync<Permissions.Microphone>();
            //Permissions.RequestAsync<Permissions.Media>();
        }
        Permissions.RequestAsync<Permissions.StorageWrite>();

        handleNotificationIntent(Intent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? intent)
    {
        base.OnActivityResult(requestCode, resultCode, intent);

        if (requestCode == PickImageId)
        {
            if ((resultCode == Result.Ok) && (intent != null))
            {
                Android.Net.Uri uri = intent.Data;

                SpixiImageData spixi_img_data = new SpixiImageData() { name = Path.GetFileName(uri.Path), path = uri.Path, stream = ContentResolver.OpenInputStream(uri) };

                // Set the Stream as the completion of the Task
                PickImageTaskCompletionSource.SetResult(spixi_img_data);
            }
            else
            {
                PickImageTaskCompletionSource.SetResult(null);
            }
        }
        else if (requestCode == SaveFileId && resultCode == Result.Ok && intent != null)
        {
            Android.Net.Uri? uri = intent.Data;
            if (uri != null)
            {
                SaveFileToUri(uri, SaveFilePath);
            }
        }
    }
    private void SaveFileToUri(Android.Net.Uri uri, string filePath)
    {
        try
        {
            using (var inputStream = File.OpenRead(filePath))
            using (var outputStream = ContentResolver.OpenOutputStream(uri))
            {
                inputStream.CopyTo(outputStream);
            }
        }
        catch (Exception ex)
        {
            Logging.error($"Error saving file: {ex.Message}");
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(500);
            handleNotificationIntent(intent);
        });
    }

    void handleNotificationIntent(Intent? intent)
    {
        if (intent?.Extras != null && intent.Extras.ContainsKey("fa"))
        {
            string? chatId = intent.Extras.GetString("fa");
            if (!string.IsNullOrEmpty(chatId))
            {
                App.startingScreen = chatId;
                HomePage.Instance().updateScreen();
            }
        }
    }

    // Fix Edge to Edge
    private class InsetsListener : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(View? v, WindowInsetsCompat? insets)
        {
            // Get system bars (status + navigation) and IME (keyboard) insets
            var sysInsets = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
            var imeInsets = insets.GetInsets(WindowInsetsCompat.Type.Ime());

            if (v is ViewGroup vg)
            {
                // Apply top padding for status bar, bottom padding for keyboard
                vg.SetPadding(0, sysInsets.Top, 0, Math.Max(imeInsets.Bottom, sysInsets.Bottom));
            }

            return WindowInsetsCompat.Consumed; // We've handled insets manually
        }

        // Optional override for older Android versions
        public WindowInsets? OnApplyWindowInsets(View? v, WindowInsets? insets)
        {
            return v?.OnApplyWindowInsets(insets);
        }
    }

    // Picture in picture
    /*protected override void OnUserLeaveHint()
    {
        base.OnUserLeaveHint();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            //App.isInForeground = false;
            //Node.pause();
            var aspectRatio = new Android.Util.Rational(16, 9);
            var pipParams = new PictureInPictureParams.Builder()
                .SetAspectRatio(aspectRatio)
                .Build();

            EnterPictureInPictureMode(pipParams);
        }
    }*/
}
