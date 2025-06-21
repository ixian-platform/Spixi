using SPIXI;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Net;
using Android.Content;
using Android.Webkit;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AWebView = Android.Webkit.WebView;
using MixedContentHandling = Android.Webkit.MixedContentHandling;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Microsoft.Maui.Platform;
using Android.Annotation;
using IXICore.Meta;
using Android.Views.InputMethods;
using AndroidX.Core.View.InputMethod;
using Android.OS;
using AInputMethods = Android.Views.InputMethods;
using Android.Runtime;

namespace Spixi.Platforms.Android.Renderers;

public class SpixiWebChromeClient : WebChromeClient
{
    WebNavigationResult _navigationResult = WebNavigationResult.Success;
    SpixiWebviewRenderer2 _renderer;
    // Activity activity;

    public SpixiWebChromeClient(SpixiWebviewRenderer2 renderer)//, Activity context)
    {
        if (renderer == null)
            throw new ArgumentNullException("renderer");
        _renderer = renderer;
        //activity = context;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _renderer = null;

    }
    [TargetApi(Value = 21)]
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        request.Grant(request.GetResources());
    }
}

public class SpixiWebViewClient : WebViewClient
{
    WebNavigationResult _navigationResult = WebNavigationResult.Success;
    SpixiWebviewRenderer2 _renderer;

    public SpixiWebViewClient(SpixiWebviewRenderer2 renderer) //: base(renderer)
    {
        if (renderer == null)
            throw new ArgumentNullException("renderer");
        _renderer = renderer;
    }

    public SpixiWebViewClient(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {

    }

    // Hackish solution to the Xamarin ERR_UNKNOWN_URL_SCHEME issue plaguing the latest releases
    // TODO: find a better way to handle the Navigating event without triggering a page load
    [Obsolete]
    public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView view, string url)
    {
        var args = new WebNavigatingEventArgs(WebNavigationEvent.NewPage, new UrlWebViewSource { Url = url }, url);

        try
        {
            _renderer.ElementController.SendNavigating(args);
        }
        catch (Exception e)
        {
            Logging.error("Exception in should override url loading {0}", e);
        }

        if (args.Cancel)
        {
            return true;
        }
        return false;
    }

    public override void OnPageFinished(global::Android.Webkit.WebView view, string url)
    {
        if (_renderer.Element == null)// || url == AssetBaseUrl)
        {
            base.OnPageFinished(view, url);

            return;
        }
        var source = new UrlWebViewSource { Url = url };
        var args = new WebNavigatedEventArgs(WebNavigationEvent.NewPage, source, url, _navigationResult);
        _renderer.ElementController.SendNavigated(args);
        _renderer.UpdateCanGoBackForward();

        base.OnPageFinished(view, url);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing
            && _renderer != null)
        {
            _renderer = null;
        }
    }
}

public class SpixiWebview(Context context) : AWebView(context), InputConnectionCompat.IOnCommitContentListener
{
    public bool OnCommitContent(InputContentInfoCompat inputContentInfo, int flags, Bundle? opts)
    {
        bool permission_requested = false;
        bool processed = false;

        // read and display inputContentInfo asynchronously
        if (Build.VERSION.SdkInt >= BuildVersionCodes.NMr1
            && (flags & InputConnectionCompat.InputContentGrantReadUriPermission) != 0)
        {
            try
            {
                inputContentInfo.RequestPermission();
                permission_requested = true;
            }
            catch (Exception)
            {
                return processed;
            }
        }

        if (inputContentInfo.LinkUri != null)
        {
            string url = inputContentInfo.LinkUri.ToString();

            Page p = App.Current.MainPage.Navigation.NavigationStack.Last();
            if (p != null && p.GetType() == typeof(SingleChatPage))
            {
                string rx_pattern = @"^https://[A-Za-z0-9]+\.(tenor|giphy)\.com/[A-Za-z0-9_/=%\?\-\.\&]+$";

                if (Regex.IsMatch(url, rx_pattern))
                {
                    ((SingleChatPage)p).onSend(url);
                    processed = true;
                }
            }
        }
        else
        {
            Logging.error("Error adding keyboard content, LinkUri is null");
        }

        if (permission_requested)
        {
            inputContentInfo.ReleasePermission();
        }

        return processed;
    }

    public override IInputConnection OnCreateInputConnection(EditorInfo? outAttrs)
    {
        var inputConnection = base.OnCreateInputConnection(outAttrs);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            outAttrs.ImeOptions |= AInputMethods.ImeFlags.NoPersonalizedLearning;
        }

        if (inputConnection != null)
        {
            EditorInfoCompat.SetContentMimeTypes(outAttrs, new string[] { "image/gif" });
            inputConnection = InputConnectionCompat.CreateWrapper(inputConnection, outAttrs, this);
        }

        return inputConnection;
    }
}

public class SpixiWebviewRenderer2 : ViewRenderer<Microsoft.Maui.Controls.WebView, SpixiWebview>, IWebViewDelegate
{
    public const string AssetBaseUrl = "file:///android_asset/";

    WebNavigationEvent _eventState;
    SpixiWebViewClient _webViewClient;
    SpixiWebChromeClient _webChromeClient;
    bool _isDisposed = false;
    protected internal IWebViewController ElementController => Element;
    protected internal bool IgnoreSourceChanges { get; set; }
    protected internal string UrlCanceled { get; set; }

    public SpixiWebviewRenderer2(Context context) : base(context)
    {
        AutoPackage = false;
    }

    public void LoadHtml(string html, string baseUrl)
    {
        _eventState = WebNavigationEvent.NewPage;
        Control.LoadDataWithBaseURL(baseUrl ?? AssetBaseUrl, html, "text/html", "UTF-8", null);
    }

    public void LoadUrl(string url)
    {
        LoadUrl(url, true);
    }

    void LoadUrl(string url, bool fireNavigatingCanceled)
    {
        if (!fireNavigatingCanceled || !SendNavigatingCanceled(url))
        {
            _eventState = WebNavigationEvent.NewPage;
            /*if (url != null && !url.StartsWith('/') && !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                // URLs like "index.html" can't possibly load, so try "file:///android_asset/index.html"
                url = AssetBaseUrl + url;
            }*/
            Control.LoadUrl(url);
        }
    }

    protected internal bool SendNavigatingCanceled(string url)
    {
        if (Element == null || string.IsNullOrWhiteSpace(url))
            return true;

        if (url == AssetBaseUrl)
            return false;

        var args = new WebNavigatingEventArgs(_eventState, new UrlWebViewSource { Url = url }, url);
        SyncNativeCookies(url);
        ElementController.SendNavigating(args);
        UpdateCanGoBackForward();
        UrlCanceled = args.Cancel ? null : url;
        return args.Cancel;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (disposing)
        {
            try
            {
                if (Control != null)
                {
                    Control.StopLoading();
                    Control.ClearHistory();
                    Control.ClearCache(true);
                    Control.RemoveAllViews();
                    Control.Destroy();
                }

                _webViewClient?.Dispose();
                _webChromeClient?.Dispose();
                _webViewClient = null;
                _webChromeClient = null;

                if (ElementController != null)
                {
                    ElementController.EvalRequested -= OnEvalRequested;
                    ElementController.EvaluateJavaScriptRequested -= OnEvaluateJavaScriptRequested;
                    ElementController.GoBackRequested -= OnGoBackRequested;
                    ElementController.GoForwardRequested -= OnGoForwardRequested;
                    ElementController.ReloadRequested -= OnReloadRequested;
                }
            }
            catch (Exception ex)
            {
                Logging.error("Error during WebView Dispose: " + ex.ToString());
            }
        }

        base.Dispose(disposing);
    }

    [PortHandler]
    protected virtual SpixiWebViewClient GetWebViewClient()
    {
        return new SpixiWebViewClient(this);
    }

    [PortHandler]
    protected virtual SpixiWebChromeClient GetFormsWebChromeClient()
    {
        return new SpixiWebChromeClient(this);
    }

    protected override Size MinimumSize()
    {
        return new Size(Context.ToPixels(40), Context.ToPixels(40));
    }

    [PortHandler]
    protected override SpixiWebview CreateNativeControl()
    {
        var webView = new SpixiWebview(Context);
        webView.Settings.SetSupportMultipleWindows(true);
        return webView;
    }

    internal WebNavigationEvent GetCurrentWebNavigationEvent()
    {
        return _eventState;
    }

    protected override void OnElementChanged(ElementChangedEventArgs<Microsoft.Maui.Controls.WebView> e)
    {
        base.OnElementChanged(e);

        if (Control == null)
        {
            var webView = CreateNativeControl();
#pragma warning disable 618 // This can probably be replaced with LinearLayout(LayoutParams.MatchParent, LayoutParams.MatchParent); just need to test that theory
#pragma warning disable CA1416, CA1422 // Validate platform compatibility
            webView.LayoutParameters = new global::Android.Widget.AbsoluteLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent, 0, 0);
#pragma warning restore CA1416, CA1422 // Validate platform compatibility
#pragma warning restore 618

            _webViewClient = GetWebViewClient();
            webView.SetWebViewClient(_webViewClient);

            _webChromeClient = GetFormsWebChromeClient();
            webView.SetWebChromeClient(_webChromeClient);
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.AllowFileAccess = true;
            webView.Settings.AllowFileAccessFromFileURLs = true;
            webView.Settings.MediaPlaybackRequiresUserGesture = false;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                webView.Settings.SetAppCacheEnabled(false);
            }

            webView.Settings.CacheMode = CacheModes.NoCache;
            webView.Settings.DatabaseEnabled = false;
            webView.Settings.DomStorageEnabled = false;

            webView.FocusChange += (sender, args) =>
            {
                if (args.HasFocus)
                {
                    webView.RequestFocus();
                }
            };

            SetNativeControl(webView);
        }

        if (e.OldElement != null)
        {
            var oldElementController = e.OldElement as IWebViewController;
            oldElementController.EvalRequested -= OnEvalRequested;
            oldElementController.EvaluateJavaScriptRequested -= OnEvaluateJavaScriptRequested;
            oldElementController.GoBackRequested -= OnGoBackRequested;
            oldElementController.GoForwardRequested -= OnGoForwardRequested;
            oldElementController.ReloadRequested -= OnReloadRequested;
        }

        if (e.NewElement != null)
        {
            var newElementController = e.NewElement as IWebViewController;
            newElementController.EvalRequested += OnEvalRequested;
            newElementController.EvaluateJavaScriptRequested += OnEvaluateJavaScriptRequested;
            newElementController.GoBackRequested += OnGoBackRequested;
            newElementController.GoForwardRequested += OnGoForwardRequested;
            newElementController.ReloadRequested += OnReloadRequested;

            UpdateMixedContentMode();
            UpdateEnableZoomControls();
            UpdateDisplayZoomControls();
        }

        Load();
    }

    protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        base.OnElementPropertyChanged(sender, e);

        switch (e.PropertyName)
        {
            case "Source":
                Load();
                break;
            case "MixedContentMode":
                UpdateMixedContentMode();
                break;
            case "EnableZoomControls":
                UpdateEnableZoomControls();
                break;
            case "DisplayZoomControls":
                UpdateDisplayZoomControls();
                break;
        }
    }

    [PortHandler]
    HashSet<string> _loadedCookies = new HashSet<string>();

    [PortHandler]
    Uri CreateUriForCookies(string url)
    {
        if (url == null)
            return null;

        Uri uri;

        if (url.Length > 2000)
            url = url.Substring(0, 2000);

        if (Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            if (String.IsNullOrWhiteSpace(uri.Host))
                return null;

            return uri;
        }

        return null;
    }

    [PortHandler]
    CookieCollection GetCookiesFromNativeStore(string url)
    {
        CookieContainer existingCookies = new CookieContainer();
        var cookieManager = CookieManager.Instance;
        var currentCookies = cookieManager.GetCookie(url);
        var uri = CreateUriForCookies(url);

        if (currentCookies != null)
        {
            foreach (var cookie in currentCookies.Split(';'))
                existingCookies.SetCookies(uri, cookie);
        }

        return existingCookies.GetCookies(uri);
    }

    [PortHandler]
    void InitialCookiePreloadIfNecessary(string url)
    {
        var myCookieJar = Element.Cookies;
        if (myCookieJar == null)
            return;

        var uri = CreateUriForCookies(url);
        if (uri == null)
            return;

        if (!_loadedCookies.Add(uri.Host))
            return;

        var cookies = myCookieJar.GetCookies(uri);

        if (cookies != null)
        {
            var existingCookies = GetCookiesFromNativeStore(url);
            foreach (Cookie cookie in existingCookies)
            {
                if (cookies[cookie.Name] == null)
                    myCookieJar.Add(cookie);
            }
        }
    }

    [PortHandler]
    internal void SyncNativeCookiesToElement(string url)
    {
        var myCookieJar = Element.Cookies;
        if (myCookieJar == null)
            return;

        var uri = CreateUriForCookies(url);
        if (uri == null)
            return;

        var cookies = myCookieJar.GetCookies(uri);
        var retrieveCurrentWebCookies = GetCookiesFromNativeStore(url);

        foreach (Cookie cookie in cookies)
        {
            var nativeCookie = retrieveCurrentWebCookies[cookie.Name];
            if (nativeCookie == null)
                cookie.Expired = true;
            else
                cookie.Value = nativeCookie.Value;
        }

        SyncNativeCookies(url);
    }

    [PortHandler]
    void SyncNativeCookies(string url)
    {
        var uri = CreateUriForCookies(url);
        if (uri == null)
            return;

        var myCookieJar = Element.Cookies;
        if (myCookieJar == null)
            return;

        InitialCookiePreloadIfNecessary(url);
        var cookies = myCookieJar.GetCookies(uri);
        if (cookies == null)
            return;

        var retrieveCurrentWebCookies = GetCookiesFromNativeStore(url);

        var cookieManager = CookieManager.Instance;
        cookieManager.SetAcceptCookie(true);
        for (var i = 0; i < cookies.Count; i++)
        {
            var cookie = cookies[i];
            var cookieString = cookie.ToString();
            cookieManager.SetCookie(cookie.Domain, cookieString);
        }

        foreach (Cookie cookie in retrieveCurrentWebCookies)
        {
            if (cookies[cookie.Name] != null)
                continue;

            var cookieString = $"{cookie.Name}=; max-age=0;expires=Sun, 31 Dec 2017 00:00:00 UTC";
            cookieManager.SetCookie(cookie.Domain, cookieString);
        }
    }

    void Load()
    {
        if (IgnoreSourceChanges)
            return;

        Element.Source?.Load(this);

        UpdateCanGoBackForward();
    }

    void OnEvalRequested(object sender, EvalRequested eventArg)
    {
        LoadUrl("javascript:" + eventArg.Script, false);
    }

    Task<string> OnEvaluateJavaScriptRequested(string script)
    {
        var jsr = new JavascriptResult();

        Control.EvaluateJavascript(script, jsr);

        return jsr.JsResult;
    }

    void OnGoBackRequested(object sender, EventArgs eventArgs)
    {
        if (Control.CanGoBack())
        {
            _eventState = WebNavigationEvent.Back;
            Control.GoBack();
        }

        UpdateCanGoBackForward();
    }

    void OnGoForwardRequested(object sender, EventArgs eventArgs)
    {
        if (Control.CanGoForward())
        {
            _eventState = WebNavigationEvent.Forward;
            Control.GoForward();
        }

        UpdateCanGoBackForward();
    }

    void OnReloadRequested(object sender, EventArgs eventArgs)
    {
        SyncNativeCookies(Control.Url?.ToString());
        _eventState = WebNavigationEvent.Refresh;
        Control.Reload();
    }

    [PortHandler]
    protected internal void UpdateCanGoBackForward()
    {
        if (Element == null || Control == null)
            return;
        ElementController.CanGoBack = Control.CanGoBack();
        ElementController.CanGoForward = Control.CanGoForward();
    }

    [PortHandler]
    void UpdateMixedContentMode()
    {
        if (Control != null)
        {
            Control.Settings.MixedContentMode = (MixedContentHandling)Element.OnThisPlatform().MixedContentMode();
        }
    }

    [PortHandler]
    void UpdateEnableZoomControls()
    {
        var value = Element.OnThisPlatform().ZoomControlsEnabled();
        Control.Settings.SetSupportZoom(value);
        Control.Settings.BuiltInZoomControls = value;
    }

    [PortHandler]
    void UpdateDisplayZoomControls()
    {
        Control.Settings.DisplayZoomControls = Element.OnThisPlatform().ZoomControlsDisplayed();
    }

    class JavascriptResult : Java.Lang.Object, IValueCallback
    {
        TaskCompletionSource<string> source;
        public Task<string> JsResult { get { return source.Task; } }

        public JavascriptResult()
        {
            source = new TaskCompletionSource<string>();
        }

        public void OnReceiveValue(Java.Lang.Object result)
        {
            string json = ((Java.Lang.String)result).ToString();
            source.SetResult(json);
        }
    }
}
