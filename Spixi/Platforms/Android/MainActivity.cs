using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;
using Plugin.Fingerprint;
using SPIXI;
using SPIXI.Interfaces;
using SPIXI.Lang;
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
                        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public const int PickImageId = 1000;
    public const int SaveFileId = 1001;
    public string SaveFilePath { get; set; }

    public TaskCompletionSource<SpixiImageData> PickImageTaskCompletionSource { set; get; }
    internal static MainActivity Instance { get; private set; }
    protected override void OnCreate(Bundle bundle)
    {
        Instance = this;

        base.OnCreate(bundle);

        CrossFingerprint.SetCurrentActivityResolver(() => this);

        SpixiLocalization.addCustomString("Platform", "Xamarin-Droid");

        if (ContextCompat.CheckSelfPermission(MainActivity.Instance, Manifest.Permission.Camera) != Permission.Granted)
        {           
            Permissions.RequestAsync<Permissions.Camera>();
            //Permissions.RequestAsync<Permissions.Microphone>();
            //Permissions.RequestAsync<Permissions.Media>();
        }
        Permissions.RequestAsync<Permissions.StorageWrite>();

    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent intent)
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
            System.Diagnostics.Debug.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);

        // Handle local notification tap
        string fa = intent.GetStringExtra("fa");
        if (fa != null)
        {
            HomePage.Instance().onChat(fa, null);
        }
    }


}
