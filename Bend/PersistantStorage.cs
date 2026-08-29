using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace Bend
{
    public class SavedTerminalCommand
    {
        public string Name { get; set; }
        public string CommandLine { get; set; }

        public SavedTerminalCommand() { }

        public SavedTerminalCommand(string name, string commandLine)
        {
            Name = name;
            CommandLine = commandLine;
        }
    }

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
        // Stored by name so older settings files (where the element is absent)
        // can safely fall back to Inline instead of depending on enum ordinals.
        public string DiffViewMode;
        public string LastDiffViewMode;

        // Font Picker
        public int DefaultFontFamilyIndex;
        public string DefaultFontFamily;

        // Theme 
        public string CurrentThemeFilename;
        public ThemeSettings CurrentTheme;

        // Workspace
        public string LastWorkspaceFolder;
        public double BottomTerminalHeight;
        public double LeftPaneWidth;
        public double AgentPaneWidth;
        public string DefaultAgentCli;
        public string AdditionalAgentClis;
        public List<SavedTerminalCommand> PowerShellCommands;
        public List<SavedTerminalCommand> CommandPromptCommands;

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
            DiffViewMode = "Inline";
            LastDiffViewMode = "Inline";
            DefaultFontFamily = "Consolas";
            DefaultFontFamilyIndex = -1;

            CurrentThemeFilename = "Light";
            CurrentTheme = new ThemeSettings();

            LastWorkspaceFolder = null;
            BottomTerminalHeight = 300;
            LeftPaneWidth = 240;
            AgentPaneWidth = 360;
            DefaultAgentCli = "copilot";
            AdditionalAgentClis = String.Empty;
            PowerShellCommands = new List<SavedTerminalCommand>();
            CommandPromptCommands = new List<SavedTerminalCommand>();
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

        public List<SavedTerminalCommand> GetTerminalCommands(bool powerShell)
        {
            if (PowerShellCommands == null) PowerShellCommands = new List<SavedTerminalCommand>();
            if (CommandPromptCommands == null) CommandPromptCommands = new List<SavedTerminalCommand>();
            return powerShell ? PowerShellCommands : CommandPromptCommands;
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
