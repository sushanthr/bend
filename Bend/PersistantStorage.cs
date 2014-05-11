using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace Bend
{
    public class PersistantStorage
    {
        static PersistantStorage singletonPersistantStorageObject;
        const string settingsFileName = "Settings.xml";

        #region Member Data
        public string[] mruFile;
        public double mainWindowTop;
        public double mainWindowLeft;
        public double mainWindowWidth;
        public double mainWindowHeight;

        public bool SettingsPageAnimation;
        public bool ShowStatusBar;
        public bool IsFirstRun;
        public bool ReopenFilesOnStart;
        public bool Diagnostics;

        // JSBeautifier Options
        public bool JSBeautifyPreserveLine;
        public int  JSBeautifyIndent;
        public bool JSBeautifyUseSpaces;
        public bool JSBeautifyUseTabs;

        // Text Editor Options
        public int TextIndent;
        public bool TextUseSpaces;
        public bool TextUseTabs;
        public bool TextShowFormatting;
        public bool TextWordWrap;
        public bool SmoothScrolling;
        public bool SyntaxHighlighting;
        public bool PreserveIndent;

        // Font Picker
        public int DefaultFontFamilyIndex;
        public string DefaultFontFamily;

        // Theme 
        public string CurrentThemeFilename;
        public ThemeSettings CurrentTheme;

        #endregion

        private PersistantStorage()
        {
            // Prevent object construction and default file creation
            mruFile = null;
            mainWindowTop = System.Windows.SystemParameters.PrimaryScreenHeight / 2 - 300;
            mainWindowLeft = System.Windows.SystemParameters.PrimaryScreenWidth / 2 - 400;
            mainWindowWidth = 800.0;
            mainWindowHeight = 600.0;

            SettingsPageAnimation = true;
            ShowStatusBar = false;
            IsFirstRun = true;
            ReopenFilesOnStart = false;
            Diagnostics = false;

            JSBeautifyPreserveLine = false;
            JSBeautifyIndent = 4;
            JSBeautifyUseSpaces = true;
            JSBeautifyUseTabs = false;

            TextIndent = 4;
            TextUseSpaces = true;
            TextUseTabs = false;
            TextShowFormatting = false;
            TextWordWrap = true;
            SmoothScrolling = true;
            SyntaxHighlighting = true;
            PreserveIndent = true;
            DefaultFontFamily = "Consolas";
            DefaultFontFamilyIndex = -1;

            CurrentThemeFilename = "Light";
            CurrentTheme = new ThemeSettings();            
        }

        static PersistantStorage()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(PersistantStorage));
                String filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
                FileStream fs = new FileStream(filePath + settingsFileName, FileMode.Open);
                singletonPersistantStorageObject = (PersistantStorage)serializer.Deserialize(fs);
                fs.Close();
            }
            catch
            {
                singletonPersistantStorageObject = new PersistantStorage();
            }
        }

        public static PersistantStorage StorageObject
        {
            get
            {
                return singletonPersistantStorageObject;
            }
        }

        ~PersistantStorage()
        {
            try
            {
                this.IsFirstRun = false;
                XmlSerializer serializer = new XmlSerializer(this.GetType());
                String filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
                TextWriter writer = new StreamWriter(filePath + settingsFileName);
                serializer.Serialize(writer, this);
                writer.Close();
            }
            catch
            {
            }
        }
    }
}
