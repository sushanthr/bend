using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    public static class Settings
    {
        static Settings()
        {
            // Create the DWrite Factory
            Settings.dwriteFactory                       = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            Settings.DefaultTextFormat                   = dwriteFactory.CreateTextFormat("Consolas", 14, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal);

            Settings.AutoWrap                            = true;

            Settings.DefaultForegroundColor              = new ColorF(0, 0, 0, 1);
            Settings.DefaultBackgroundColor              = new ColorF(0.96f, 0.96f, 0.96f);
            Settings.DefaultSelectionColor               = new ColorF(0.414f, 0.484f, 0.625f, 1.0f);
            Settings.DefaultSelectionOutlineColor        = new ColorF(0.3686f, 0.447f, 0.6f, 1.0f);
            Settings.LineNumberColor                     = new ColorF(0.55f, 0.55f, 0.55f);

            Settings.MouseWheel_Normal_Step_LineCount    = 4;
            Settings.MouseWheel_Fast1_Step_LineCount     = 16;
            Settings.MouseWheel_Fast1_Threshold_MS       = 20;

            Settings.CopyPaste_ClipRing_Max_Entries      = 10;

            Settings.ShowDebugHUD                        = false;

            Settings.ShowLineNumber                      = false;
            Settings.MinLineNumberDigits                 = 3;

            Settings.UseStringForTab                     = true;
            Settings.TabString                           = "    ";

            Settings.ReturnKeyInsertsNewLineCharacter    = true;
            Settings.AllowSmoothScrollBy                 = true;
        }

        public static void IncreaseFontSize()
        {            
            Settings.DefaultTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName, 
                Settings.DefaultTextFormat.FontSize + 2,
                Settings.DefaultTextFormat.FontWeight,
                Settings.DefaultTextFormat.FontStyle,
                Settings.DefaultTextFormat.FontStretch);
        }

        public static void DecreaseFontSize()
        {
            if (Settings.DefaultTextFormat.FontSize > 3)
            {                
                Settings.DefaultTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
                    Settings.DefaultTextFormat.FontSize - 2,
                    Settings.DefaultTextFormat.FontWeight,
                    Settings.DefaultTextFormat.FontStyle,
                    Settings.DefaultTextFormat.FontStretch);
            }
        }

        public static void ResetFontSize()
        {
            Settings.DefaultTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
                14,
                Settings.DefaultTextFormat.FontWeight,
                Settings.DefaultTextFormat.FontStyle,
                Settings.DefaultTextFormat.FontStretch);
        }

        private static DWriteFactory dwriteFactory;

        public static TextFormat    DefaultTextFormat;

        public static bool          AutoWrap;

        public static ColorF        DefaultForegroundColor;
        public static ColorF        DefaultBackgroundColor;
        public static ColorF        DefaultSelectionColor;
        public static ColorF        DefaultSelectionOutlineColor;
        public static ColorF        LineNumberColor;

        public static int           MouseWheel_Normal_Step_LineCount;
        public static int           MouseWheel_Fast1_Step_LineCount;
        public static int           MouseWheel_Fast1_Threshold_MS;

        public static int           CopyPaste_ClipRing_Max_Entries;

        public static bool          ShowDebugHUD;
        public static bool          ShowLineNumber;
        public static int           MinLineNumberDigits;

        public static bool          UseStringForTab;
        public static string        TabString;

        public static bool          ReturnKeyInsertsNewLineCharacter;
        public static bool          AllowSmoothScrollBy;
    }
}
