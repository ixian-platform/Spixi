using Foundation;
using System;
using System.Runtime.InteropServices;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using WebKit;

namespace Spixi.Platforms.iOS
{
    class SecureNavigationDelegate : MauiWebViewNavigationDelegate
    {
        public SecureNavigationDelegate(WebViewHandler handler) : base(handler)
        {
        }
        
        public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
        {
            var url = navigationAction.Request.Url?.AbsoluteString ?? "";
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                decisionHandler(WKNavigationActionPolicy.Cancel);
                return;
            }

            base.DecidePolicy(webView, navigationAction, decisionHandler);
        }
    }

    public class iOSWebViewHandler : WebViewHandler
    {
        static bool _swizzled;

        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);

            //var previousDelegate = platformView.NavigationDelegate;
            platformView.NavigationDelegate = new SecureNavigationDelegate(this);

            platformView.ScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
            platformView.ScrollView.ScrollEnabled = false;
            platformView.ScrollView.Bounces = false;

            // Enable inspection for debugging
            //platformView.Inspectable = true;

            // Remove the iOS keyboard accessory bar (up/down arrows and checkmark)
            var assistantItem = platformView.InputAssistantItem;
            if (assistantItem != null)
            {
                assistantItem.LeadingBarButtonGroups = Array.Empty<UIBarButtonItemGroup>();
                assistantItem.TrailingBarButtonGroups = Array.Empty<UIBarButtonItemGroup>();
            }
 
            // Try swizzling WKContentView's inputAccessoryView to return nil
            TrySwizzleInputAccessoryView();
        }

        protected override WKWebView CreatePlatformView()
        {
            var platformView = base.CreatePlatformView();
            WKContentRuleListStore.DefaultStore.CompileContentRuleList("ContentBlockingRules",
                """
                [
                    {
                        "trigger": { "url-filter": ".*" }, 
                        "action": { "type": "block" }
                    },
                    {
                        "trigger": { "url-filter": "file://.*" },
                        "action": { "type": "ignore-previous-rules" }
                    },
                    {
                        "trigger": { "url-filter": "https://[A-Za-z0-9]+\\.tenor\\.com/[A-Za-z0-9_/=%\\?\\-\\.\\&]+" },
                        "action": { "type": "ignore-previous-rules" }
                    },
                    {
                        "trigger": { "url-filter": "https://[A-Za-z0-9]+\\.giphy\\.com/[A-Za-z0-9_/=%\\?\\-\\.\\&]+" },
                        "action": { "type": "ignore-previous-rules" }
                    }
                ]
                """,
                (compiledRuleList, error) =>
                {
                    if (error == null)
                    {
                        platformView.Configuration.UserContentController.AddContentRuleList(compiledRuleList);
                    }
                });
            return platformView;
        }


        // ObjC runtime imports for method swizzling
        [DllImport("/usr/lib/libobjc.A.dylib")]
        static extern IntPtr objc_getClass(string name);
 
        [DllImport("/usr/lib/libobjc.A.dylib")]
        static extern IntPtr sel_registerName(string name);
 
        [DllImport("/usr/lib/libobjc.A.dylib")]
        static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr sel);
 
        [DllImport("/usr/lib/libobjc.A.dylib")]
        static extern IntPtr method_getImplementation(IntPtr method);
 
        [DllImport("/usr/lib/libobjc.A.dylib")]
        static extern IntPtr method_setImplementation(IntPtr method, IntPtr imp);
 
        // Delegates must match Objective-C IMP signature: id (*IMP)(id, SEL, ...)
        // We return nil (IntPtr.Zero) for inputAccessoryView
        delegate IntPtr InputAccessoryViewDelegate(IntPtr self, IntPtr _cmd);
        static InputAccessoryViewDelegate? _returnNilDelegate;
        static IntPtr _originalImp = IntPtr.Zero;
 
        static void TrySwizzleInputAccessoryView()
        {
            if (_swizzled)
                return;
 
            try
            {
                var cls = objc_getClass("WKContentView");
                if (cls == IntPtr.Zero)
                    return;
 
                var selector = sel_registerName("inputAccessoryView");
                var method = class_getInstanceMethod(cls, selector);
                if (method == IntPtr.Zero)
                    return;
 
                _originalImp = method_getImplementation(method);
 
                _returnNilDelegate ??= new InputAccessoryViewDelegate(ReturnNil);
                var imp = Marshal.GetFunctionPointerForDelegate(_returnNilDelegate);
 
               method_setImplementation(method, imp);
                _swizzled = true;
            }
            catch
            {
                // Ignore failures (iOS internals may change)
            }
        }
 
        static IntPtr ReturnNil(IntPtr self, IntPtr _cmd) => IntPtr.Zero;
    }
}
