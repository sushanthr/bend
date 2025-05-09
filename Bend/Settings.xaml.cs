﻿using System;
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

            // Load up list of themes
            try
            {
                string themesDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Themes\\";
                if (System.IO.Directory.Exists(themesDirectory))
                {
                    string[] themeFiles = System.IO.Directory.GetFiles(themesDirectory, "*.xml");
                    for (int i =0; i < themeFiles.Length; i++)
                    {
                        string themeName = System.IO.Path.GetFileNameWithoutExtension(themeFiles[i]);
                        Label themeLabel = new Label();
                        themeLabel.Content = themeName;
                        themeLabel.Height = 20;
                        themeLabel.Padding = new Thickness(2, 0, 0, 0);
                        ThemePicker.Items.Add(themeLabel);
                    }
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
                            if (paths[i].IndexOf("bend..tion") < 0 && paths[i] != string.Empty)
                            {
                                // This path is not the path of old bend, retain it in the new PATH string.
                                currentPath += paths[i];
                                currentPath += ";";
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
                    if (!currentPath.EndsWith(";"))
                    {
                        currentPath = currentPath + ";";
                    }
                    currentPath = currentPath + bendDirectory;
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
            ReopenFilesOnStart.IsChecked = persistantStorage.ReopenFilesOnStart;
            Diagnostics.IsChecked = persistantStorage.Diagnostics;

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

            for (int i =0; i < ThemePicker.Items.Count; i++)
            {
                string themeName = (string)(((Label)ThemePicker.Items[i]).Content);
                if (themeName == PersistantStorage.StorageObject.CurrentThemeFilename)
                {
                    ThemePicker.SelectedIndex = i;
                    break;
                }            
            }
        }

        private void OptionsCancel_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateOptions();
            this.CancelSettingsUI();
        }

        private void SetTheme()
        {
            PersistantStorage persistantStorage = PersistantStorage.StorageObject;
            persistantStorage.CurrentTheme = ThemeSettings.LoadThemeSettings(persistantStorage.CurrentThemeFilename);                        
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
                persistantStorage.ReopenFilesOnStart = ReopenFilesOnStart.IsChecked ?? true;
                persistantStorage.Diagnostics = Diagnostics.IsChecked ?? true;
                persistantStorage.SyntaxHighlighting = SyntaxHighlighting.IsChecked ?? true;
                persistantStorage.TextWordWrap = TextWordWrap.IsChecked ?? true;
                persistantStorage.SettingsPageAnimation = SettingsPageAnimation.IsChecked ?? true;
                persistantStorage.ShowStatusBar = ShowStatusBar.IsChecked ?? true;
                persistantStorage.DefaultFontFamilyIndex = FontPicker.SelectedIndex;
                persistantStorage.DefaultFontFamily = ((System.Windows.Media.FontFamily)(FontPicker.Items[FontPicker.SelectedIndex])).Source;
                persistantStorage.CurrentThemeFilename = (string)((Label)ThemePicker.Items[ThemePicker.SelectedIndex]).Content;
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
