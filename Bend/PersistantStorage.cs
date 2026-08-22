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

        public PersistantStorage()
        {
            // Prevent object construction and default file creation
            mruFile = null;
            mainWindowTop = System.Windows.SystemParameters.PrimaryScreenHeight / 2 - 300;
            mainWindowLeft = System.Windows.SystemParameters.PrimaryScreenWidth / 2 - 400;
            mainWindowWidth = 800.0;
            mainWindowHeight = 600.0;

            SettingsPageAnimation = true;
            ShowStatusBar = true;
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
                string loadPath = File.Exists(SettingsPath) ? SettingsPath : LegacySettingsPath;
                using (FileStream fs = new FileStream(loadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    singletonPersistantStorageObject = (PersistantStorage)serializer.Deserialize(fs);
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

        private static string SettingsPath
        {
            get
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bend");
                return Path.Combine(directory, settingsFileName);
            }
        }

        private static string LegacySettingsPath
        {
            get { return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), settingsFileName); }
        }

        public static void Save()
        {
            string settingsPath = SettingsPath;
            string directory = Path.GetDirectoryName(settingsPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, settingsFileName + ".tmp");
            singletonPersistantStorageObject.IsFirstRun = false;
            XmlSerializer serializer = new XmlSerializer(typeof(PersistantStorage));
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    serializer.Serialize(stream, singletonPersistantStorageObject);
                    stream.Flush(true);
                }
                if (File.Exists(settingsPath))
                    File.Replace(temporaryPath, settingsPath, null);
                else
                    File.Move(temporaryPath, settingsPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
