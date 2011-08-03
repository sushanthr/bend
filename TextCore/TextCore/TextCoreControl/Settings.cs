using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    static class Settings
    {
        static Settings()
        {
            // Create the DWrite Factory
            Settings.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            Settings.defaultTextFormat = dwriteFactory.CreateTextFormat("Consolas", 14, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal);
            Settings.autoWrap = true;

            Settings.defaultForegroundColor = new ColorF(0, 0, 0, 1);
            Settings.defaultBackgroundColor = new ColorF(0.96f, 0.96f, 0.96f);
            Settings.defaultSelectionColor = new ColorF(0.414f, 0.484f, 0.625f, 1.0f);
        }

        public static TextFormat defaultTextFormat;
        public static DWriteFactory dwriteFactory;
        public static bool autoWrap;
        public static ColorF defaultForegroundColor;
        public static ColorF defaultBackgroundColor;
        public static ColorF defaultSelectionColor;
    }
}
