using Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIKit;

namespace Spixi
{
    public class FileInteractionDelegate : UIDocumentInteractionControllerDelegate
    {
        UIViewController parent;

        public FileInteractionDelegate(UIViewController controller)
        {
            parent = controller;
        }

        public override UIViewController ViewControllerForPreview(UIDocumentInteractionController controller)
        {
            return parent;
        }
    }

    public class SFileOperations
    {
        public static void open(string filepath)
        {
            var viewController = GetVisibleViewController();
            if (viewController == null)
            {
                return;
            }

            var previewController = UIDocumentInteractionController.FromUrl(NSUrl.FromFilename(filepath));
            previewController.Delegate = new FileInteractionDelegate(viewController);
            previewController.PresentPreview(true);
        }

        public static Task share(string filepath, string title)
        {
            var items = new NSObject[] { NSObject.FromObject(title), NSUrl.FromFilename(filepath) };
            var activityController = new UIActivityViewController(items, null);
            var vc = GetVisibleViewController();

            NSString[] excludedActivityTypes = null;

            if (excludedActivityTypes != null && excludedActivityTypes.Length > 0)
                activityController.ExcludedActivityTypes = excludedActivityTypes;

            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
            {
                if (activityController.PopoverPresentationController != null)
                {
                    activityController.PopoverPresentationController.SourceView = vc.View;
                }
            }
            vc.PresentViewControllerAsync(activityController, true);
            return Task.FromResult(true);
        }

        static UIViewController? GetVisibleViewController()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                var windowScene = UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIWindowScene>()
                    .FirstOrDefault(x => x.ActivationState == UISceneActivationState.ForegroundActive);

                var keyWindow = windowScene?.Windows?.FirstOrDefault(x => x.IsKeyWindow);
                if (keyWindow?.RootViewController is UINavigationController navController)
                {
                    return navController.VisibleViewController ?? navController.TopViewController;
                }
            }
            else
            {
                var keyWindow = UIApplication.SharedApplication.KeyWindow;
                if (keyWindow?.RootViewController is UINavigationController fallbackNavController)
                {
                    return fallbackNavController.VisibleViewController ?? fallbackNavController.TopViewController;
                }
            }

            return null;
        }

    }
}
