using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SPIXI.Lang;

namespace SPIXI
{
    public enum ThemeAppearance
    {
        automatic = 0,
        light = 1,
        dark = 2
    }

    public static class ThemeManager
    {
        private static ThemeAppearance activeAppearance = ThemeAppearance.automatic;
        private static string activeTheme = "spixiui";

        public static bool loadTheme(string name, ThemeAppearance appearance)
        {
            string appearance_name = appearance switch
            {
                ThemeAppearance.dark => "dark",
                ThemeAppearance.automatic => Application.Current!.UserAppTheme == AppTheme.Dark ? "dark" : "light",
                _ => "light"
            };

            activeTheme = name;
            activeAppearance = appearance;

            Preferences.Default.Set("appearance", (int)activeAppearance);
            SpixiLocalization.addCustomString("SpixiThemeName", appearance_name);
            SpixiLocalization.addCustomString("SpixiThemeMode", name + "-" + appearance_name + ".css");

            return true;
        }

        public static bool changeAppearance(ThemeAppearance newAppearance)
        {
            return loadTheme(activeTheme, newAppearance);
        }

        public static ThemeAppearance getActiveAppearance()
        {
            return activeAppearance;
        }

        // Temporary function to handle Android appearance changes. Will be removed in the future
        public static string getActiveAppearanceString()
        {
            if (activeAppearance == ThemeAppearance.dark)
            {
                return "spixiui-dark";
            }
            else if (activeAppearance == ThemeAppearance.automatic)
            {
                if (Application.Current.UserAppTheme == AppTheme.Dark)
                    return "spixiui-dark";
            }

            return "spixiui-light";
        }


        public static string getBackgroundColorString()
        {
            if (getActiveAppearanceString() == "spixiui-light")
                return "#223766";
            return "#0B1219";
        }

        public static Color getBackgroundColor()
        {
            return Color.FromArgb(getBackgroundColorString());
        }

    }
}
