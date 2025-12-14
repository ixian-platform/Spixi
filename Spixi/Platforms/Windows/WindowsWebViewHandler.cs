using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using SPIXI;
using System.Text.RegularExpressions;

namespace Spixi.Platforms.Windows
{
    public class WindowsWebViewHandler : WebViewHandler
    {
        protected override void ConnectHandler(WebView2 platformView)
        {
            base.ConnectHandler(platformView);

            void AttachWebResourceRequested(CoreWebView2 core)
            {
                // Add filter for all resources
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

                // Prevent multiple subscriptions
                core.WebResourceRequested -= CoreWebView2_WebResourceRequested;
                core.WebResourceRequested += CoreWebView2_WebResourceRequested;
            }

            if (platformView.CoreWebView2 != null)
            {
                AttachWebResourceRequested(platformView.CoreWebView2);
            }
            else
            {
                platformView.CoreWebView2Initialized += (s, e) =>
                {
                    if (platformView.CoreWebView2 != null)
                        AttachWebResourceRequested(platformView.CoreWebView2);
                };
            }
        }

        private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var uri = args.Request.Uri;
            if (!Utils.IsAllowedURL(uri))
            {
                // Block by providing an empty response
                var env = (sender as CoreWebView2)?.Environment;
                if (env != null)
                    args.Response = env.CreateWebResourceResponse(null, 403, "Blocked", "");
            }
        }
    }
}