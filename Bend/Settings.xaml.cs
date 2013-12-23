using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Deployment.Application;
using EfTidyNet;

namespace Bend
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : UserControl
    {
        #region Member Data
        private bool isApplicationNetworkDeployed;
        #endregion

        #region Settings UI Maintainance
        public Settings()
        {
            InitializeComponent();
        }

        public void UpdateFocus()
        {
            ((TabItem)this.SettingsTabs.SelectedItem).Focus();
        }

        private void ControlInitialized(object sender, EventArgs e)
        {
            this.isApplicationNetworkDeployed = ApplicationDeployment.IsNetworkDeployed;
            if (isApplicationNetworkDeployed)
            {
                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
                ad.CheckForUpdateProgressChanged += new DeploymentProgressChangedEventHandler(ad_UpdateProgressChanged);
                ad.CheckForUpdateCompleted += new CheckForUpdateCompletedEventHandler(ad_CheckForUpdateCompleted);
                ad.UpdateCompleted += new System.ComponentModel.AsyncCompletedEventHandler(ad_UpdateCompleted);
                ad.UpdateProgressChanged += new DeploymentProgressChangedEventHandler(ad_UpdateProgressChanged);
            }

            if (PersistantStorage.StorageObject.IsFirstRun)
            {
                UpdateRegistryOnFirstRun();
            }

            this.UpdateButtons();
            CheckForUpdatesButton.IsEnabled = isApplicationNetworkDeployed;

            // Load defaults from persistant storage            
            JSBeautifyPreserveLine.IsChecked = PersistantStorage.StorageObject.JSBeautifyPreserveLine;
            JSBeautifyIndent.Text = PersistantStorage.StorageObject.JSBeautifyIndent.ToString();
            JSBeautifyUseSpaces.IsChecked = PersistantStorage.StorageObject.JSBeautifyUseSpaces;
            JSBeautifyUseTabs.IsChecked = PersistantStorage.StorageObject.JSBeautifyUseTabs;

            try
            {
                if (isApplicationNetworkDeployed)
                {
                    Version.Text = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                }
                else
                {
                    Version.Text = "DEBUG";
                }
            }
            catch
            {
            }

            this.UpdateOptions();
        }

        private void UpdateButtons()
        {
            bool enableContextMenu = true;
            bool enableAddToPath = true;
            try
            {
                string BendExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                RegistryKey HKCU = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                RegistryKey HKCU_SOFTWARE_CLASSES = HKCU.OpenSubKey("Software").OpenSubKey("Classes");
                RegistryKey BendShortcutKey = HKCU_SOFTWARE_CLASSES.OpenSubKey("*");
                BendShortcutKey = BendShortcutKey != null ? BendShortcutKey.OpenSubKey("Shell") : null;
                BendShortcutKey = BendShortcutKey != null ? BendShortcutKey.OpenSubKey("Bend") : null;
                if (BendShortcutKey != null && BendShortcutKey.GetValue("").ToString() == "Bend file")
                {
                    if (BendShortcutKey.GetValue("Icon").ToString() == BendExePath + ",0")
                    {
                        if (BendShortcutKey.OpenSubKey("Command").GetValue("").ToString() == "rundll32.exe dfshim.dll, ShOpenVerbExtension {29C436A6-392B-4069-8DF7-760271B08F67} %1")
                        {
                            RegistryKey HKCU_CLSID_UNIQUE = HKCU_SOFTWARE_CLASSES.OpenSubKey("CLSID").OpenSubKey("{29C436A6-392B-4069-8DF7-760271B08F67}");
                            if (HKCU_CLSID_UNIQUE.GetValue("").ToString() == "Bend - A modern text editor (Explorer right click menu integration)" &&
                                HKCU_CLSID_UNIQUE.GetValue("AppId").ToString() == "Bend.application, Culture = neutral, PublicKeyToken = 0000000000000000, processorArchitecture = x86" &&
                                HKCU_CLSID_UNIQUE.GetValue("DeploymentProviderUrl").ToString() == "http://bend.codeplex.com/releases/clickonce/Bend.application")
                            {
                                DisableContextMenuButton.IsEnabled = true;
                                EnableContextMenuButton.IsEnabled = false;
                                enableContextMenu = false;
                            }
                        }
                    }
                }

                RegistryKey HKCU_ENVIRONMENT = Registry.CurrentUser.CreateSubKey("Environment");
                string bendDirectory = System.IO.Path.GetDirectoryName(BendExePath);
                string currentPath;
                if (HKCU_ENVIRONMENT.GetValue("Path") != null)
                {
                    currentPath = HKCU_ENVIRONMENT.GetValue("Path").ToString();
                    if (currentPath.IndexOf(bendDirectory) >= 0)
                    {
                        // We are already in the path
                        enableAddToPath = false;
                    }
                }
            }
            catch
            {
            }

            DisableContextMenuButton.IsEnabled = !enableContextMenu;
            EnableContextMenuButton.IsEnabled = enableContextMenu;
            AppendToPathButton.IsEnabled = enableAddToPath;
        }

        private Tab CurrentTab()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            if (mainWindow != null)
            {
                return mainWindow.CurrentTab;
            }
            else
            {
                return null;
            }
        }

        private void CancelSettingsUI()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.CancelSettingsUI();
            }
        }

        private void MainWindow_LoadOptions()
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.LoadOptions();
            }
        }              
        
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TabItem tabItem = (TabItem)SettingsTabs.SelectedItem;
                if (tabItem != null)
                {
                    TabControl tabControl = (TabControl)tabItem.Parent;
                    for (int i = 0; i < tabControl.Items.Count; i++)
                    {
                        ((Label)((TabItem)tabControl.Items[i]).Header).Foreground = Brushes.Gray;
                    }

                    Label header = (Label)tabItem.Header;
                    header.Foreground = new SolidColorBrush(Color.FromArgb(255, 25, 162, 222));
                    ProgressBar.Rect = new Rect(0, 0, 0, 5);
                }
            }
            catch
            {
            }
        }
        
        private void Settings_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ProgressBar.Rect = new Rect(0, 0, 0, 5);
        }
        #endregion

        #region Application Update

        private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdatesButton.IsEnabled = false;
            ProgressBar.Rect = new Rect(0, 0, 0, 5);

            if (isApplicationNetworkDeployed)
            {
                try
                {
                    ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
                    ad.CheckForUpdateAsync();
                }
                catch (Exception exception)
                {
                    StyledMessageBox.Show("UPDATE", "Bend cannot be updated. Error: " + exception.Message + "\n", true);
                }                
            }

            CheckForUpdatesButton.IsEnabled = isApplicationNetworkDeployed;
        }

        void ad_CheckForUpdateCompleted(object sender, CheckForUpdateCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(delegate
            {
                if (e.Error != null)
                {
                    StyledMessageBox.Show("UPDATE", "The new version of the Bend cannot be downloaded at this time.\nPlease check your network connection, or try again later. Error: " + e.Error.Message + "\n", true);
                }
                else
                {
                    // Ask the user if they would like to update the application now.
                    if (e.UpdateAvailable)
                    {
                        if (!e.IsUpdateRequired)
                        {
                            if (StyledMessageBox.Show("UPDATE", "An update is available. Choose OK to start update. \n", true))
                                this.DoUpdate();
                        }
                        else
                        {
                            // Display a message that the app MUST reboot. Display the minimum required version.
                            StyledMessageBox.Show("UPDATE", "Bend has detected a mandatory update from your current version. Bend will now install the update.\n", true);
                            this.DoUpdate();
                        }
                    }
                    else
                    {
                        StyledMessageBox.Show("UPDATE", "You already have the latest version of Bend.\n", true);
                    }
                }
            }));
        }

        private void DoUpdate()
        {
            try
            {
                this.Dispatcher.Invoke(new Action(delegate { ProgressBar.Rect = new Rect(0, 0, 0, 5); }));

                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
                ad.UpdateAsync();
            }
            catch (Exception exception)
            {
                this.Dispatcher.Invoke(new Action(delegate { 
                    StyledMessageBox.Show("UPDATE", "Cannot install the latest version of Bend.\nPlease check your network connection, or try again later. Error: " + exception.Message + "\n", true);
                }));
            }
        }

        void ad_UpdateProgressChanged(object sender, DeploymentProgressChangedEventArgs e)
        {
            this.Dispatcher.Invoke( new Action( delegate { ProgressBar.Rect = new Rect(0, 0, (int)(125.0f * e.ProgressPercentage / 100), 5);} ) );   
        }

        void ad_UpdateCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(delegate
            {
                CheckForUpdatesButton.IsEnabled = isApplicationNetworkDeployed;
                if (!e.Cancelled)
                {
                    if (e.Error != null)
                    {
                        StyledMessageBox.Show("UPDATE", "Cannot install the latest version of Bend.\nPlease check your network connection, or try again later. Error: " + e.Error.Message + "\n", true);
                    }
                    else
                    {
                        StyledMessageBox.Show("UPDATE", "Bend has been upgraded, please save your work and restart application.\n", true);
                    }
                }
            }));
        }
        #endregion

        #region Integration Tab
        private static void WriteRegKeysForShellRightClickContextMenu()
        {
            try
            {
                RegistryKey HKCU = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                RegistryKey HKCU_STAR_SHELL = HKCU.CreateSubKey("Software");
                HKCU_STAR_SHELL = HKCU_STAR_SHELL.CreateSubKey("Classes");

                // Attempt to write the class id that describes bend
                {
                    RegistryKey HKCR_CLSID_UNIQUE = HKCU_STAR_SHELL.CreateSubKey("CLSID");
                    HKCR_CLSID_UNIQUE = HKCR_CLSID_UNIQUE.CreateSubKey("{29C436A6-392B-4069-8DF7-760271B08F67}");
                    HKCR_CLSID_UNIQUE.SetValue("", "Bend - A modern text editor (Explorer right click menu integration)");
                    string applicationId = "Bend.application, Culture = neutral, PublicKeyToken = 0000000000000000, processorArchitecture = x86";
                    HKCR_CLSID_UNIQUE.SetValue("AppId", applicationId);
                    HKCR_CLSID_UNIQUE.SetValue("DeploymentProviderUrl", "http://bend.codeplex.com/releases/clickonce/Bend.application");
                }

                // Write the registry entries that add bend to the right click menu
                HKCU_STAR_SHELL = HKCU_STAR_SHELL.CreateSubKey("*");
                HKCU_STAR_SHELL = HKCU_STAR_SHELL.CreateSubKey("Shell");
                RegistryKey BendShortcutKey = HKCU_STAR_SHELL.CreateSubKey("Bend");
                BendShortcutKey.SetValue("", "Bend file");
                string BendExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                BendShortcutKey.SetValue("Icon", BendExePath + ",0");
                BendShortcutKey.CreateSubKey("Command").SetValue("", "rundll32.exe dfshim.dll, ShOpenVerbExtension {29C436A6-392B-4069-8DF7-760271B08F67} %1");
            }
            catch
            {
            }
        }

        private void EnableContextMenu_Click(object sender, RoutedEventArgs e)
        {
            WriteRegKeysForShellRightClickContextMenu();
            this.UpdateButtons();
        }

        private void DisableContextMenu(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistryKey HKCU = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                RegistryKey HKCU_STAR_SHELL = HKCU.CreateSubKey("Software");
                HKCU_STAR_SHELL = HKCU_STAR_SHELL.CreateSubKey("Classes");
                HKCU_STAR_SHELL.CreateSubKey("CLSID").DeleteSubKeyTree("{29C436A6-392B-4069-8DF7-760271B08F67}");
                HKCU_STAR_SHELL = HKCU_STAR_SHELL.CreateSubKey("*");
                HKCU_STAR_SHELL = HKCU_STAR_SHELL.CreateSubKey("Shell");
                HKCU_STAR_SHELL.DeleteSubKeyTree("Bend");
            }
            catch
            {
            }
            this.UpdateButtons();
        }

        private static void UpdateEnvironmentPathWithExecutableDirectory(bool forceWrite)
        {
            try
            {
                RegistryKey HKCU_ENVIRONMENT = Registry.CurrentUser.CreateSubKey("Environment");
                string BendExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string bendDirectory = System.IO.Path.GetDirectoryName(BendExePath);
                string currentPath;
                if (HKCU_ENVIRONMENT.GetValue("Path") != null)
                {
                    currentPath = HKCU_ENVIRONMENT.GetValue("Path").ToString();
                    if (currentPath.IndexOf(bendDirectory) >= 0)
                    {
                        // We are already in the path nothing to do here.
                        return;
                    }
                    else if (currentPath.IndexOf("bend..tion") >= 0)
                    {
                        // There is a previous version of bend registered in the path, we need to replace it.
                        forceWrite = true;
                        string[] paths = currentPath.Split(';');
                        currentPath = "";
                        for (int i = 0; i < paths.Length; i++)
                        {
                            if (paths[i].IndexOf("bend..tion") < 0)
                            {
                                // This path is not the path the bend, retain it.
                                currentPath += paths[i];
                            }
                        }
                    }
                }
                else
                {
                    currentPath = "";
                }

                if (forceWrite)
                {
                    currentPath = bendDirectory + ";" + currentPath;
                    HKCU_ENVIRONMENT.SetValue("Path", currentPath);
                }
            }
            catch
            {
            }
        }

        private void AppendToPath_Click(object sender, RoutedEventArgs e)
        {
            UpdateEnvironmentPathWithExecutableDirectory(/*forceWrite*/true);
            UpdateButtons();
        }

        private void UpdateRegistryOnFirstRun()
        {
            RegistryKey HKCU = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            RegistryKey HKCU_SOFTWARE_CLASSES = HKCU.OpenSubKey("Software").OpenSubKey("Classes");
            RegistryKey BendShortcutKey = HKCU_SOFTWARE_CLASSES.OpenSubKey("*");
            BendShortcutKey = BendShortcutKey != null ? BendShortcutKey.OpenSubKey("Shell") : null;
            BendShortcutKey = BendShortcutKey != null ? BendShortcutKey.OpenSubKey("Bend") : null;
            if (BendShortcutKey != null && BendShortcutKey.GetValue("").ToString() == "Bend file")
            {
                // Previous install of Bend was registered for right click context menu. 
                // Enable the integration for this installation.
                WriteRegKeysForShellRightClickContextMenu();
            }
            UpdateEnvironmentPathWithExecutableDirectory(/*forceWrite*/false);
        }
        #endregion

        #region Plugins Tab
        private Plugins.JSBeautifyOptions GetAndPersistJsBeautifyOptions()
        {
            Plugins.JSBeautifyOptions jsBeautifyOptions = new Plugins.JSBeautifyOptions();
            if (this.JSBeautifyUseSpaces.IsChecked ?? true)
            {
                jsBeautifyOptions.indent_char = ' ';
                PersistantStorage.StorageObject.JSBeautifyUseSpaces = true;
                PersistantStorage.StorageObject.JSBeautifyUseTabs = false;
            }
            if (this.JSBeautifyUseTabs.IsChecked ?? true)
            {
                jsBeautifyOptions.indent_char = '\t';
                PersistantStorage.StorageObject.JSBeautifyUseTabs = true;
                PersistantStorage.StorageObject.JSBeautifyUseSpaces = false;
            }
            
            int indentSize; 
            if (int.TryParse(JSBeautifyIndent.Text, out indentSize))
            {
                jsBeautifyOptions.indent_size = indentSize;
                PersistantStorage.StorageObject.JSBeautifyIndent = indentSize;
            }

            if (JSBeautifyPreserveLine.IsChecked ?? true) 
            {
                jsBeautifyOptions.preserve_newlines = true;
                PersistantStorage.StorageObject.JSBeautifyPreserveLine = true;
            }
            else
            {
                jsBeautifyOptions.preserve_newlines = false;
                PersistantStorage.StorageObject.JSBeautifyPreserveLine = false;
            }
            return jsBeautifyOptions;
        }

        private void JSBeautifyFile(object sender, RoutedEventArgs e)
        {
            try
            {   
                Plugins.JSBeautify jsBeautify = new Plugins.JSBeautify(CurrentTab().TextEditor.Document.Text, GetAndPersistJsBeautifyOptions());
                string newFile = jsBeautify.GetResult();
                CurrentTab().TextEditor.ReplaceText(0, CurrentTab().TextEditor.Document.Text.Length, newFile);
                this.CancelSettingsUI();
            }
            catch
            {
            }
        }

        private void JSBeautifySelection(object sender, RoutedEventArgs e)
        {
            try
            {
                Plugins.JSBeautify jsBeautify = new Plugins.JSBeautify(CurrentTab().TextEditor.SelectedText, GetAndPersistJsBeautifyOptions());
                string formattedScript = jsBeautify.GetResult();
                CurrentTab().TextEditor.SelectedText = formattedScript;
                this.CancelSettingsUI();
            }
            catch
            {
            }
        }

        private void AllowOnlyDigits_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(e.Key == Key.D0 ||
                e.Key == Key.D1 ||
                e.Key == Key.D2 ||
                e.Key == Key.D3 ||
                e.Key == Key.D4 ||
                e.Key == Key.D5 ||
                e.Key == Key.D6 ||
                e.Key == Key.D7 ||
                e.Key == Key.D8 ||
                e.Key == Key.D9 ||
                e.Key == Key.Enter ||
                e.Key == Key.Back ||
                e.Key == Key.Escape ||
                e.Key == Key.Delete))
            {
                e.Handled = true;
            }            
        }

        private void HTMLTidyProcessFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                String tidyHTML = "";
                TidyNet objTidyNet = new TidyNet();

                // Set up options
                objTidyNet.Option.Clean(true);
                objTidyNet.Option.NewInlineTags("tidy");
                objTidyNet.Option.OutputType(EfTidyNet.EfTidyOpt.EOutputType.XhtmlOut);
                objTidyNet.Option.DoctypeMode(EfTidyNet.EfTidyOpt.EDoctypeModes.DoctypeAuto);
                objTidyNet.Option.Indent(EfTidyNet.EfTidyOpt.EIndentScheme.AUTOINDENT);
                objTidyNet.Option.TabSize(4);
                objTidyNet.Option.IndentSpace(4);

                objTidyNet.TidyMemToMem(CurrentTab().TextEditor.Document.Text, ref tidyHTML);

                int totalWarnings = 0;
                int totalErrors = 0;
                objTidyNet.TotalWarnings(ref totalWarnings);
                objTidyNet.TotalErrors(ref totalErrors);
                string error = objTidyNet.ErrorWarning();

                if (StyledMessageBox.Show("HTML TIDY FINISHED WITH " + totalErrors.ToString() + " ERRORS AND " + totalWarnings.ToString() + " WARNINGS",
                    error,
                    true))
                {
                    CurrentTab().TextEditor.ReplaceText(0, CurrentTab().TextEditor.Document.Text.Length, tidyHTML);
                }
                this.CancelSettingsUI();
            }
            catch
            {
            }
        }
        #endregion

        #region Options Tab
        private void UpdateOptions()
        {
            PersistantStorage persistantStorage = PersistantStorage.StorageObject;
            TextUseSpaces.IsChecked = persistantStorage.TextUseSpaces;
            TextUseTabs.IsChecked = persistantStorage.TextUseTabs;
            TextIndent.Text = persistantStorage.TextIndent.ToString();
            TextFormatShowFormatting.IsChecked = persistantStorage.TextShowFormatting;
            TextWordWrap.IsChecked = persistantStorage.TextWordWrap;
            SmoothScrolling.IsChecked = persistantStorage.SmoothScrolling;
            SyntaxHighlighting.IsChecked = persistantStorage.SyntaxHighlighting;
            SettingsPageAnimation.IsChecked = persistantStorage.SettingsPageAnimation;
            ShowStatusBar.IsChecked = persistantStorage.ShowStatusBar;
            PreserveIndent.IsChecked = persistantStorage.PreserveIndent;

            // Set up the font picker
            if (persistantStorage.DefaultFontFamilyIndex >= 0 && persistantStorage.DefaultFontFamilyIndex < FontPicker.Items.Count)
            {
                FontPicker.SelectedIndex = persistantStorage.DefaultFontFamilyIndex;
            }
            else
            {
                FontPicker.SelectedIndex = 0;
            }
            if (((System.Windows.Media.FontFamily)(FontPicker.Items[FontPicker.SelectedIndex])).Source != persistantStorage.DefaultFontFamily)
            {
                for (int fontFamilyIndex = 0; fontFamilyIndex < FontPicker.Items.Count; fontFamilyIndex++)
                {
                    if (((System.Windows.Media.FontFamily)(FontPicker.Items[fontFamilyIndex])).Source == persistantStorage.DefaultFontFamily)
                    {
                        FontPicker.SelectedIndex = fontFamilyIndex;
                        persistantStorage.DefaultFontFamilyIndex = fontFamilyIndex;
                        break;
                    }
                }
            }

            ThemePicker.SelectedIndex = PersistantStorage.StorageObject.DefaultThemeIndex;
        }

        private void OptionsCancel_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateOptions();
            this.CancelSettingsUI();
        }

        private void SetTheme()
        {
            PersistantStorage persistantStorage = PersistantStorage.StorageObject;
            switch (persistantStorage.DefaultThemeIndex)
            {
                case 0:
                    // Light Theme
                    persistantStorage.BackgroundColor = System.Windows.Media.Colors.WhiteSmoke;
                    persistantStorage.ForegroundColor = System.Windows.Media.Colors.Black;
                    persistantStorage.BaseBackgroundImage = "Images/FrontBackground.png";
                    persistantStorage.ScrollButtonColor = System.Windows.Media.Color.FromRgb(208, 208, 208);
                    persistantStorage.LogoBackgroundColor = System.Windows.Media.Color.FromRgb(54, 80, 128);
                    persistantStorage.LogoForegroundColor = System.Windows.Media.Colors.White;
                    persistantStorage.MenuSelectedBackgroundColor = System.Windows.Media.Color.FromRgb(221, 221, 221);

                    persistantStorage.DefaultForegroundColor = System.Windows.Media.Color.FromRgb(0, 0, 0);
                    persistantStorage.DefaultBackgroundColor = System.Windows.Media.Color.FromRgb(245, 245, 245);
                    persistantStorage.DefaultSelectionColor = System.Windows.Media.Color.FromRgb(106, 124, 159);
                    persistantStorage.DefaultSelectionOutlineColor = System.Windows.Media.Color.FromRgb(94, 114, 153);
                    persistantStorage.DefaultSelectionDimColor = System.Windows.Media.Color.FromArgb(128, 245, 245, 245);
                    persistantStorage.LineNumberColor = System.Windows.Media.Color.FromRgb(140, 140, 140);

                    persistantStorage.DefaultShowFormattingColor = System.Windows.Media.Color.FromRgb(189, 189, 189);
                    persistantStorage.DefaultShowFormattingColorAlt = System.Windows.Media.Color.FromRgb(230, 230, 230);

                    persistantStorage.SyntaxHighlightingKeyword1 = System.Windows.Media.Color.FromRgb(0, 102, 153);
                    persistantStorage.SyntaxHighlightingKeyword2 = System.Windows.Media.Color.FromRgb(0, 0, 128);
                    persistantStorage.SyntaxHighlightingKeyword3 = System.Windows.Media.Color.FromRgb(0, 0, 255);
                    persistantStorage.SyntaxHighlightingKeyword4 = System.Windows.Media.Color.FromRgb(0, 0, 255);
                    persistantStorage.SyntaxHighlightingKeyword5 = System.Windows.Media.Color.FromRgb(0, 0, 255);
                    persistantStorage.SyntaxHighlightingKeyword6 = System.Windows.Media.Color.FromRgb(139, 0, 0);
                    persistantStorage.SyntaxHighlightingPreProcessorKeyword = System.Windows.Media.Color.FromRgb(0, 128, 0);
                    persistantStorage.SyntaxHighlightingPreProcessor = System.Windows.Media.Color.FromRgb(0, 155, 91);
                    persistantStorage.SyntaxHighlightingComment = System.Windows.Media.Color.FromRgb(170, 170, 170);
                    persistantStorage.SyntaxHighlightingOperator = System.Windows.Media.Color.FromRgb(230, 51, 51);
                    persistantStorage.SyntaxHighlightingBracket = System.Windows.Media.Color.FromRgb(250, 51, 51);
                    persistantStorage.SyntaxHighlightingNumber = System.Windows.Media.Color.FromRgb(184, 134, 11);
                    persistantStorage.SyntaxHighlightingString = System.Windows.Media.Color.FromRgb(0, 100, 0);
                    persistantStorage.SyntaxHighlightingChar = System.Windows.Media.Color.FromRgb(0, 100, 0);

                    break;
                case 1:

                    // Dark Theme
                    persistantStorage.BackgroundColor = System.Windows.Media.Color.FromRgb(30,30,30);
                    persistantStorage.ForegroundColor = System.Windows.Media.Colors.Silver;
                    persistantStorage.BaseBackgroundImage = "Images/SettingsPattern.png";
                    persistantStorage.ScrollButtonColor = System.Windows.Media.Color.FromRgb(153, 153, 153);
                    persistantStorage.LogoBackgroundColor = System.Windows.Media.Color.FromRgb(243,243,26);
                    persistantStorage.LogoForegroundColor = System.Windows.Media.Colors.Black;
                    persistantStorage.MenuSelectedBackgroundColor = System.Windows.Media.Color.FromRgb(51, 51, 43);

                    persistantStorage.DefaultForegroundColor = System.Windows.Media.Color.FromRgb(250, 250, 250);
                    persistantStorage.DefaultBackgroundColor = System.Windows.Media.Color.FromRgb(30, 30, 30);
                    persistantStorage.DefaultSelectionColor = System.Windows.Media.Color.FromRgb(62, 47, 132);
                    persistantStorage.DefaultSelectionOutlineColor = System.Windows.Media.Color.FromRgb(38, 29, 81);
                    persistantStorage.DefaultSelectionDimColor = System.Windows.Media.Color.FromArgb(128, 30, 30, 30);
                    persistantStorage.LineNumberColor = System.Windows.Media.Color.FromRgb(150, 150, 150);

                    persistantStorage.DefaultShowFormattingColor = System.Windows.Media.Color.FromRgb(51, 51, 51);
                    persistantStorage.DefaultShowFormattingColorAlt = System.Windows.Media.Color.FromRgb(90, 90, 90);

                    persistantStorage.SyntaxHighlightingKeyword1 = System.Windows.Media.Color.FromRgb(146, 202, 244);
                    persistantStorage.SyntaxHighlightingKeyword2 = System.Windows.Media.Color.FromRgb(86, 156, 214);
                    persistantStorage.SyntaxHighlightingKeyword3 = System.Windows.Media.Color.FromRgb(102, 189, 255);
                    persistantStorage.SyntaxHighlightingKeyword4 = System.Windows.Media.Color.FromRgb(0, 171, 171);
                    persistantStorage.SyntaxHighlightingKeyword5 = System.Windows.Media.Color.FromRgb(0, 171, 171);
                    persistantStorage.SyntaxHighlightingKeyword6 = System.Windows.Media.Color.FromRgb(255, 206, 6);
                    persistantStorage.SyntaxHighlightingPreProcessorKeyword = System.Windows.Media.Color.FromRgb(0, 158, 0);
                    persistantStorage.SyntaxHighlightingPreProcessor = System.Windows.Media.Color.FromRgb(0, 205, 91);
                    persistantStorage.SyntaxHighlightingComment = System.Windows.Media.Color.FromRgb(170, 170, 170);
                    persistantStorage.SyntaxHighlightingOperator = System.Windows.Media.Color.FromRgb(220, 251, 121);
                    persistantStorage.SyntaxHighlightingBracket = System.Windows.Media.Color.FromRgb(220, 251, 121);
                    persistantStorage.SyntaxHighlightingNumber = System.Windows.Media.Color.FromRgb(6, 233, 255);
                    persistantStorage.SyntaxHighlightingString = System.Windows.Media.Color.FromRgb(116, 194, 113);
                    persistantStorage.SyntaxHighlightingChar = System.Windows.Media.Color.FromRgb(6, 255, 164);

                    break;
            }
        }

        private void OptionsSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                PersistantStorage persistantStorage = PersistantStorage.StorageObject;
                persistantStorage.TextUseSpaces = TextUseSpaces.IsChecked ?? true;
                persistantStorage.TextUseTabs = TextUseTabs.IsChecked ?? true;

                int indentSize;
                if (int.TryParse(TextIndent.Text, out indentSize))
                {
                    persistantStorage.TextIndent = indentSize;
                }

                persistantStorage.TextShowFormatting = TextFormatShowFormatting.IsChecked ?? true;
                persistantStorage.SmoothScrolling = SmoothScrolling.IsChecked ?? true;
                persistantStorage.SyntaxHighlighting = SyntaxHighlighting.IsChecked ?? true;
                persistantStorage.TextWordWrap = TextWordWrap.IsChecked ?? true;
                persistantStorage.SettingsPageAnimation = SettingsPageAnimation.IsChecked ?? true;
                persistantStorage.ShowStatusBar = ShowStatusBar.IsChecked ?? true;
                persistantStorage.DefaultFontFamilyIndex = FontPicker.SelectedIndex;
                persistantStorage.DefaultFontFamily = ((System.Windows.Media.FontFamily)(FontPicker.Items[FontPicker.SelectedIndex])).Source;
                persistantStorage.DefaultThemeIndex = ThemePicker.SelectedIndex;
                persistantStorage.PreserveIndent = PreserveIndent.IsChecked ?? true;

                SetTheme();
                MainWindow_LoadOptions();                
                this.CancelSettingsUI();
            }
            catch
            {
                
            }
        }
        #endregion
    }
}
