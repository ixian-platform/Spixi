using Microsoft.Maui.Handlers;
using UIKit;
using WebKit;

namespace Spixi.Platforms.iOS
{
    public class iOSWebViewHandler : WebViewHandler
    {
        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);

            platformView.ScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
            platformView.ScrollView.ScrollEnabled = false;
            platformView.ScrollView.Bounces = false;
        }
    }
}
