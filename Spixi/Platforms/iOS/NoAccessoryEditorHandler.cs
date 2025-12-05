using Microsoft.Maui.Handlers;
using UIKit;

namespace Spixi.Platforms.iOS
{
    public class NoAccessoryEditorHandler : EditorHandler
    {
        protected override void ConnectHandler(UITextView nativeView)
        {
            base.ConnectHandler(nativeView);

            // Remove iOS toolbar above keyboard (arrow up/down + checkmark)
            nativeView.InputAccessoryView = new UIView();
        }
    }
}
