using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml.Controls;

namespace Spixi
{
    public class SSystemAlert
    {

        private const UInt32 FLASHW_STOP = 0; // Stop flashing. The system restores the window to its original state.
        private const UInt32 FLASHW_CAPTION = 1; // Flash the window caption. 
        private const UInt32 FLASHW_TRAY = 2; // Flash the taskbar button. 
        private const UInt32 FLASHW_ALL = 3; // Flash both the window caption and taskbar button.
        private const UInt32 FLASHW_TIMER = 4; // Flash continuously, until the FLASHW_STOP flag is set.
        private const UInt32 FLASHW_TIMERNOFG = 12; // Flash continuously until the window comes to the foreground.

        private struct FLASHWINFO
        {
            public UInt32 cbSize; // The size of the structure, in bytes.
            public IntPtr hwnd; // A handle to the window to be flashed. The window can be either opened or minimized.
            public UInt32 dwFlags; // The flash status.
            public UInt32 uCount; // The number of times to flash the window.
            public UInt32 dwTimeout; // The rate at which the window is to be flashed, in milliseconds. If dwTimeout is zero, the function uses the default cursor blink rate.
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pfwi);

        public async static void displayAlert(string title, string message, string cancel)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };

            await dialog.ShowAsync();
        }
        public static void flash()
        {
            Microsoft.UI.Xaml.Window xamlWindow = (Microsoft.UI.Xaml.Window)App.Current.Windows.First<Window>().Handler.PlatformView;
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(xamlWindow);

            FLASHWINFO info = new FLASHWINFO
            {
                hwnd = windowHandle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 0,
                dwTimeout = 0
            };

            info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));

            FlashWindowEx(ref info);
        }

    }
}