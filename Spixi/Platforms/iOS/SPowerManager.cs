using IXICore.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIKit;

namespace Spixi
{
    public class SPowerManager

    {
        public static bool AquireLock(string lock_type = "screenDim")
        {
            switch (lock_type)
            {
                case "screenDim":
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DeviceDisplay.Current.KeepScreenOn = true;
                    });
                    return true;
                case "partial":
                    return true;
                case "proximityScreenOff":
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UIDevice.CurrentDevice.ProximityMonitoringEnabled = true;
                    });
                    return true;
                case "wifi":
                    return true;
            }
            return false;
        }

        public static bool ReleaseLock(string lock_type = "screenDim")
        {
            switch (lock_type)
            {
                case "screenDim":
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DeviceDisplay.Current.KeepScreenOn = false;
                    });
                    return true;
                case "partial":
                    return true;
                case "proximityScreenOff":
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UIDevice.CurrentDevice.ProximityMonitoringEnabled = false;
                    });
                    return true;
                case "wifi":
                    return true;
            }
            return false;
        }

    }
}
