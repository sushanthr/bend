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
            Settings.dwriteFactory                         = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            Settings.DefaultTextFormat                     = dwriteFactory.CreateTextFormat("Consolas", 14, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal);
            Settings.DefaultShowFormattingTextFormat       = dwriteFactory.CreateTextFormat("Consolas", 12, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal);

            Settings.AutoWrap                              = true;
            
            Settings.DefaultForegroundColor                = new ColorF(0, 0, 0, 1);
            Settings.DefaultBackgroundColor                = new ColorF(0.96f, 0.96f, 0.96f);
            Settings.DefaultSelectionColor                 = new ColorF(0.414f, 0.484f, 0.625f, 1.0f);
            Settings.DefaultSelectionOutlineColor          = new ColorF(0.3686f, 0.447f, 0.6f, 1.0f);
            Settings.DefaultSelectionDimColor              = new ColorF(245 / 255f, 245 / 255f, 245 / 255f, 0.50f);
            Settings.LineNumberColor                       = new ColorF(0.55f, 0.55f, 0.55f);

            Settings.MouseWheel_Normal_Step_LineCount      = 4;
            Settings.MouseWheel_Fast1_Step_LineCount       = 16;
            Settings.MouseWheel_Fast1_Threshold_MS         = 20;

            Settings.CopyPaste_ClipRing_Max_Entries        = 10;

            Settings.ShowDebugHUD                          = false;

            Settings.ShowLineNumber                        = false;
            Settings.MinLineNumberDigits                   = 3;
             
            Settings.UseStringForTab                       = false;
            Settings.TabString                             = "    ";

            Settings.ReturnKeyInsertsNewLineCharacter      = true;
            Settings.AllowSmoothScrollBy                   = true;
            Settings.EnableSyntaxHighlighting              = true;
             
            Settings.ShowFormatting                        = false;
            Settings.DefaultShowFormattingColor            = new ColorF(0.74f, 0.74f, 0.74f);
            Settings.DefaultShowFormattingColorAlt         = new ColorF(0.90f, 0.90f, 0.90f);

            Settings.PreserveIndentLevel                   = true;
            
            Settings.SyntaxHighlightingKeyword1            = new ColorF(   0, 102f/255, 153f/255);
            Settings.SyntaxHighlightingKeyword2            = new ColorF(   0,   0, 128f/255);
            Settings.SyntaxHighlightingKeyword3            = new ColorF(   0,   0, 255f/255);
            Settings.SyntaxHighlightingKeyword4            = new ColorF(   0,   0, 255f/255);
            Settings.SyntaxHighlightingKeyword5            = new ColorF(   0,   0, 255f/255);
            Settings.SyntaxHighlightingKeyword6            = new ColorF( 139f/255,   0,   0);
            Settings.SyntaxHighlightingPreProcessorKeyword = new ColorF(   0, 128f/255,   0);
            Settings.SyntaxHighlightingPreProcessor        = new ColorF(   0, 155f/255,  91f/255);
            Settings.SyntaxHighlightingComment             = new ColorF( 170f/255, 170f/255, 170f/255);
            Settings.SyntaxHighlightingOperator            = new ColorF( 230f/255,  51f/255,  51f/255);
            Settings.SyntaxHighlightingBracket             = new ColorF( 250f/255,  51f/255,  51f/255);
            Settings.SyntaxHighlightingNumber              = new ColorF( 184f/255, 134f/255,  11f/255);
            Settings.SyntaxHighlightingString              = new ColorF(   0f/255, 100f/255,  0f/255);
            Settings.SyntaxHighlightingChar                = new ColorF(   0f/255, 100f/255,  0f/255);
        }

        public static void SetFontFamily(string fontfamily)
        {
            Settings.DefaultTextFormat = dwriteFactory.CreateTextFormat(fontfamily,
             Settings.DefaultTextFormat.FontSize,
             Settings.DefaultTextFormat.FontWeight,
             Settings.DefaultTextFormat.FontStyle,
             Settings.DefaultTextFormat.FontStretch);

            Settings.DefaultShowFormattingTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
                Settings.DefaultTextFormat.FontSize - 2,
                Settings.DefaultTextFormat.FontWeight,
                Settings.DefaultTextFormat.FontStyle,
                Settings.DefaultTextFormat.FontStretch);
        }
        
        public static void IncreaseFontSize()
        {            
            Settings.DefaultTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName, 
                Settings.DefaultTextFormat.FontSize + 2,
                Settings.DefaultTextFormat.FontWeight,
                Settings.DefaultTextFormat.FontStyle,
                Settings.DefaultTextFormat.FontStretch);

            Settings.DefaultShowFormattingTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
                Settings.DefaultTextFormat.FontSize - 2,
                Settings.DefaultTextFormat.FontWeight,
                Settings.DefaultTextFormat.FontStyle,
                Settings.DefaultTextFormat.FontStretch);
        }

        public static void DecreaseFontSize()
        {
            if (Settings.DefaultTextFormat.FontSize > 5)
            {                
                Settings.DefaultTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
                    Settings.DefaultTextFormat.FontSize - 2,
                    Settings.DefaultTextFormat.FontWeight,
                    Settings.DefaultTextFormat.FontStyle,
                    Settings.DefaultTextFormat.FontStretch);

                Settings.DefaultShowFormattingTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
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

            Settings.DefaultShowFormattingTextFormat = dwriteFactory.CreateTextFormat(Settings.DefaultTextFormat.FontFamilyName,
                Settings.DefaultTextFormat.FontSize - 2,
                Settings.DefaultTextFormat.FontWeight,
                Settings.DefaultTextFormat.FontStyle,
                Settings.DefaultTextFormat.FontStretch);
        }

        public static void CopyColor(System.Windows.Media.Color source, ref ColorF destination)
        {
            destination = new ColorF((source.R * 1f) / 255f, (source.G * 1f) / 255f, (source.B * 1f) / 255f, (source.A * 1f) / 255f);
        }

        private static DWriteFactory dwriteFactory;

        public static TextFormat    DefaultTextFormat;

        public static bool          AutoWrap;

        public static ColorF        DefaultForegroundColor;
        public static ColorF        DefaultBackgroundColor;
        public static ColorF        DefaultSelectionColor;
        public static ColorF        DefaultSelectionOutlineColor;
        public static ColorF        DefaultSelectionDimColor;
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

        public static bool          EnableSyntaxHighlighting;

        public static bool          ShowFormatting;
        public static ColorF        DefaultShowFormattingColor;
        public static ColorF        DefaultShowFormattingColorAlt;
        public static TextFormat    DefaultShowFormattingTextFormat;

        public static bool          PreserveIndentLevel;

        // Syntax highlighting colors
        public static ColorF         SyntaxHighlightingKeyword1;
        public static ColorF         SyntaxHighlightingKeyword2;
        public static ColorF         SyntaxHighlightingKeyword3;
        public static ColorF         SyntaxHighlightingKeyword4;
        public static ColorF         SyntaxHighlightingKeyword5;
        public static ColorF         SyntaxHighlightingKeyword6;
        public static ColorF         SyntaxHighlightingPreProcessorKeyword;
        public static ColorF         SyntaxHighlightingPreProcessor;
        public static ColorF         SyntaxHighlightingComment;
        public static ColorF         SyntaxHighlightingOperator;
        public static ColorF         SyntaxHighlightingBracket;
        public static ColorF         SyntaxHighlightingNumber;
        public static ColorF         SyntaxHighlightingString;
        public static ColorF         SyntaxHighlightingChar;
    }
}
