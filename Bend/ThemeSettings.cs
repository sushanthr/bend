using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;

namespace Bend
{
    public class ThemeSettings
    {
        public ThemeSettings()
        {
            BackgroundColor = System.Windows.Media.Colors.WhiteSmoke;
            BackgroundTerminalColor = BackgroundColor;
            ForegroundColor = System.Windows.Media.Colors.Black;
            BaseBackgroundImage = "Images/FrontBackground.png";
            ScrollButtonColor = System.Windows.Media.Color.FromRgb(208, 208, 208);
            LogoBackgroundColor = System.Windows.Media.Color.FromRgb(54, 80, 128);
            LogoForegroundColor = System.Windows.Media.Colors.White;
            MenuSelectedBackgroundColor = System.Windows.Media.Color.FromRgb(221, 221, 221);

            DefaultForegroundColor = System.Windows.Media.Color.FromRgb(0, 0, 0);
            DefaultBackgroundColor = System.Windows.Media.Color.FromRgb(245, 245, 245);
            DefaultSelectionColor = System.Windows.Media.Color.FromRgb(106, 124, 159);
            DefaultSelectionOutlineColor = System.Windows.Media.Color.FromRgb(94, 114, 153);
            DefaultSelectionDimColor = System.Windows.Media.Color.FromArgb(128, 245, 245, 245);
            DefaultBackgroundHighlightColor = System.Windows.Media.Color.FromArgb(64, 106, 124, 159);
            LineNumberColor = System.Windows.Media.Color.FromRgb(140, 140, 140);

            DefaultShowFormattingColor = System.Windows.Media.Color.FromRgb(189, 189, 189);
            DefaultShowFormattingColorAlt = System.Windows.Media.Color.FromRgb(230, 230, 230);

            SyntaxHighlightingKeyword1 = System.Windows.Media.Color.FromRgb(0, 102, 153);
            SyntaxHighlightingKeyword2 = System.Windows.Media.Color.FromRgb(0, 0, 128);
            SyntaxHighlightingKeyword3 = System.Windows.Media.Color.FromRgb(0, 0, 255);
            SyntaxHighlightingKeyword4 = System.Windows.Media.Color.FromRgb(0, 0, 255);
            SyntaxHighlightingKeyword5 = System.Windows.Media.Color.FromRgb(0, 0, 255);
            SyntaxHighlightingKeyword6 = System.Windows.Media.Color.FromRgb(139, 0, 0);
            SyntaxHighlightingPreProcessorKeyword = System.Windows.Media.Color.FromRgb(0, 128, 0);
            SyntaxHighlightingPreProcessor = System.Windows.Media.Color.FromRgb(0, 155, 91);
            SyntaxHighlightingComment = System.Windows.Media.Color.FromRgb(170, 170, 170);
            SyntaxHighlightingOperator = System.Windows.Media.Color.FromRgb(230, 51, 51);
            SyntaxHighlightingBracket = System.Windows.Media.Color.FromRgb(250, 51, 51);
            SyntaxHighlightingNumber = System.Windows.Media.Color.FromRgb(184, 134, 11);
            SyntaxHighlightingString = System.Windows.Media.Color.FromRgb(0, 100, 0);
            SyntaxHighlightingChar = System.Windows.Media.Color.FromRgb(0, 100, 0);
        }

        public static ThemeSettings LoadThemeSettings(string themeName)
        {
            ThemeSettings themeSettings;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ThemeSettings));
                String filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Themes\\" + themeName + ".xml";
                FileStream fs = new FileStream(filePath, FileMode.Open);                
                themeSettings = (ThemeSettings)serializer.Deserialize(fs);
                fs.Close();
            }
            catch
            {
                themeSettings = new ThemeSettings();
            }
            return themeSettings;
        }

        public System.Windows.Media.Color BackgroundColor;
        public System.Windows.Media.Color BackgroundTerminalColor;
        public System.Windows.Media.Color ForegroundColor;
        public System.Windows.Media.Color ScrollButtonColor;
        public System.Windows.Media.Color LogoForegroundColor;
        public System.Windows.Media.Color LogoBackgroundColor;
        public System.Windows.Media.Color MenuSelectedBackgroundColor;

        public string BaseBackgroundImage;

        // Editor Theme
        public System.Windows.Media.Color DefaultForegroundColor;
        public System.Windows.Media.Color DefaultBackgroundColor;
        public System.Windows.Media.Color DefaultSelectionColor;
        public System.Windows.Media.Color DefaultSelectionOutlineColor;
        public System.Windows.Media.Color DefaultSelectionDimColor;
        public System.Windows.Media.Color DefaultBackgroundHighlightColor;
        public System.Windows.Media.Color LineNumberColor;

        public System.Windows.Media.Color DefaultShowFormattingColor;
        public System.Windows.Media.Color DefaultShowFormattingColorAlt;

        public System.Windows.Media.Color SyntaxHighlightingKeyword1;
        public System.Windows.Media.Color SyntaxHighlightingKeyword2;
        public System.Windows.Media.Color SyntaxHighlightingKeyword3;
        public System.Windows.Media.Color SyntaxHighlightingKeyword4;
        public System.Windows.Media.Color SyntaxHighlightingKeyword5;
        public System.Windows.Media.Color SyntaxHighlightingKeyword6;
        public System.Windows.Media.Color SyntaxHighlightingPreProcessorKeyword;
        public System.Windows.Media.Color SyntaxHighlightingPreProcessor;
        public System.Windows.Media.Color SyntaxHighlightingComment;
        public System.Windows.Media.Color SyntaxHighlightingOperator;
        public System.Windows.Media.Color SyntaxHighlightingBracket;
        public System.Windows.Media.Color SyntaxHighlightingNumber;
        public System.Windows.Media.Color SyntaxHighlightingString;
        public System.Windows.Media.Color SyntaxHighlightingChar;
    }
}
