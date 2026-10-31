using Microsoft.Maui.Layouts;
using MoneyRecord.Helpers;

namespace MoneyRecord.Controls
{
    /// <summary>The system font scale (Android: Settings → Display → Font size) and the layout values that follow it.</summary>
    public static class ScaledLayout
    {
        /// <summary>1 at the normal font size; read each time, so a page created after the user changed it follows it.</summary>
        public static double FontScale
        {
            get
            {
#if ANDROID
                return Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1d;
#else
                return 1d;
#endif
            }
        }

        /// <summary>A category cell on the add form: a fourth, a third or a half of the row.</summary>
        public static FlexBasis CategoryBasis => new(1f / FontScaleLayout.CategoryColumns(FontScale), isRelative: true);
    }
}
