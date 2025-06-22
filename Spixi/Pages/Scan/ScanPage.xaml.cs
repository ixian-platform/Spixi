using SPIXI.Lang;
using SPIXI.Meta;
using System.Web;

namespace SPIXI;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ScanPage : SpixiContentPage
{
    public event EventHandler<SPIXI.EventArgs<string>> scanSucceeded;

    private bool allowScanning = true;

    public ScanPage()
	{
		InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        loadPage(webView, "scan.html");
    }

    private void onNavigated(object sender, WebNavigatedEventArgs e)
    {
        // Deprecated due to WPF, use onLoad
    }

    private void onLoad()
    {
    }

    private void onNavigating(object sender, WebNavigatingEventArgs e)
    {
        string current_url = HttpUtility.UrlDecode(e.Url);

        if (onNavigatingGlobal(current_url))
        {
            e.Cancel = true;
            return;
        }

        if (current_url.Equals("ixian:onload", StringComparison.Ordinal))
        {
            onLoad();
        }
        else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
        {
            OnBackButtonPressed();
        }
        else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
        {
            displaySpixiAlert(SpixiLocalization._SL("global-invalid-address-title"), SpixiLocalization._SL("global-invalid-address-text"), SpixiLocalization._SL("global-dialog-ok"));
        }
        else if (current_url.Contains("ixian:qrresult:"))
        {
            try
            {
                string[] split = current_url.Split(new string[] { "ixian:qrresult:" }, StringSplitOptions.None);
                processQRResult(split[1]);
            }
            catch (Exception)
            {

            }
            e.Cancel = true;
            return;
        }
        else
        {
            // Otherwise it's just normal navigation
            e.Cancel = false;
            return;
        }
        e.Cancel = true;

    }

    public void processQRResult(string text)
    {
        if (!allowScanning)
            return;

        string wal = text;
        if (scanSucceeded != null)
        {
            allowScanning = false;
            OnBackButtonPressed();
            scanSucceeded(this, new SPIXI.EventArgs<string>(wal));
        }
    }

    protected override bool OnBackButtonPressed()
    {
        popPageAsync();
        GC.Collect();
        return true;
    }

}