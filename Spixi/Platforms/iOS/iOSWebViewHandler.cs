using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;
using WebKit;

namespace Spixi.Platforms.iOS
{
    class SecureSchemeHandler : NSObject, IWKUrlSchemeHandler
    {
        public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        {
            var url = urlSchemeTask.Request.Url?.AbsoluteString ?? "";
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                // Block external resources
                urlSchemeTask.DidReceiveResponse(new NSUrlResponse());
                urlSchemeTask.DidReceiveData(NSData.FromArray(Array.Empty<byte>()));
                urlSchemeTask.DidFinish();
                return;
            }
        }

        public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask) { }
    }

    public class iOSWebViewHandler : WebViewHandler
    {
        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);

            platformView.Configuration.SetUrlSchemeHandler(new SecureSchemeHandler(), "file");
            platformView.Configuration.SetUrlSchemeHandler(new SecureSchemeHandler(), "app");

            platformView.ScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
            platformView.ScrollView.ScrollEnabled = false;
            platformView.ScrollView.Bounces = false;
        }
    }
}
