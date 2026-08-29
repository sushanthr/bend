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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Shell;
using Microsoft.Win32;
using System.Collections;
using TextCoreControl;
using Microsoft.Terminal.Wpf;
using Bend.Controls;
using Bend.SourceControl;

namespace Bend
{ 
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x00080000;

        [DllImport("user32.dll")]
        private extern static int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")]
        private extern static int GetWindowLong(IntPtr hwnd, int index);

        #region Member Data
        HwndSource mainWindow;

        Window findAndReplaceWindow;

        List<Tab> tab;
        int currentTabIndex;
        List<Console.TerminalControl> terminalSessions;
        int currentTerminalIndex = -1;
        string terminalStartupCommand = "pwsh.exe -NoLogo";
        readonly List<Console.TerminalControl> agentTerminalSessions = new List<Console.TerminalControl>();
        int currentAgentTerminalIndex = -1;
        bool agentPaneExpanded;

        WindowChrome windowChrome;

        bool isFullScreen;
        bool isInSettingsAnimation;
        bool holdInitialReferenceStatus = true;

        StatusType currentStatusType;

        InterBendCommunication interBendCommuncation;
        
        TabTitle dragDropSource;
        TabDragVisual tabDragVisual;
        bool dropWasConsumedAsTabMove;
        bool extendDragDrop;
        private string currentFolderPath;
        private Tab treePreviewTab;
        private const string LineEndingsOnlyStatus = "ONLY LINE ENDINGS CHANGED";
        private readonly IGitService diffGitService = new GitService();
        private readonly Dictionary<string, string> sessionFileBaselines =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private System.Threading.CancellationTokenSource diffBaseCancellation;
        private bool initializingDiffMode;
        #endregion

        #region Public API

        public MainWindow()
        {
            InitializeComponent();
            terminalSessions = new List<Console.TerminalControl> { Terminal };
            EnsureTerminalTab(Terminal);
            FontFamily shellFont = new FontFamily("Segoe UI Variable Text");
            this.FontFamily = shellFont;
            MainWindowGrid.RowDefinitions[0].Height = new GridLength(49.33);
            WindowControls.Height = 49.33;
            foreach (FrameworkElement control in WindowControls.Children) control.Height = 49.33;
            MenuItem logoMenuItem = Logo.Items[0] as MenuItem;
            if (logoMenuItem != null)
            {
                logoMenuItem.Height = 49.33;
                logoMenuItem.Padding = new Thickness(22, 0, 22, 0);
                logoMenuItem.FontFamily = new FontFamily("Segoe UI Variable Display");
                logoMenuItem.FontWeight = FontWeights.SemiBold;
            }
            Border headerSeparator = new Border
            {
                Height = 0.67,
                Background = (Brush)Resources["ShellBorderBrush"],
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            };
            Grid.SetRow(headerSeparator, 0);
            Panel.SetZIndex(headerSeparator, 20);
            MainWindowGrid.Children.Add(headerSeparator);
            Border clientBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(207, 207, 207)),
                BorderThickness = new Thickness(0.67),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(clientBorder, 100);
            ClientAreaGrid.Children.Add(clientBorder);

            Grid bottomChrome = MainWindowGrid.Children.OfType<Grid>().FirstOrDefault(child => Grid.GetRow(child) == 2);
            if (bottomChrome != null)
            {
                TextBlock terminalLabel = bottomChrome.Children.OfType<TextBlock>().FirstOrDefault();
                if (terminalLabel != null)
                {
                    terminalLabel.FontFamily = shellFont;
                    terminalLabel.FontSize = 13.3;
                    terminalLabel.Margin = new Thickness(14.7, 0, 0, 0);
                    terminalLabel.RenderTransform = new TranslateTransform(0, -2.7);
                }
                StackPanel terminalActions = bottomChrome.Children.OfType<StackPanel>().FirstOrDefault();
                if (terminalActions != null)
                {
                    terminalActions.RenderTransform = new TranslateTransform(-5, 0);
                    Grid shellSelector = terminalActions.Children.OfType<Grid>().FirstOrDefault();
                    if (shellSelector != null) shellSelector.RenderTransform = new TranslateTransform(-13, 0);
                }
            }
            StatusBar.Margin = new Thickness(0, 0, 16, 0);
            foreach (TextBlock text in StatusBar.Children.OfType<TextBlock>()) text.FontFamily = shellFont;
            foreach (Label label in StatusBar.Children.OfType<Label>()) label.FontFamily = shellFont;
            SearchHint.FontFamily = shellFont;
            FindText.FontFamily = shellFont;
            SearchHint.RenderTransform = new TranslateTransform(5, 0);

            Path searchGlyph = SearchActivityButton.Content as Path;
            if (searchGlyph != null)
            {
                searchGlyph.Width = 18;
                searchGlyph.Height = 18;
            }
            findAndReplaceWindow = null;
            var style = (Style)Resources["PlainStyle"];
            this.Style = style;
            tab = new List<Tab>();
            InitializeDiffModeControl();
            double savedWidth = PersistantStorage.StorageObject.mainWindowWidth;
            double savedHeight = PersistantStorage.StorageObject.mainWindowHeight;
            this.Width = double.IsNaN(savedWidth) || savedWidth < 200 ? 800 : Math.Min(savedWidth, SystemParameters.VirtualScreenWidth);
            this.Height = double.IsNaN(savedHeight) || savedHeight < 150 ? 600 : Math.Min(savedHeight, SystemParameters.VirtualScreenHeight);
            double savedLeft = PersistantStorage.StorageObject.mainWindowLeft;
            double savedTop = PersistantStorage.StorageObject.mainWindowTop;
            bool intersectsDesktop = savedLeft + this.Width >= SystemParameters.VirtualScreenLeft && savedLeft <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                savedTop + this.Height >= SystemParameters.VirtualScreenTop && savedTop <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
            this.Left = intersectsDesktop ? savedLeft : SystemParameters.WorkArea.Left + 40;
            this.Top = intersectsDesktop ? savedTop : SystemParameters.WorkArea.Top + 40;
            this.windowChrome = new WindowChrome();
            this.windowChrome.ResizeBorderThickness = new Thickness(4);
            this.windowChrome.CaptionHeight = 40;
            this.windowChrome.GlassFrameThickness = new Thickness(1);
            this.windowChrome.CornerRadius = new CornerRadius(0);
            this.windowChrome.NonClientFrameEdges = NonClientFrameEdges.None;
            WindowChrome.SetWindowChrome(this, this.windowChrome);
            this.isFullScreen = false;
            this.currentStatusType = StatusType.STATUS_OTHER;
            this.dropWasConsumedAsTabMove = false;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(Logo, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(SettingsLogo, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(FindText, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(AgentButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(BackButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(FullscreenButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(MaxButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(MinButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(QuitButton, /*hitTestVisible*/true);
            string savedWorkspace = PersistantStorage.StorageObject.LastWorkspaceFolder;
            if (!string.IsNullOrWhiteSpace(savedWorkspace) && System.IO.Directory.Exists(savedWorkspace))
                SetCurrentFolder(savedWorkspace);
            else if (System.IO.Directory.Exists(Environment.CurrentDirectory))
                SetCurrentFolder(Environment.CurrentDirectory, false);
        }

        internal Tab CurrentTab {
            get
            {
                if (this.currentTabIndex >= 0 && this.currentTabIndex < this.tab.Count)
                {
                    return this.tab[this.currentTabIndex];
                }
                else
                {
                    return null;
                }
            }
        }

        internal void LoadOptions()
        {
            // Theme definitions are authoritative. Persisted ThemeSettings instances may
            // predate newly added colors and otherwise keep stale serialized values.
            PersistantStorage.StorageObject.CurrentTheme = ThemeSettings.LoadThemeSettings(PersistantStorage.StorageObject.CurrentThemeFilename);
            StatusBar.Visibility = PersistantStorage.StorageObject.ShowStatusBar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

            Application.Current.Resources["BackgroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.BackgroundColor);
            Application.Current.Resources["BackgroundTerminalBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.BackgroundTerminalColor);
            Application.Current.Resources["EditorSurfaceBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.DefaultBackgroundColor);
            uint terminalBackground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorBackground;
            Application.Current.Resources["TerminalColorBrush"] = new SolidColorBrush(Color.FromRgb(
                (byte)(terminalBackground & 0xFF),
                (byte)((terminalBackground >> 8) & 0xFF),
                (byte)((terminalBackground >> 16) & 0xFF)));
            Application.Current.Resources["ForegroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.ForegroundColor);
            Application.Current.Resources["ScrollButtonBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.ScrollButtonColor);
            Application.Current.Resources["LogoForegroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.LogoForegroundColor);
            Application.Current.Resources["LogoBackgroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.LogoBackgroundColor);
            Application.Current.Resources["MenuSelectedBackgroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.MenuSelectedBackgroundColor);

            Color shellBackground = PersistantStorage.StorageObject.CurrentTheme.BackgroundColor;
            Color shellForeground = PersistantStorage.StorageObject.CurrentTheme.ForegroundColor;
            bool isDarkTheme = (shellBackground.R + shellBackground.G + shellBackground.B) < 384;
            ThemeSettings currentTheme = PersistantStorage.StorageObject.CurrentTheme;
            Application.Current.Resources["TabBackgroundBrush"] = new SolidColorBrush(currentTheme.TabBackgroundColor.A > 0
                ? currentTheme.TabBackgroundColor : currentTheme.BackgroundColor);
            Application.Current.Resources["ActivityBarBrush"] = new SolidColorBrush(currentTheme.ActivityBarColor.A > 0
                ? currentTheme.ActivityBarColor : currentTheme.BackgroundColor);
            Application.Current.Resources["SourceControlStatusBrush"] = new SolidColorBrush(currentTheme.SourceControlStatusColor.A > 0
                ? currentTheme.SourceControlStatusColor : Color.FromRgb(214, 168, 75));
            Application.Current.Resources["ErrorForegroundBrush"] = new SolidColorBrush(currentTheme.ErrorForegroundColor.A > 0
                ? currentTheme.ErrorForegroundColor : Color.FromRgb(224, 108, 117));
            this.Resources["ShellChromeBrush"] = new SolidColorBrush(currentTheme.ShellChromeColor.A > 0
                ? currentTheme.ShellChromeColor : BlendColor(shellBackground, shellForeground, isDarkTheme ? 0.05 : 0.04));
            this.Resources["ShellPanelBrush"] = new SolidColorBrush(currentTheme.ShellPanelColor.A > 0
                ? currentTheme.ShellPanelColor : BlendColor(shellBackground, shellForeground, isDarkTheme ? 0.025 : 0.016));
            this.Resources["ShellBorderBrush"] = new SolidColorBrush(currentTheme.ShellBorderColor.A > 0
                ? currentTheme.ShellBorderColor : BlendColor(shellBackground, shellForeground, isDarkTheme ? 0.25 : 0.15));
            this.Resources["ShellMutedBrush"] = new SolidColorBrush(currentTheme.ShellMutedColor.A > 0
                ? currentTheme.ShellMutedColor : BlendColor(shellBackground, shellForeground, isDarkTheme ? 0.68 : 0.51));
            
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultForegroundColor, ref TextCoreControl.Settings.DefaultForegroundColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultBackgroundColor, ref TextCoreControl.Settings.DefaultBackgroundColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultSelectionColor, ref TextCoreControl.Settings.DefaultSelectionColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultSelectionOutlineColor, ref TextCoreControl.Settings.DefaultSelectionOutlineColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultSelectionDimColor, ref TextCoreControl.Settings.DefaultSelectionDimColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultBackgroundHighlightColor, ref TextCoreControl.Settings.DefaultBackgroundHighlightColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DiffAddedBackgroundColor, ref TextCoreControl.Settings.DiffAddedBackgroundColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DiffRemovedBackgroundColor, ref TextCoreControl.Settings.DiffRemovedBackgroundColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DiffPaddingPatternColor, ref TextCoreControl.Settings.DiffPaddingPatternColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.LineNumberColor, ref TextCoreControl.Settings.LineNumberColor);

            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultShowFormattingColor, ref TextCoreControl.Settings.DefaultShowFormattingColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultShowFormattingColorAlt, ref TextCoreControl.Settings.DefaultShowFormattingColorAlt);

            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingKeyword1, ref TextCoreControl.Settings.SyntaxHighlightingKeyword1);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingKeyword2, ref TextCoreControl.Settings.SyntaxHighlightingKeyword2);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingKeyword3, ref TextCoreControl.Settings.SyntaxHighlightingKeyword3);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingKeyword4, ref TextCoreControl.Settings.SyntaxHighlightingKeyword4);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingKeyword5, ref TextCoreControl.Settings.SyntaxHighlightingKeyword5);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingKeyword6, ref TextCoreControl.Settings.SyntaxHighlightingKeyword6);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingPreProcessorKeyword, ref TextCoreControl.Settings.SyntaxHighlightingPreProcessorKeyword);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingPreProcessor, ref TextCoreControl.Settings.SyntaxHighlightingPreProcessor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingComment, ref TextCoreControl.Settings.SyntaxHighlightingComment);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingOperator, ref TextCoreControl.Settings.SyntaxHighlightingOperator);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingBracket, ref TextCoreControl.Settings.SyntaxHighlightingBracket);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingNumber, ref TextCoreControl.Settings.SyntaxHighlightingNumber);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingString, ref TextCoreControl.Settings.SyntaxHighlightingString);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.SyntaxHighlightingChar, ref TextCoreControl.Settings.SyntaxHighlightingChar);

            for (int i = 0; i < this.tab.Count; i++)
            {
                tab[i].LoadOptions();
            }

            for (int i = Editor.ContextMenu.Items.Count - 1; i >= 0; i--)
            {
                MenuItem menuItem = Editor.ContextMenu.Items[i] as MenuItem;
                if (menuItem != null && menuItem.Header.ToString() == "Record")
                {
                    if (PersistantStorage.StorageObject.Diagnostics)
                    { 
                        menuItem.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        menuItem.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }
            }
        }

        private static Color BlendColor(Color from, Color to, double amount)
        {
            return Color.FromArgb(
                255,
                (byte)(from.R + ((to.R - from.R) * amount)),
                (byte)(from.G + ((to.G - from.G) * amount)),
                (byte)(from.B + ((to.B - from.B) * amount)));
        }
        #endregion

        #region Window management

        public void Window_SourceInitialized(object sender, EventArgs e)
        {
            this.LoadOptions();

            this.mainWindow = PresentationSource.FromVisual((Visual)this) as HwndSource;
            // Remove the default window buttons
            int style = GetWindowLong(this.mainWindow.Handle, GWL_STYLE);
            SetWindowLong(this.mainWindow.Handle, GWL_STYLE, style & ~WS_SYSMENU);
#if DEBUG
            System.Diagnostics.Debug.Assert(RenderCapability.Tier == 0x00020000);
            RenderCapability.TierChanged += new EventHandler(RenderCapability_TierChanged);
#endif

            // Reopen from explorer or last session or create empty tab
            bool tabOpened = false;
            try
            {
                string[] fileNames;
                if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null)
                {
                    fileNames = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                }
                else
                {
                    fileNames = null;
                }

                if (fileNames != null)
                {
                    if (fileNames.Length == 1 && fileNames[0].StartsWith(BEND_SERIALIZED_TABDATA_PREFIX))
                    {
                        tabOpened = this.LoadSerializedTabData(fileNames[0]);
                    }
                    else
                    {
                        tabOpened = AddNewTabWithFiles(fileNames);
                    }
                }
                else if ((fileNames == null || fileNames.Length <= 0) && PersistantStorage.StorageObject.ReopenFilesOnStart) 
                {
                        tabOpened = AddNewTabWithFiles(PersistantStorage.StorageObject.mruFile);
                }                
            }
            catch
            {
            }
            bool isInitialUntitledDocument = false;
            if (!tabOpened)
            {
                this.AddNewTab();
                isInitialUntitledDocument = true;
            }
            this.currentTabIndex = this.tab.Count - 1;
            if (this.currentTabIndex >= 0)
            {
                this.tab[this.currentTabIndex].Title.Opacity = 1;
                this.tab[this.currentTabIndex].Content.Visibility = Visibility.Visible;
                this.SetFocusAfterTextEditorInitialization();
            }
            UpdateEditorChrome();
            if (isInitialUntitledDocument)
            {
                TabStrip.Visibility = Visibility.Visible;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Line.Content = "0";
                    Column.Content = "0";
                }), DispatcherPriority.ApplicationIdle);
            }

            System.Windows.Media.Animation.Storyboard settingsAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("slideSettingsOut");
            settingsAnimation.Completed += new EventHandler(slideSettingsOutAnimation_Completed);
            settingsAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("slideSettingsIn");
            settingsAnimation.Completed += new EventHandler(slideSettingsInAnimation_Completed);
            isInSettingsAnimation = false;

            interBendCommuncation = new InterBendCommunication(mainWindow);
            interBendCommuncation.RecivedFileNameEvent += new InterBendCommunication.RecivedFileNameEventHandler(RecivedFileNameEvent);

            this.QueryContinueDrag += TabDrag_QueryContinueDrag;
        }
        
        private void ReopenLastSession(object sender, RoutedEventArgs e)
        {
            this.AddNewTabWithFiles(PersistantStorage.StorageObject.mruFile);
        }

        bool AddNewTabWithFiles(string[] fileNames)
        {
            bool tabOpened = false;

            if (fileNames != null)
            {
                for (int mruCount = 0; mruCount < fileNames.Length; mruCount++)
                {
                    string fileName = fileNames[mruCount];
                    if (System.IO.File.Exists(fileName))
                    {
                        this.AddNewTab();
                        int lastTab = this.tab.Count - 1;
                        if (!this.tab[lastTab].OpenFile(fileName))
                        {
                            this.TabClose(lastTab);
                            continue;
                        }
                        this.tab[lastTab].Title.Opacity = 0.72;
                        this.tab[lastTab].Content.Visibility = Visibility.Hidden;
                        tabOpened = true;
                    }
                }
            }

            return tabOpened;
        }

        void RenderCapability_TierChanged(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.Assert(false, "Switching to software rendering mode !");
        }
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save file name to MRU list
            try
            {
                PersistantStorage.StorageObject.mruFile = new String[this.tab.Count];
                for (int mruCount = 0; mruCount < this.tab.Count; mruCount++)
                {
                    PersistantStorage.StorageObject.mruFile[mruCount] = this.tab[mruCount].FullFileName;
                }

                if (this.WindowState == System.Windows.WindowState.Normal)
                {
                    PersistantStorage.StorageObject.mainWindowTop = this.Top;
                    PersistantStorage.StorageObject.mainWindowLeft = this.Left;
                    PersistantStorage.StorageObject.mainWindowWidth = this.Width;
                    PersistantStorage.StorageObject.mainWindowHeight = this.Height;
                }
                if (BottomChrome.RowDefinitions[2].ActualHeight > 0) PersistantStorage.StorageObject.BottomTerminalHeight = BottomChrome.RowDefinitions[2].ActualHeight;
                if (SidePaneColumn.ActualWidth > 0) PersistantStorage.StorageObject.LeftPaneWidth = SidePaneColumn.ActualWidth;
                if (AgentPaneColumn.ActualWidth > 0) PersistantStorage.StorageObject.AgentPaneWidth = AgentPaneColumn.ActualWidth;
            }
            catch
            {
            }

            // Close tabs with no pending content
            for (int i = tab.Count - 1; i >= 0; i--)
            {
                if (!this.tab[i].TextEditor.Document.HasUnsavedContent)
                {
                    this.TabClose(i);
                }
            }

            // Close tabs with pending content.
            for (int i = tab.Count - 1; i >= 0; i--)
            {
                this.TabClose(i);
            }

            if (tab.Count != 0)
            {
                e.Cancel = true;
            }
            else
            {
                try { PersistantStorage.Save(); }
                catch (Exception exception) { StyledMessageBox.Show("SETTINGS", "Bend could not save its settings: " + exception.Message); }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        public const UInt32 FLASHW_ALL = 3;
        void RecivedFileNameEvent(string fileName)
        {
            // If the settings page is the one in view, come out of it.
            if (Settings.Visibility != System.Windows.Visibility.Hidden)
            {
                BackImage_MouseDown(null, null);
            }
            this.AddTabWithFile(fileName);
            FLASHWINFO fInfo = new FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = this.mainWindow.Handle;
            fInfo.dwFlags = FLASHW_ALL;
            fInfo.uCount = 1;
            fInfo.dwTimeout = 0;

            FlashWindowEx(ref fInfo);
            this.Activate();
        }
        
        private bool AddTabWithFile(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                Tab newTab = new Tab();
                tab.Add(newTab);
                // Hook up tab band event handlers
                newTab.Title.MouseLeftButtonUp += this.TabClick;
                newTab.Title.ContextMenu = (ContextMenu)Resources["TabTitleContextMenu"];
                newTab.Title.CloseButtonClicked += this.TabClose;
                newTab.TextEditor.DisplayManager.CaretPositionChanged += TextEditor_CaretPositionChanged;
                newTab.TextEditor.Document.LanguageChanged += Document_LanguageChanged;
                newTab.Title.MouseMove += TabTitle_MouseMove;

                newTab.Title.Opacity = 0.72;
                newTab.Content.Visibility = Visibility.Hidden;

                TabBar.Children.Add(newTab.Title);
                Editor.Children.Add(newTab.Content);
                UpdateEditorChrome();
                newTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);
                newTab.TextEditor.DisplayManager.SelectionChange += DisplayManager_SelectionChange;
                if (!newTab.OpenFile(filePath))
                {
                    TabBar.Children.Remove(newTab.Title);
                    Editor.Children.Remove(newTab.Content);
                    newTab.Close();
                    tab.Remove(newTab);
                    return false;
                }
                CaptureSessionBaseline(newTab);

                // Switch focus to the new file
                if (currentTabIndex >= 0)
                {
                    tab[currentTabIndex].Content.Visibility = Visibility.Hidden;
                    tab[currentTabIndex].Title.Opacity = 0.72;
                }

                int newTabFocus = tab.Count - 1;
                this.currentTabIndex = newTabFocus;
                tab[newTabFocus].Title.Opacity = 1.0;
                tab[newTabFocus].Content.Visibility = Visibility.Visible;
                SetFocusAfterTextEditorInitialization();
                _ = ApplyDiffModeToTabAsync(newTab, GetSelectedDiffMode());

                StatusBar.Visibility = PersistantStorage.StorageObject.ShowStatusBar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
                return true;
            }
            return false;
        }

        /// <summary>
        ///     The text editor can't set focus on itself until its render target has been created and
        ///     it is fully initialized. This method adds a delay to accomodate for the editor initialization.
        /// </summary>
        private void SetFocusAfterTextEditorInitialization()
        {
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += SetFocusAfterTextEditorInitialization_TimerEvent;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        void SetFocusAfterTextEditorInitialization_TimerEvent(object sender, EventArgs e)
        {
            if (this.currentTabIndex >= 0 && this.currentTabIndex < this.tab.Count)
            {
                tab[this.currentTabIndex].TextEditor.SetFocus();
            }
            DispatcherTimer timer = sender as DispatcherTimer;
            if (timer != null)
            {
                timer.Stop();
            }
        }

        private void MinimizeButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        private void MaximizeButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                this.ResetFullScreen();
            }
            else
            {
                this.WindowState = System.Windows.WindowState.Maximized;
            }
        }

        private void QuitButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }
        
        private void FullscreenButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.isFullScreen)
            {
                this.ResetFullScreen();
            }
            else
            {
                this.WindowStyle = System.Windows.WindowStyle.None;
                WindowChrome.SetWindowChrome(this, null);
                this.WindowState = System.Windows.WindowState.Normal;
                this.WindowState = System.Windows.WindowState.Maximized;
                this.isFullScreen = true;
                FullscreenButton.Foreground = (Brush)Application.Current.Resources["LogoBackgroundBrush"];
            }
        }

        private void ResetFullScreen()
        {
            WindowChrome.SetWindowChrome(this, windowChrome);
            this.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
            this.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
            this.WindowState = System.Windows.WindowState.Normal;
            FullscreenButton.ClearValue(Control.ForegroundProperty);
            this.isFullScreen = false;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                ClientAreaGrid.Margin = new Thickness(4);
                this.windowChrome.GlassFrameThickness = new Thickness(0);
                this.ResizeCrimp.Visibility = System.Windows.Visibility.Hidden;
                MaxButton.Content = new Path
                {
                    Width = 12,
                    Height = 12,
                    Stroke = MaxButton.Foreground,
                    StrokeThickness = 1,
                    Data = Geometry.Parse("M1,4 L8,4 L8,11 L1,11 Z M4,1 L11,1 L11,8")
                };
            }
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                ClientAreaGrid.Margin = new Thickness(0);
                this.windowChrome.GlassFrameThickness = new Thickness(1);
                this.ResizeCrimp.Visibility = System.Windows.Visibility.Visible;
                MaxButton.Content = new Rectangle
                {
                    Width = 11,
                    Height = 11,
                    Stroke = MaxButton.Foreground,
                    StrokeThickness = 1
                };
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private void ResizeCrimp_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {            
            const uint WM_SYSCOMMAND = 274;
            const uint DIRECTION_BOTTOMRIGHT = 61448;
            SendMessage(this.mainWindow.Handle, WM_SYSCOMMAND, (IntPtr)DIRECTION_BOTTOMRIGHT, IntPtr.Zero);
        }

        private class DefferedUnRasterize
        {
            internal DefferedUnRasterize(TextEditor textEditor)
            {
                this.textEditor = textEditor;
            }

            public void UnRasterize(object sender, EventArgs e)
            {
                textEditor.UnRasterize();
            }

            private readonly TextEditor textEditor;
        }

        private void CommandSave(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0 && this.tab[this.currentTabIndex].IsDiff)
            {
                this.SetStatusText("DIFFS ARE READ-ONLY", MainWindow.StatusType.STATUS_OTHER);
                return;
            }
            if (this.currentTabIndex >= 0)
            {
                this.tab[this.currentTabIndex].CheckEncoding();

                bool fileSaved = false;
                if (this.tab[this.currentTabIndex].FullFileName != null)
                {
                    fileSaved = this.tab[this.currentTabIndex].SaveFile(this.tab[this.currentTabIndex].FullFileName);
                }
                else
                {
                    SaveFileDialog dlg = new SaveFileDialog();
                    dlg.Filter = FilterString;  
                    if (this.currentTabIndex >= 0 && this.tab[this.currentTabIndex].FullFileName != null)
                    {
                        string initialDirectory = System.IO.Path.GetDirectoryName(this.tab[this.currentTabIndex].FullFileName);
                        if (initialDirectory != null && initialDirectory.Length != 0)
                        {
                            dlg.InitialDirectory = initialDirectory;
                        }
                    }

                    if (dlg.ShowDialog(this) ?? false)
                    {
                        fileSaved = this.tab[this.currentTabIndex].SaveFile(dlg.FileName);
                    }
                }
                if (fileSaved)
                {
                    System.Windows.Media.Animation.Storyboard fileSaveAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("fileSave");
                    this.tab[this.currentTabIndex].TextEditor.Rasterize();
                    DefferedUnRasterize unRasterizer = new DefferedUnRasterize(this.tab[this.currentTabIndex].TextEditor);
                    fileSaveAnimation.Completed += unRasterizer.UnRasterize;
                    fileSaveAnimation.SpeedRatio = 5;
                    fileSaveAnimation.Begin();

                    this.SetStatusText("FILE SAVED", MainWindow.StatusType.STATUS_OTHER);
                    System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                    dispatcherTimer.Tick += new EventHandler(ClearStatusDispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimer.Start();
                }                
            }
        }
                
        private void ClearStatusDispatcherTimer_Tick(object sender, EventArgs e)
        {
            ((DispatcherTimer)sender).Stop();
            this.SetStatusText("", StatusType.STATUS_CLEAR);
        }

        internal static string FilterString = "All files (*.*)|*.*|" +
            "C# (*.cs)|*.cs|" +
            "C/C++ |*.h;*.hxx;*.hpp;*.c;*.cxx;*.cpp|" +
            "HTML|*.htm;*.html|" +
            "JavaScript|*.js|" +
            "PHP|*.php|" +
            "Python (*.py)|*.py|" +
            "Ruby (*.rb)|*.rb|" +
            "Stylesheet|*.css|" +
            "Text files (*.txt)|*.txt|" +
            "XML|*.xml";

        private void CommandOpen(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.Filter = FilterString;            
            if (this.currentTabIndex >= 0 && this.tab[this.currentTabIndex].FullFileName != null)
            {
                string initialDirectory = System.IO.Path.GetDirectoryName(this.tab[this.currentTabIndex].FullFileName);
                if (initialDirectory != null && initialDirectory.Length != 0)
                {
                    dlg.InitialDirectory = initialDirectory;
                }
            }

            if (dlg.ShowDialog(this) ?? false)
            {
                CommandOpenFile(dlg.FileName);
            }
        }

        private void CommandOpenFile(string path)
        {
            string normalizedPath;
            try { normalizedPath = System.IO.Path.GetFullPath(path); }
            catch (ArgumentException) { return; }
            for (int i = 0; i < this.tab.Count; i++)
            {
                if (this.tab[i].FullFileName != null && string.Equals(this.tab[i].FullFileName, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    this.currentTabIndex = i;
                    this.tab[i].Title.Opacity = 1;
                    this.tab[i].Content.Visibility = Visibility.Visible;
                    this.tab[i].TextEditor.Focus();
                    return;
                }
            }

            bool reuseCurrent = this.currentTabIndex >= 0
                && this.tab[this.currentTabIndex].FullFileName == null
                && this.tab[this.currentTabIndex].TextEditor.Document.IsEmpty
                && !this.tab[this.currentTabIndex].TextEditor.Document.HasUnsavedContent;
            if (!reuseCurrent)
            {
                if (this.currentTabIndex >= 0)
                {
                    this.tab[this.currentTabIndex].Title.Opacity = 0.72;
                    this.tab[this.currentTabIndex].Content.Visibility = Visibility.Hidden;
                }
                this.AddNewTab();
                this.currentTabIndex = tab.Count - 1;
                SetFocusAfterTextEditorInitialization();
            }

            Tab openedTab = this.tab[this.currentTabIndex];
            if (!openedTab.OpenFile(normalizedPath) && openedTab.TextEditor.Document.IsEmpty)
            {
                this.TabClose(this.currentTabIndex);
                return;
            }
            CaptureSessionBaseline(openedTab);
            _ = ApplyDiffModeToTabAsync(openedTab, GetSelectedDiffMode());
        }

        private void CommandNew(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                tab[this.currentTabIndex].Title.Opacity = 0.72;
                tab[this.currentTabIndex].Content.Visibility = Visibility.Hidden;
            }

            this.AddNewTab();
            
            this.currentTabIndex = tab.Count - 1;
            SetFocusAfterTextEditorInitialization();
        }

        private void CommandRefresh(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0 
                && tab[this.currentTabIndex].FullFileName != null 
                && System.IO.File.Exists(tab[this.currentTabIndex].FullFileName) 
                && !tab[currentTabIndex].TextEditor.Document.HasUnsavedContent)
            {
                tab[this.currentTabIndex].OpenFile(tab[this.currentTabIndex].FullFileName);
                SetStatusText("FILE REFRESHED", StatusType.STATUS_CLEAR);
                System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(ClearStatusDispatcherTimer_Tick);
                dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                dispatcherTimer.Start();
            }            
        }

        private void CommandReplace(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.findAndReplaceWindow == null)
            {
                this.findAndReplaceWindow = new FindAndReplace(this);
                this.findAndReplaceWindow.Owner = this;
            }
            if (this.findAndReplaceWindow.IsVisible)
            {
                this.findAndReplaceWindow.Hide();
                this.Editor.Focus();
            }
            else
            {                
                this.findAndReplaceWindow.Show();
                this.findAndReplaceWindow.Focus();
            }
        }

        private void CommandGoto(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                tab[this.currentTabIndex].TextEditor.SetFocus();
            }            
            GotoLine.ShowGotoLineWindow(this);
        }

        public void CommandGoto(int lineNumber)
        {
            if (this.currentTabIndex >= 0)
            {
                try
                {
                    tab[this.currentTabIndex].TextEditor.GoToLine(lineNumber);
                }
                catch
                {
                }
            }
        }

        private void CommandHelp(object sender, ExecutedRoutedEventArgs e)
        {
            if (Settings.Visibility == System.Windows.Visibility.Hidden)
            {
                Logo_MouseDown(null, null);
            }
            else
            {
                BackImage_MouseDown(null, null);
            }
        }
        
        private void CommandResetZoom(object sender, ExecutedRoutedEventArgs e)
        {
            TextCoreControl.Settings.ResetFontSize();
            if (this.currentTabIndex >= 0)
            {
                TextCoreControl.TextEditor textEditor = tab[this.currentTabIndex].TextEditor;
                textEditor.NotifyOfSettingsChange();
            }            
        }
        #endregion

        #region Drag Drop

        private void MoveTab(int sourceTabIndex, int insertAfterTabIndex)
        {
            if (insertAfterTabIndex != sourceTabIndex - 1)
            {
                // Need to move a tab from currentTabIndex to after insertAfterTabIndex;
                if (insertAfterTabIndex < sourceTabIndex)
                {
                    insertAfterTabIndex++;
                }

                // Switch focus to non existant tab.
                this.SwitchTabFocusTo(-1);
                Tab tabBeingMoved = tab[sourceTabIndex];
                tab.RemoveAt(sourceTabIndex);
                TabBar.Children.RemoveAt(sourceTabIndex);
                Editor.Children.RemoveAt(sourceTabIndex);
                tab.Insert(insertAfterTabIndex, tabBeingMoved);
                TabBar.Children.Insert(insertAfterTabIndex, tabBeingMoved.Title);
                Editor.Children.Insert(insertAfterTabIndex, tabBeingMoved.Content);
                this.SwitchTabFocusTo(insertAfterTabIndex);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (dragDropSource != null)
            {
                // This is a tab rearrange operation.
                for (int i = 0; i < tab.Count; i++)
                {
                    if (tab[i].Title == dragDropSource)
                    {
                        // Found the tab.                                
                        int currentTabIndex = i;
                        int insertAfterTabIndex = FindTabDropPosition(e.GetPosition(WindowDrag).X);

                        MoveTab(currentTabIndex, insertAfterTabIndex);
                    }
                }
                this.dropWasConsumedAsTabMove = true;
            }
            else if (e.Data is System.Windows.DataObject)
            {
                DataObject dataObject = (System.Windows.DataObject)e.Data;
                string[] dataFormats = dataObject.GetFormats();
                System.Collections.Specialized.StringCollection filePaths = null;
                if (dataObject.ContainsFileDropList())
                {
                    filePaths = ((System.Windows.DataObject)e.Data).GetFileDropList();
                    foreach (string filePath in filePaths)
                    {
                        this.AddTabWithFile(filePath);
                    }
                }
                else if (dataObject.GetDataPresent(BEND_FILE_DISPLAY_NAME) &&
                    dataObject.GetDataPresent(BEND_FILE_PATH) &&
                    dataObject.GetDataPresent(BEND_FILE_CONTENT) &&
                    dataObject.GetDataPresent(BEND_FILE_DELETE))
                {
                    // Another bend is trying to send us a tab.
                    int insertAfterTabIndex = FindTabDropPosition(e.GetPosition(WindowDrag).X);
                    string contentPath = dataObject.GetData(BEND_FILE_CONTENT) as string;
                    string originalPath = dataObject.GetData(BEND_FILE_PATH) as string;
                    string displayName = dataObject.GetData(BEND_FILE_DISPLAY_NAME) as string;
                    string deletePath = dataObject.GetData(BEND_FILE_DELETE) as string;
                    if (!this.AddTabWithFile(contentPath))
                        return;
                    int tabIndex = this.tab.Count - 1;
                    if (!String.IsNullOrEmpty(originalPath))
                    {
                        this.tab[tabIndex].SetFullFileName(originalPath);
                    }
                    if (!String.IsNullOrEmpty(displayName))
                        this.tab[tabIndex].Title.TitleText = displayName;
                    TryDeleteTransferFile(deletePath);

                    if (!String.Equals(contentPath, originalPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Document has some kind of change.
                        this.tab[tabIndex].TextEditor.Document.HasUnsavedContent = true;
                    }

                    MoveTab(tabIndex, insertAfterTabIndex);
                }
                this.dropWasConsumedAsTabMove = false;
            }
        }

        private int FindTabDropPosition(double mouseX)
        {
            double totalWidth = TabBar.Margin.Left;
            int insertAfterTabIndex = -1;
            for (int i = 0; i < this.tab.Count; i++)
            {
                double titleWidth = this.tab[i].Title.ActualWidth;
                totalWidth += titleWidth;
                if (totalWidth <= mouseX)
                {
                    insertAfterTabIndex = i;
                }
                else
                {
                    break;
                }
            }
            return insertAfterTabIndex;
        }

        #region Windows API
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }
        #endregion

        private const string BEND_FILE_CONTENT = "BEND_FILE_CONTENT";
        private const string BEND_FILE_DISPLAY_NAME = "BEND_FILE_DISPLAY_NAME";
        private const string BEND_FILE_PATH = "BEND_FILE_PATH";
        private const string BEND_FILE_DELETE = "BEND_FILE_DELETE";
        private const string BEND_SERIALIZED_TABDATA_PREFIX = "/TABDATA";

        void TabTitle_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.dragDropSource == null && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                // Find the Tab
                int tabIndex = -1;
                for (int i = 0; i < tab.Count; i++)
                {
                    if (tab[i].Title == sender)
                    {                        
                        tabIndex = i;
                    }
                }

                // TabTitle was found in collection.
                if (tabIndex >= 0)
                {
                    if (tabIndex > 0)
                        this.SwitchTabFocusTo(tabIndex - 1);
                    else if (tabIndex + 1 < tab.Count)
                        this.SwitchTabFocusTo(tabIndex + 1);

                    string originalFullFileName = tab[tabIndex].FullFileName;
                    string contentFullFileName = originalFullFileName;
                    string deleteFile = System.IO.Path.GetTempFileName();
                    if (contentFullFileName == null || tab[tabIndex].TextEditor.Document.HasUnsavedContent)
                    {
                        contentFullFileName = deleteFile;
                        tab[tabIndex].CheckEncoding();
                        tab[tabIndex].TextEditor.SaveFile(contentFullFileName);
                    }
                    else
                    {
                        // Create an empty file.
                        System.IO.File.WriteAllLines(deleteFile, new string[0]);
                    }

                    Tab sourceTab = tab[tabIndex];
                    sourceTab.Content.Visibility = Visibility.Collapsed;
                    sourceTab.Title.Visibility = Visibility.Collapsed;

                    // Package the data.
                    DataObject data = new DataObject();

                    if (Keyboard.IsKeyDown(Key.LeftAlt))
                    {
                        // Copy the file to any other application.
                        System.Collections.Specialized.StringCollection fileList = new System.Collections.Specialized.StringCollection();
                        fileList.Add(contentFullFileName);
                        data.SetFileDropList(fileList);

                        // Initiate the drag-and-drop operation.    
                        DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
                    }
                    else
                    {
                        // Move the tab to another bend.
                        data.SetData(BEND_FILE_CONTENT, contentFullFileName);
                        data.SetData(BEND_FILE_DISPLAY_NAME, sourceTab.Title.TitleText);
                        data.SetData(BEND_FILE_PATH, originalFullFileName == null ? String.Empty : originalFullFileName);
                        data.SetData(BEND_FILE_DELETE, deleteFile);

                        this.tabDragVisual = new TabDragVisual(sourceTab.TextEditor, sourceTab.Title);
                        this.dragDropSource = sender as TabTitle;
                        this.dropWasConsumedAsTabMove = false;
                        this.tabDragVisual.UpdatePosition(this);
                        this.tabDragVisual.Show();
                        this.tabDragVisual.DragMove();
                        double tabDragVisualTop = this.tabDragVisual.Top;
                        double tabDragVisualLeft = this.tabDragVisual.Left;
                        double tabDragVisualWidth = this.tabDragVisual.ActualWidth;
                        double tabDragVisualHeight = this.tabDragVisual.ActualHeight;
                        this.tabDragVisual.Close();
                        this.tabDragVisual = null;

                        // Check if this is a aero snap
                        Point point = GetMousePosition();
                        double mouseX = point.X;
                        if (Math.Abs(mouseX - System.Windows.SystemParameters.WorkArea.Right) < 4||
                            Math.Abs(mouseX - System.Windows.SystemParameters.VirtualScreenWidth) < 4 ||
                            Math.Abs(mouseX - System.Windows.SystemParameters.WorkArea.Left) < 4 ||
                            mouseX < 4)
                        {
                            // Snap to right
                            tabDragVisualWidth = System.Windows.SystemParameters.WorkArea.Width / 2;
                            tabDragVisualHeight = System.Windows.SystemParameters.WorkArea.Height;
                        }                        

                        // Initiate the drag-and-drop operation.  
                        extendDragDrop = true;
                        DragDrop.DoDragDrop(this, data, DragDropEffects.All);
                        extendDragDrop = false;

                        if (!this.dropWasConsumedAsTabMove)
                        {
                            // The tab that was dragged needs to be closed.
                            if (System.IO.File.Exists(deleteFile))
                            {
                                // The tab was not taken by another bend. Start a new instance of bend and pass the tab to it.
                                string[] serializedData = new string [8];
                                serializedData[0] = contentFullFileName;
                                serializedData[1] = (string)data.GetData(BEND_FILE_DISPLAY_NAME);
                                serializedData[2] = (string)data.GetData(BEND_FILE_PATH);
                                serializedData[3] = (string)data.GetData(BEND_FILE_DELETE);
                                serializedData[4] = tabDragVisualLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                serializedData[5] = tabDragVisualTop.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                serializedData[6] = tabDragVisualWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                serializedData[7] = tabDragVisualHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                string serializedDataString = string.Join("\n", serializedData);
                                string arguments = BEND_SERIALIZED_TABDATA_PREFIX + System.Uri.EscapeDataString(serializedDataString);

                                App.LaunchBendClickOnceApplication(arguments);
                            }
                            this.TabClose(tabIndex);
                        }

                        this.dragDropSource = null;

                    }

                    TryDeleteTransferFile(deleteFile);
                    sourceTab.Content.Visibility = Visibility.Visible;
                    sourceTab.Title.Visibility = Visibility.Visible;
                }
            }
        }

        bool LoadSerializedTabData(string serializedTabData)
        {
            if (String.IsNullOrEmpty(serializedTabData) || !serializedTabData.StartsWith(BEND_SERIALIZED_TABDATA_PREFIX, StringComparison.Ordinal))
                return false;
            string[] serializedData;
            try { serializedData = System.Uri.UnescapeDataString(serializedTabData.Substring(BEND_SERIALIZED_TABDATA_PREFIX.Length)).Split('\n'); }
            catch (UriFormatException) { return false; }
            if (serializedData.Length != 8 || !System.IO.File.Exists(serializedData[0]))
                return false;

            double left, top, width, height;
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse(serializedData[4], System.Globalization.NumberStyles.Float, culture, out left) ||
                !double.TryParse(serializedData[5], System.Globalization.NumberStyles.Float, culture, out top) ||
                !double.TryParse(serializedData[6], System.Globalization.NumberStyles.Float, culture, out width) ||
                !double.TryParse(serializedData[7], System.Globalization.NumberStyles.Float, culture, out height) ||
                double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height) || width < 200 || height < 150)
                return false;

            this.Top = top;
            this.Left = left;
            this.Width = width;
            this.Height = height;

            // Another bend is trying to send us a tab.
            if (!this.AddTabWithFile(serializedData[0]))
                return false;
            int tabIndex = this.tab.Count - 1;
            if (!String.IsNullOrEmpty(serializedData[2]))
                this.tab[tabIndex].SetFullFileName(serializedData[2]);
            if (!String.IsNullOrEmpty(serializedData[1]))
                this.tab[tabIndex].Title.TitleText = serializedData[1];
            TryDeleteTransferFile(serializedData[3]);

            if (serializedData[0] != serializedData[2])
            {
                // Document has some kind of change.
                this.tab[tabIndex].TextEditor.Document.HasUnsavedContent = true;
            }
            return true;
        }

        private static void TryDeleteTransferFile(string filePath)
        {
            if (String.IsNullOrWhiteSpace(filePath))
                return;
            string fullPath;
            try { fullPath = System.IO.Path.GetFullPath(filePath); }
            catch { return; }
            string tempPath = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (fullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath))
            {
                try { System.IO.File.Delete(fullPath); }
                catch (System.IO.IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        void TabDrag_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (extendDragDrop)
            {
                e.Action = DragAction.Continue;
                e.Handled = true;
                extendDragDrop = false;
            }
        }
        #endregion

        #region Menu band management
        private void NewButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.CommandNew(sender, null);
            }
            if (e != null) e.Handled = true;
        }

        private void OpenButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.CommandOpen(sender, null);
            }
            e.Handled = true;
        }

        private void SaveButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.CommandSave(sender, null);
            }
            e.Handled = true;
        }

        private void SavePlusButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (this.currentTabIndex >= 0)
                {
                    if (this.tab[this.currentTabIndex].IsDiff)
                    {
                        this.SetStatusText("DIFFS ARE READ-ONLY", MainWindow.StatusType.STATUS_OTHER);
                        e.Handled = true;
                        return;
                    }
                    this.tab[this.currentTabIndex].CheckEncoding();

                    SaveFileDialog dlg = new SaveFileDialog();
                    if (this.tab[this.currentTabIndex].FullFileName != null)
                    {
                        string initialDirectory = System.IO.Path.GetDirectoryName(this.tab[this.currentTabIndex].FullFileName);
                        if (initialDirectory != null && initialDirectory.Length != 0)
                        {
                            dlg.InitialDirectory = initialDirectory;
                        }
                    }

                    dlg.Filter = FilterString;   
                    if (dlg.ShowDialog(this) ?? false)
                    {
                        this.tab[this.currentTabIndex].SaveFile(dlg.FileName);
                    }                    
                }
            }
            e.Handled = true;
        }

        private void FindButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.CommandFind(sender, null);
            }
            e.Handled = true;
        }

        private void Logo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ShowSettings();
            return;
#pragma warning disable 162
            try
            {
                if (!this.isInSettingsAnimation)
                {
                    this.isInSettingsAnimation = true;
                    System.Windows.Media.Animation.Storyboard settingsAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("slideSettingsIn");
                    MainWindowGridRotateTransform.CenterX = this.Width / 3;
                    MainWindowGridRotateTransform.CenterY = this.Height;
                    SettingsGridRotateTransform.CenterX = this.Width / 1.5;
                    SettingsGridRotateTransform.CenterY = this.Height;

                    if (MainDockBottomPanel.Visibility == System.Windows.Visibility.Visible)
                    {
                        ToggleBottomPanel_MouseDown(sender, e);
                    }
                    if (this.currentTabIndex >= 0 && this.currentTabIndex < this.tab.Count)
                        this.tab[this.currentTabIndex].TextEditor.Rasterize();

                    if (PersistantStorage.StorageObject.SettingsPageAnimation)
                    {
                        settingsAnimation.SpeedRatio = 1;
                        settingsAnimation.Begin(this);
                    }
                    else
                    {
                        Settings.Visibility = System.Windows.Visibility.Visible;
                        MainWindowGridRotateTransform.Angle = 180;
                        SettingsGridRotateTransform.Angle = 0;
                        settingsAnimation.SpeedRatio = 1000;
                        settingsAnimation.Begin(this);
                    }
                }
            }
            catch
            {
            }
#pragma warning restore 162
        }
        
        private void BackImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Settings.Visibility = Visibility.Hidden;
            Editor.Visibility = Visibility.Visible;
            BottomChrome.Visibility = Visibility.Visible;
            MainWindowGridRotateTransform.Angle = 0;
            SettingsGridRotateTransform.Angle = 0;
            isInSettingsAnimation = false;
            return;
#pragma warning disable 162
            try
            {
                if (!this.isInSettingsAnimation)
                {
                    this.isInSettingsAnimation = true;
                    System.Windows.Media.Animation.Storyboard settingsAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("slideSettingsOut");
                    MainWindowGridRotateTransform.CenterX = this.Width / 3;
                    MainWindowGridRotateTransform.CenterY = this.Height;
                    SettingsGridRotateTransform.CenterX = this.Width / 1.5;
                    SettingsGridRotateTransform.CenterY = this.Height;

                    if (PersistantStorage.StorageObject.SettingsPageAnimation)
                    {
                        // Rerasterize to get the new size
                        if (this.currentTabIndex >= 0 && this.currentTabIndex < this.tab.Count)
                            this.tab[this.currentTabIndex].TextEditor.Rasterize();

                        settingsAnimation.SpeedRatio = 1;
                        settingsAnimation.Begin(this);
                    }
                    else
                    {
                        MainWindowGridRotateTransform.Angle = 0;
                        SettingsGridRotateTransform.Angle = -180;
                        Settings.Visibility = System.Windows.Visibility.Hidden;
                        settingsAnimation.SpeedRatio = 1000;
                        settingsAnimation.Begin(this);
                    }
                }                
            }
            catch
            {
            }
#pragma warning restore 162
        }
        
        private void ToggleBottomPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MainDockSplitter.Visibility != System.Windows.Visibility.Visible)
            {
                MainDockSplitter.Visibility = System.Windows.Visibility.Visible;
                BottomPaneResizeThumb.Visibility = Visibility.Visible;
                BottomChrome.RowDefinitions[1].Height = new GridLength(4);
                double terminalHeight = PersistantStorage.StorageObject.BottomTerminalHeight;
                BottomChrome.RowDefinitions[2].Height = new GridLength(terminalHeight >= 80 ? terminalHeight : 300);
                TerminalToggleChevron.Data = Geometry.Parse("M0,1 L4,5 L8,1");
                ToggleBottomPanel.Foreground = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.LogoBackgroundColor);
                MainDockBottomPanel.Background = (SolidColorBrush)Application.Current.Resources["TerminalColorBrush"];
                var theme = new TerminalTheme
                {
                    DefaultBackground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorBackground,
                    DefaultForeground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorForeground,
                    DefaultSelectionBackground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorSelectionBackground,
                    CursorStyle = CursorStyle.BlinkingBar,
                    ColorTable = PersistantStorage.StorageObject.CurrentTheme.TerminalColors,
                };
                Terminal.Theme = theme;
                MainDockBottomPanel.Visibility = System.Windows.Visibility.Visible;
                Terminal.StartupCommandLine = terminalStartupCommand;
                Terminal.WorkingDirectory = currentFolderPath;
                Terminal.StartTerminal();
                EnsureTerminalTab(Terminal);
                SelectTerminal(0);
                Terminal.Terminal.Focus();
            }
            else
            {
                MainDockSplitter.Visibility = System.Windows.Visibility.Collapsed;
                BottomPaneResizeThumb.Visibility = Visibility.Collapsed;
                MainDockBottomPanel.Visibility = System.Windows.Visibility.Collapsed;
                BottomChrome.RowDefinitions[1].Height = new GridLength(0);
                BottomChrome.RowDefinitions[2].Height = new GridLength(0);
                TerminalToggleChevron.Data = Geometry.Parse("M0,5 L4,1 L8,5");
                ToggleBottomPanel.Foreground = MaxButton.Foreground;
            }
            if (e != null) e.Handled = true;
        }

        private void BottomPaneResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            const double minimumPaneHeight = 80;
            const double minimumEditorHeight = 80;
            double currentHeight = BottomChrome.RowDefinitions[2].ActualHeight;
            double maximumHeight = currentHeight + Math.Max(0, MainWindowGrid.RowDefinitions[1].ActualHeight - minimumEditorHeight);
            double newHeight = Math.Max(minimumPaneHeight, Math.Min(maximumHeight, currentHeight - e.VerticalChange));
            BottomChrome.RowDefinitions[2].Height = new GridLength(newHeight);
        }

        private void BottomPaneResizeThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (BottomChrome.RowDefinitions[2].ActualHeight > 0)
                PersistantStorage.StorageObject.BottomTerminalHeight = BottomChrome.RowDefinitions[2].ActualHeight;
            SavePaneLayout();
        }

        private void SidePaneSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (SidePaneColumn.ActualWidth > 0)
                PersistantStorage.StorageObject.LeftPaneWidth = SidePaneColumn.ActualWidth;
            SavePaneLayout();
        }

        private void AgentPaneSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (AgentPaneColumn.ActualWidth > 0)
                PersistantStorage.StorageObject.AgentPaneWidth = AgentPaneColumn.ActualWidth;
            SavePaneLayout();
        }

        private static void SavePaneLayout()
        {
            try { PersistantStorage.Save(); }
            catch (Exception exception) { System.Diagnostics.Debug.WriteLine("Could not persist pane layout: " + exception); }
        }

        void slideSettingsInAnimation_Completed(object sender, EventArgs e)
        {
            this.SettingsControl.UpdateFocus();
            this.isInSettingsAnimation = false;
        }

        void slideSettingsOutAnimation_Completed(object sender, EventArgs e)
        {            
            if (this.currentTabIndex >= 0 && this.currentTabIndex < this.tab.Count)
            {
                this.tab[this.currentTabIndex].TextEditor.UnRasterize();
            }
            this.isInSettingsAnimation = false;
        }

        public void CancelSettingsUI()
        {
            this.BackImage_MouseDown(null, null);
        }

        private void ReplaceButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.CommandReplace(sender, null);
        }
        #endregion

        #region Tab bar management
        private void AddNewTab()
        {
            Tab newTab = new Tab();
            tab.Add(newTab);
            // Hook up tab band event handlers
            newTab.Title.MouseLeftButtonUp += this.TabClick;
            newTab.Title.ContextMenu = (ContextMenu)Resources["TabTitleContextMenu"];
            newTab.Title.CloseButtonClicked += this.TabClose;
            newTab.TextEditor.DisplayManager.CaretPositionChanged += TextEditor_CaretPositionChanged;
            newTab.TextEditor.Document.LanguageChanged += Document_LanguageChanged;
            newTab.Title.MouseMove += TabTitle_MouseMove;            

            TabBar.Children.Add(newTab.Title);
            Editor.Children.Add(newTab.Content);
            UpdateEditorChrome();
            newTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);
            newTab.TextEditor.DisplayManager.SelectionChange += DisplayManager_SelectionChange;

            StatusBar.Visibility = PersistantStorage.StorageObject.ShowStatusBar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        }
                
        void DisplayManager_ContextMenu()
        {
            if( Editor.ContextMenu != null )
            {
                Editor.ContextMenu.PlacementTarget = Editor;
                Editor.ContextMenu.IsOpen = true;                
            }
        }

        private void TabClick(object sender, MouseButtonEventArgs e)
        {
            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == sender)
                {
                    this.SwitchTabFocusTo(i);
                    break;
                }
            }
            // Tab was not found - fail silently
        }

        private void TabClose(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement closeElement = sender as FrameworkElement;
            TabTitle tabTitle = closeElement?.Parent as TabTitle;
            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == tabTitle)
                {
                    this.TabClose(i);
                }
            }
            // Tab was not found - fail silently
        }       

        private void ContextCloseOtherTabs(object sender, RoutedEventArgs e)
        {
            UIElement tabTitle = (Control)((MenuItem)e.OriginalSource).Parent;
            tabTitle = ((System.Windows.Controls.Primitives.Popup)((Control)tabTitle).Parent).PlacementTarget;

            // Close all the other tabs
            for (int i = tab.Count - 1; i >= 0; i--)
            {
                if (tab[i].Title != tabTitle)
                {
                    // Delete the tab                
                    TabBar.Children.Remove(tab[i].Title);
                    Editor.Children.Remove(tab[i].Content);
                    tab.RemoveAt(i);                 
                }
            }
            
            // Now set focus on the first tab.
            if (tab.Count > 0)
            {
                this.currentTabIndex = 0;
                tab[this.currentTabIndex].Title.Opacity = 1.0;
                tab[this.currentTabIndex].Content.Visibility = Visibility.Visible;
                tab[this.currentTabIndex].Content.Focus();
            }
        }

        private void ContextCopyFullPath(object sender, RoutedEventArgs e)
        {
            UIElement tabTitle = (Control)((MenuItem)e.OriginalSource).Parent;
            tabTitle = ((System.Windows.Controls.Primitives.Popup)((Control)tabTitle).Parent).PlacementTarget;

            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == tabTitle)
                {
                    string fullFileName = tab[i].FullFileName;
                    if (fullFileName != null)
                    {
                        Clipboard.SetText(fullFileName);
                    }
                    break;
                }
            }
        }

        private void ContextFileEncoding(object sender, RoutedEventArgs e)
        {
            UIElement tabTitle = (Control)((MenuItem)e.OriginalSource).Parent;
            tabTitle = ((System.Windows.Controls.Primitives.Popup)((Control)tabTitle).Parent).PlacementTarget;

            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == tabTitle)
                {
                    FileEncodingMessageBox.Show(tab[i].TextEditor, /*warningMode*/false);
                }
            }
        }

        private void ContextGoToLine(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                tab[this.currentTabIndex].TextEditor.SetFocus();
            }
            GotoLine.ShowGotoLineWindow(this);
        }

        private void ContextOpenContainingFolder(object sender, RoutedEventArgs e)
        {
            UIElement tabTitle = (Control)((MenuItem)e.OriginalSource).Parent;
            tabTitle = ((System.Windows.Controls.Primitives.Popup)((Control)tabTitle).Parent).PlacementTarget;
            
            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == tabTitle)
                {
                    if (tab[i].FullFileName != null && tab[i].FullFileName.Length > 0)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", System.IO.Path.GetDirectoryName(tab[i].FullFileName));
                    }
                    break;
                }
            }
        }

        private void ContextRecord(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                tab[this.currentTabIndex].TextEditor.StartFlightRecord();
            }
        }

        private void ContextClose(object sender, RoutedEventArgs e)
        {
            UIElement tabTitle = (Control)((MenuItem)e.OriginalSource).Parent;
            tabTitle = ((System.Windows.Controls.Primitives.Popup)((Control)tabTitle).Parent).PlacementTarget;
            
            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == tabTitle)
                {
                    this.TabClose(i);
                    break;
                }
            }
            // Tab was not found - fail silently
        }

        private void SwitchTabFocusTo(int tabIndex)
        {
            // Set Focus to tab.
            if (currentTabIndex >= 0)
            {
                tab[currentTabIndex].Content.Visibility = Visibility.Hidden;
                tab[currentTabIndex].Title.Opacity = 0.72;
            }
            
            if (tabIndex >= 0)
            {
                this.currentTabIndex = tabIndex;
                tab[tabIndex].Title.Opacity = 1.0;
                tab[tabIndex].Content.Visibility = Visibility.Visible;
                tab[tabIndex].Content.Focus();
                StatusBar.Visibility = PersistantStorage.StorageObject.ShowStatusBar ? Visibility.Visible : Visibility.Hidden;
                if (!tab[tabIndex].IsDiff && String.Equals(StatusText.Content as string, LineEndingsOnlyStatus, StringComparison.Ordinal))
                    SetStatusText("", StatusType.STATUS_CLEAR);
                if (!tab[tabIndex].IsDiff) this.FindText.Text = tab[tabIndex].FindOptions.FindText;
                _ = ApplyDiffModeToTabAsync(tab[tabIndex], GetSelectedDiffMode());
                UpdateDocumentTypeStatus();
            }
        }

        private void TabClose(int tabIndex)
        {
            if (this.tab[tabIndex].TextEditor.Document.HasUnsavedContent)
            {
                this.SwitchTabFocusTo(tabIndex);

                SaveChangesMessageBox.ButtonClicked buttonClicked = SaveChangesMessageBox.Show(tab[tabIndex].FullFileName);
                if (buttonClicked == SaveChangesMessageBox.ButtonClicked.Cancel)
                {
                    return;
                }
                if (buttonClicked == SaveChangesMessageBox.ButtonClicked.Save)
                {
                    this.CommandSave(null, null);
                    if (this.tab[tabIndex].TextEditor.Document.HasUnsavedContent)
                    {
                        return;
                    }
                }                
            }

            if (tabIndex == this.currentTabIndex)
            {
                // Switch to an existing tab
                // We know i < tab.Count - check if we are the last tab before switching to a tab after us.
                // if we are the last tab switch to a tab before us.
                if (tabIndex == (tab.Count - 1))
                {
                    this.currentTabIndex = tabIndex - 1;
                    if (this.currentTabIndex >= 0)
                    {
                        tab[this.currentTabIndex].Title.Opacity = 1.0;
                        tab[this.currentTabIndex].Content.Visibility = Visibility.Visible;
                        tab[this.currentTabIndex].Content.Focus();
                    }
                }
                else
                {
                    // After deletion all indexes after i shift.
                    this.currentTabIndex = tabIndex;

                    tab[this.currentTabIndex + 1].Title.Opacity = 1.0;
                    tab[this.currentTabIndex + 1].Content.Visibility = Visibility.Visible;
                    tab[this.currentTabIndex + 1].Content.Focus();
                }
            }
            else
            {
                // The indexes shifted, since a tab was deleted.
                if (this.currentTabIndex > tabIndex)
                {
                    this.currentTabIndex--;
                }
            }      

            // Delete current tab                    
            TabBar.Children.Remove(tab[tabIndex].Title);
            Editor.Children.Remove(tab[tabIndex].Content);
            tab[tabIndex].Close();
            tab.RemoveAt(tabIndex);
            UpdateEditorChrome();

            if (tab.Count == 0)
                StatusBar.Visibility = System.Windows.Visibility.Hidden;
        }

        private void ContextRefresh(object sender, RoutedEventArgs e)
        {
            UIElement tabTitle = (Control)((MenuItem)e.OriginalSource).Parent;
            tabTitle = ((System.Windows.Controls.Primitives.Popup)((Control)tabTitle).Parent).PlacementTarget;

            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == tabTitle)
                {
                    if (tab[i].FullFileName != null && System.IO.File.Exists(tab[i].FullFileName))
                    {
                        tab[i].OpenFile(tab[i].FullFileName);
                    }
                    break;
                }
            }
        }

        internal enum StatusType
        {
            STATUS_FINDONPAGE,
            STATUS_CLEAR,
            STATUS_OTHER
        };
        
        internal void SetStatusText(string statusText, StatusType statusType)        
        {
            if (statusText.Length == 0)
            {
                this.StatusText.Visibility = System.Windows.Visibility.Hidden;
                this.StatusText.Content = "";
            }
            else
            {
                this.StatusText.Visibility = System.Windows.Visibility.Visible;
                this.StatusText.Content = statusText;
            }
            this.currentStatusType = statusType;
        }
        #endregion

        #region Editor Context Menu
        private void ContextCopy(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                this.tab[this.currentTabIndex].TextEditor.CopySelection();
            }
        }

        private void ContextCut(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                Tab.CopyPasteManager.Cut(this.tab[this.currentTabIndex].TextEditor);
            }
        }

        private void ContextUndo(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                this.tab[this.currentTabIndex].TextEditor.Undo();
            }
        }

        private void ContextRedo(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                this.tab[this.currentTabIndex].TextEditor.Redo();
            }
        }

        private void ContextPaste(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                Tab.CopyPasteManager.Paste(this.tab[this.currentTabIndex].TextEditor);
            }
        }
        #endregion

        #region Find On page

        private void CommandFind(object sender, ExecutedRoutedEventArgs e)
        {
            FindText.Focus();
            FindText.SelectAll();
        }       

        private void FindText_KeyDown(object sender, KeyEventArgs e)
        {
            if (this.CurrentTab != null)
            {
                if (e.Key == Key.Enter)
                {
                    if (this.CurrentTab.FindOptions.FindText == this.FindText.Text)
                    {
                        if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                        {
                            this.SetStatusText(this.CurrentTab.HighlightPreviousMatch(), StatusType.STATUS_FINDONPAGE);
                        }
                        else
                        {
                            this.SetStatusText(this.CurrentTab.HighlightNextMatch(), StatusType.STATUS_FINDONPAGE);
                        }
                    }
                    else
                    {
                        FindOptions findOptions = new FindOptions(FindText.Text);
                        this.CurrentTab.StartFindOnPage(this, findOptions);
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    this.CurrentTab.ClearFindOnPage();
                    this.SetStatusText("", StatusType.STATUS_CLEAR);
                }
            }
        }

        private void FindText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchHint != null) SearchHint.Visibility = String.IsNullOrEmpty(FindText.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (this.CurrentTab != null)
            {
                FindOptions findOptions = new FindOptions(this.FindText.Text);
                this.CurrentTab.StartFindOnPage(this, findOptions);
            }
        }
        
        void DisplayManager_SelectionChange()
        {
            if (this.currentStatusType == StatusType.STATUS_FINDONPAGE)
                this.SetStatusText("", StatusType.STATUS_CLEAR);
        }
        #endregion

        #region Status Bar

        private string activeActivity;

        private void InitializeDiffModeControl()
        {
            initializingDiffMode = true;
            DiffViewMode mode = ParseDiffMode(PersistantStorage.StorageObject.DiffViewMode);
            if (String.IsNullOrWhiteSpace(PersistantStorage.StorageObject.LastDiffViewMode))
                PersistantStorage.StorageObject.LastDiffViewMode =
                    (mode == DiffViewMode.SideBySide ? DiffViewMode.SideBySide : DiffViewMode.Inline).ToString();
            DiffNoneButton.IsChecked = mode == DiffViewMode.None;
            DiffInlineButton.IsChecked = mode == DiffViewMode.Inline;
            DiffSideBySideButton.IsChecked = mode == DiffViewMode.SideBySide;
            initializingDiffMode = false;
        }

        private static DiffViewMode ParseDiffMode(string value)
        {
            DiffViewMode mode;
            return Enum.TryParse(value, true, out mode) ? mode : DiffViewMode.Inline;
        }

        private static DiffViewMode ParseLastDiffMode(string value)
        {
            DiffViewMode mode = ParseDiffMode(value);
            return mode == DiffViewMode.SideBySide ? DiffViewMode.SideBySide : DiffViewMode.Inline;
        }

        private DiffViewMode GetSelectedDiffMode()
        {
            if (DiffSideBySideButton.IsChecked == true) return DiffViewMode.SideBySide;
            if (DiffNoneButton.IsChecked == true) return DiffViewMode.None;
            return DiffViewMode.Inline;
        }

        private async void DiffMode_Checked(object sender, RoutedEventArgs e)
        {
            if (initializingDiffMode || tab == null) return;
            DiffViewMode mode = GetSelectedDiffMode();
            PersistantStorage.StorageObject.DiffViewMode = mode.ToString();
            if (mode != DiffViewMode.None)
                PersistantStorage.StorageObject.LastDiffViewMode = mode.ToString();
            try { PersistantStorage.Save(); } catch { }

            // Existing comparison tabs can switch immediately. A normal file gets
            // its HEAD version lazily without replacing its editable document.
            foreach (Tab openTab in tab.Where(t => t.TextEditor.HasDiffBase))
                openTab.TextEditor.DiffMode = mode;
            if (CurrentTab != null && !CurrentTab.TextEditor.HasDiffBase)
                await ApplyDiffModeToTabAsync(CurrentTab, mode);
        }

        private async System.Threading.Tasks.Task ApplyDiffModeToTabAsync(Tab target, DiffViewMode mode)
        {
            if (target == null) return;
            if (mode == DiffViewMode.None)
            {
                target.TextEditor.DiffMode = DiffViewMode.None;
                return;
            }
            if (target.TextEditor.HasDiffBase)
            {
                target.TextEditor.DiffMode = mode;
                return;
            }

            if (diffBaseCancellation != null) diffBaseCancellation.Cancel();
            diffBaseCancellation = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = diffBaseCancellation.Token;
            try
            {
                string baseText = String.Empty;
                if (!String.IsNullOrWhiteSpace(target.FullFileName))
                {
                    string workspace = !String.IsNullOrWhiteSpace(currentFolderPath)
                        ? currentFolderPath : System.IO.Path.GetDirectoryName(target.FullFileName);
                    baseText = await diffGitService.GetWorkingFileBaseAsync(workspace, target.FullFileName, token);
                }
                if (token.IsCancellationRequested || !tab.Contains(target)) return;
                target.TextEditor.SetDiffBase(baseText, target.FullFileName);
                target.TextEditor.DiffMode = mode;
            }
            catch (OperationCanceledException) { }
            catch
            {
                if (!token.IsCancellationRequested && tab.Contains(target))
                {
                    string sessionBase;
                    if (TryGetSessionBaseline(target, out sessionBase))
                    {
                        target.TextEditor.SetDiffBase(sessionBase, target.FullFileName);
                        target.TextEditor.DiffMode = mode;
                    }
                    else
                    {
                        target.TextEditor.ClearDiffBase();
                    }
                }
            }
        }

        private void CaptureSessionBaseline(Tab target)
        {
            if (target == null || String.IsNullOrWhiteSpace(target.FullFileName)) return;
            string path;
            try { path = System.IO.Path.GetFullPath(target.FullFileName); }
            catch (Exception) { return; }
            if (sessionFileBaselines.ContainsKey(path)) return;

            string text = target.TextEditor.Document.Text ?? String.Empty;
            if (text.Length > 0 && text[text.Length - 1] == '\0')
                text = text.Substring(0, text.Length - 1);
            sessionFileBaselines[path] = text;
        }

        private bool TryGetSessionBaseline(Tab target, out string baseline)
        {
            baseline = null;
            if (target == null || String.IsNullOrWhiteSpace(target.FullFileName)) return false;
            try
            {
                return sessionFileBaselines.TryGetValue(
                    System.IO.Path.GetFullPath(target.FullFileName), out baseline);
            }
            catch (Exception) { return false; }
        }

        private void UpdateEditorChrome()
        {
            bool hasTabs = tab != null && tab.Count > 0;
            TabStrip.Visibility = hasTabs ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleActivityPane(string activity, string title)
        {
            if (activeActivity == activity && SidePaneColumn.Width.Value > 0)
            {
                SidePaneColumn.Width = new GridLength(0);
                activeActivity = null;
                return;
            }
            activeActivity = activity;
            SidePaneTitle.Text = title;
            double savedPaneWidth = PersistantStorage.StorageObject.LeftPaneWidth;
            SidePaneColumn.Width = new GridLength(savedPaneWidth >= 140 ? savedPaneWidth : (activity == "source" ? 320 : 240));
            bool showFiles = activity == "files";
            bool showSearch = activity == "search";
            bool showSource = activity == "source";
            FilesPanel.Visibility = showFiles ? Visibility.Visible : Visibility.Collapsed;
            SearchPanel.Visibility = showSearch ? Visibility.Visible : Visibility.Collapsed;
            SourceControlPanel.Visibility = showSource ? Visibility.Visible : Visibility.Collapsed;
            OtherSidePaneContent.Visibility = (showFiles || showSearch || showSource) ? Visibility.Collapsed : Visibility.Visible;
            if (showSource) SourceControlPanel.RefreshAsync();
        }

        private void FilesActivity_Click(object sender, RoutedEventArgs e) { ToggleActivityPane("files", "FILES"); }
        private void SearchActivity_Click(object sender, RoutedEventArgs e) { ToggleActivityPane("search", "SEARCH"); }
        private void SourceControlActivity_Click(object sender, RoutedEventArgs e) { ToggleActivityPane("source", "SOURCE CONTROL"); }

        private void AgentButton_Click(object sender, RoutedEventArgs e)
        {
            if (agentTerminalSessions.Count == 0)
                OpenAgentTerminal(GetDefaultAgentCommand());
            else
                SetAgentPaneExpanded(!agentPaneExpanded);
            e.Handled = true;
        }

        private string GetDefaultAgentCommand()
        {
            string command = PersistantStorage.StorageObject.DefaultAgentCli;
            return String.IsNullOrWhiteSpace(command) ? "copilot" : command.Trim();
        }

        private void SetAgentPaneExpanded(bool expanded)
        {
            if (expanded)
            {
                if (!agentPaneExpanded)
                {
                    double width = PersistantStorage.StorageObject.AgentPaneWidth;
                    AgentPaneColumn.MinWidth = 320;
                    AgentSplitterColumn.Width = new GridLength(1);
                    AgentPaneColumn.Width = new GridLength(width >= 320 ? width : 360);
                    AgentPaneSplitter.Visibility = Visibility.Visible;
                    AgentPane.Visibility = Visibility.Visible;
                    AgentButton.Foreground = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.LogoBackgroundColor);
                }
                agentPaneExpanded = true;
                FocusAgentTerminalAfterLayout();
            }
            else
            {
                if (AgentPaneColumn.ActualWidth > 0) PersistantStorage.StorageObject.AgentPaneWidth = AgentPaneColumn.ActualWidth;
                AgentPane.Visibility = Visibility.Collapsed;
                AgentPaneSplitter.Visibility = Visibility.Collapsed;
                AgentPaneColumn.MinWidth = 0;
                AgentSplitterColumn.Width = new GridLength(0);
                AgentPaneColumn.Width = new GridLength(0);
                AgentButton.Foreground = MaxButton.Foreground;
                agentPaneExpanded = false;
                SavePaneLayout();
            }
        }

        private void OpenAgentTerminal(string command)
        {
            if (String.IsNullOrWhiteSpace(command)) return;
            string startupCommand = GetAgentStartupCommand(command.Trim());
            Console.TerminalControl terminal = new Console.TerminalControl
            {
                StartupCommandLine = startupCommand,
                WorkingDirectory = currentFolderPath,
                Win32InputMode = true,
                InputCapture = Console.TerminalControl.INPUT_CAPTURE.TabKey | Console.TerminalControl.INPUT_CAPTURE.DirectionKeys,
                Margin = new Thickness(5)
            };
            terminal.Visibility = Visibility.Collapsed;
            terminal.TermExited += AgentTerminal_Exited;
            AgentTerminalHost.Children.Add(terminal);
            agentTerminalSessions.Add(terminal);
            ApplyTerminalTheme(terminal);

            TabTitle terminalTab = new TabTitle(true) { Tag = terminal, Width = 155 };
            string executableName = System.IO.Path.GetFileNameWithoutExtension(command.Trim().Split(' ')[0]);
            terminalTab.TitleText = String.Equals(executableName, "copilot", StringComparison.OrdinalIgnoreCase) ? "Copilot" :
                String.Equals(executableName, "opencode", StringComparison.OrdinalIgnoreCase) ? "OpenCode" :
                String.Equals(executableName, "claude", StringComparison.OrdinalIgnoreCase) ? "Claude" : executableName;
            terminalTab.CloseButton.Tag = terminal;
            terminalTab.CloseButtonClicked += AgentTerminalTabClose_Click;
            terminalTab.MouseLeftButtonUp += AgentTerminalTab_Click;
            terminal.Tag = terminalTab;
            AgentTabBar.Children.Add(terminalTab);
            SetAgentPaneExpanded(true);
            SelectAgentTerminal(agentTerminalSessions.Count - 1);
            terminal.StartTerminal();
            FocusAgentTerminalAfterLayout();
            ResizeAgentTerminalAfterLayout(terminal);
        }

        private void ResizeAgentTerminalAfterLayout(Console.TerminalControl terminal)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!agentTerminalSessions.Contains(terminal)) return;
                terminal.UpdateLayout();
                terminal.ResizeToCurrentDimensions();
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private static string GetAgentStartupCommand(string command)
        {
            if (String.Equals(command, "opencode", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(command, "claude", StringComparison.OrdinalIgnoreCase))
                return "cmd.exe /d /s /c \"" + command + "\"";
            return command;
        }

        private void AgentTerminal_Exited(object sender, EventArgs e)
        {
            Console.TerminalControl terminal = sender as Console.TerminalControl;
            if (terminal == null) return;
            Dispatcher.BeginInvoke(new Action(() => CloseAgentTerminal(terminal)));
        }

        private void FocusAgentTerminalAfterLayout()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!agentPaneExpanded || currentAgentTerminalIndex < 0 || currentAgentTerminalIndex >= agentTerminalSessions.Count)
                    return;
                Console.TerminalControl terminal = agentTerminalSessions[currentAgentTerminalIndex];
                terminal.Focus();
                terminal.Terminal.Focus();
                Keyboard.Focus(terminal.Terminal);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void SelectAgentTerminal(int index)
        {
            if (index < 0 || index >= agentTerminalSessions.Count) return;
            currentAgentTerminalIndex = index;
            for (int i = 0; i < agentTerminalSessions.Count; i++)
            {
                agentTerminalSessions[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
                TabTitle tabTitle = agentTerminalSessions[i].Tag as TabTitle;
                if (tabTitle != null) tabTitle.Opacity = i == index ? 1 : .65;
            }
            agentTerminalSessions[index].Terminal.Focus();
            ResizeAgentTerminalAfterLayout(agentTerminalSessions[index]);
        }

        private void AgentTerminalTab_Click(object sender, MouseButtonEventArgs e)
        {
            TabTitle title = (TabTitle)sender;
            SelectAgentTerminal(agentTerminalSessions.IndexOf((Console.TerminalControl)title.Tag));
        }

        private void AgentTerminalTabClose_Click(object sender, MouseButtonEventArgs e)
        {
            CloseAgentTerminal((Console.TerminalControl)((FrameworkElement)sender).Tag);
            e.Handled = true;
        }

        private void CloseAgentTerminal(Console.TerminalControl terminal)
        {
            int index = agentTerminalSessions.IndexOf(terminal);
            if (index < 0) return;
            TabTitle title = terminal.Tag as TabTitle;
            terminal.TermExited -= AgentTerminal_Exited;
            if (title != null) AgentTabBar.Children.Remove(title);
            AgentTerminalHost.Children.Remove(terminal);
            Console.TermPTYProxy connection = terminal.DisconnectConPTYTerm();
            if (connection != null) connection.Dispose();
            agentTerminalSessions.RemoveAt(index);
            if (agentTerminalSessions.Count == 0)
            {
                currentAgentTerminalIndex = -1;
                SetAgentPaneExpanded(false);
            }
            else SelectAgentTerminal(Math.Min(index, agentTerminalSessions.Count - 1));
        }

        private void AgentClosePane_Click(object sender, RoutedEventArgs e)
        {
            SetAgentPaneExpanded(false);
            e.Handled = true;
        }

        private void AgentNewTerminalMenu_Click(object sender, RoutedEventArgs e)
        {
            AgentCliMenu.Items.Clear();
            List<string> commands = new List<string> { "copilot", "opencode", "claude" };
            string configured = PersistantStorage.StorageObject.AdditionalAgentClis;
            if (!String.IsNullOrWhiteSpace(configured))
                commands.AddRange(configured.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()));
            foreach (string command in commands.Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                MenuItem item = new MenuItem { Header = command, Tag = command };
                item.Click += AgentCliMenuItem_Click;
                AgentCliMenu.Items.Add(item);
            }
            ((Button)sender).ContextMenu.IsOpen = true;
        }

        private void AgentCliMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenAgentTerminal((string)((MenuItem)sender).Tag);
        }

        private void SourceControlPanel_DiffRequested(object sender, DiffRequestedEventArgs e)
        {
            if (GetSelectedDiffMode() == DiffViewMode.None)
            {
                DiffViewMode lastMode = ParseLastDiffMode(PersistantStorage.StorageObject.LastDiffViewMode);
                if (lastMode == DiffViewMode.SideBySide)
                    DiffSideBySideButton.IsChecked = true;
                else
                    DiffInlineButton.IsChecked = true;
            }
            e.Mode = GetSelectedDiffMode();
            Tab existing = tab.FirstOrDefault(t => t.IsDiff && String.Equals(t.DiffKey, e.Key, StringComparison.Ordinal));
            Tab priorPreview = this.treePreviewTab;
            bool targetWasDurable = existing != null && existing != priorPreview;
            CloseTreePreviewIfAllowed(existing);
            DiffModel model = null;
            bool onlyLineEndings;
            if (e.CurrentText != null || e.BaseText != null)
                onlyLineEndings = IsOnlyLineEndingsChanged(e.BaseText, e.CurrentText);
            else
            {
                model = DiffModel.Parse(e.Patch, e.Title);
                onlyLineEndings = IsOnlyLineEndingsChanged(model);
            }
            if (existing != null)
            {
                if (e.CurrentText != null || e.BaseText != null)
                    existing.TextEditor.LoadText(e.CurrentText ?? String.Empty, e.FileName, e.BaseText ?? String.Empty);
                else
                {
                    string refreshedName = model.Files.Count == 0 ? e.Title : (model.Files[0].NewPath ?? model.Files[0].OldPath);
                    existing.TextEditor.LoadText(model.BuildNewText(), refreshedName, model.BuildOldText());
                }
                existing.TextEditor.DiffMode = e.Mode;
                SwitchTabFocusTo(tab.IndexOf(existing));
                UpdateLineEndingsOnlyStatus(onlyLineEndings);
                this.treePreviewTab = e.IsPinned || targetWasDurable ? null : existing;
                return;
            }
            Tab diffTab = new Tab();
            if (e.CurrentText != null || e.BaseText != null)
                diffTab.ConfigureDiff(e.Key, e.Title, e.FileName, e.CurrentText, e.BaseText, e.Mode);
            else
                diffTab.ConfigureDiff(e.Key, e.Title, model, e.Mode);
            diffTab.Title.MouseLeftButtonUp += this.TabClick;
            diffTab.Title.ContextMenu = (ContextMenu)Resources["TabTitleContextMenu"];
            diffTab.Title.CloseButtonClicked += this.TabClose;
            diffTab.Title.MouseMove += TabTitle_MouseMove;
            diffTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);
            diffTab.TextEditor.DisplayManager.SelectionChange += DisplayManager_SelectionChange;
            diffTab.TextEditor.DisplayManager.CaretPositionChanged += TextEditor_CaretPositionChanged;
            diffTab.TextEditor.Document.LanguageChanged += Document_LanguageChanged;
            diffTab.Content.Visibility = Visibility.Hidden;
            tab.Add(diffTab); TabBar.Children.Add(diffTab.Title); Editor.Children.Add(diffTab.Content);
            UpdateEditorChrome(); SwitchTabFocusTo(tab.Count - 1);
            UpdateLineEndingsOnlyStatus(onlyLineEndings);
            this.treePreviewTab = e.IsPinned ? null : diffTab;
        }

        private static bool IsOnlyLineEndingsChanged(DiffModel model)
        {
            return model != null && model.Lines.Any(line => line.Kind == DiffLineKind.Modified ||
                line.Kind == DiffLineKind.Added || line.Kind == DiffLineKind.Removed) &&
                String.Equals(model.BuildOldText(), model.BuildNewText(), StringComparison.Ordinal);
        }

        private static bool IsOnlyLineEndingsChanged(string oldText, string newText)
        {
            oldText = oldText ?? String.Empty;
            newText = newText ?? String.Empty;
            return !String.Equals(oldText, newText, StringComparison.Ordinal) &&
                String.Equals(oldText.Replace("\r\n", "\n").Replace('\r', '\n'),
                    newText.Replace("\r\n", "\n").Replace('\r', '\n'), StringComparison.Ordinal);
        }

        private void UpdateLineEndingsOnlyStatus(bool onlyLineEndings)
        {
            if (onlyLineEndings)
                SetStatusText(LineEndingsOnlyStatus, StatusType.STATUS_OTHER);
            else if (String.Equals(StatusText.Content as string, LineEndingsOnlyStatus, StringComparison.Ordinal))
                SetStatusText("", StatusType.STATUS_CLEAR);
        }

        private void SourceControlPanel_DiffModeChanged(object sender, EventArgs e)
        {
            foreach (Tab openTab in tab.Where(t => t.IsDiff)) openTab.TextEditor.DiffMode = GetSelectedDiffMode();
        }

        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings();
            e.Handled = true;
        }

        private void SettingsActivity_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings();
            e.Handled = true;
        }

        private void ShowSettings()
        {
            if (Settings.Visibility == Visibility.Visible)
                return;

            if (agentPaneExpanded)
                SetAgentPaneExpanded(false);

            // Do not use ToggleBottomPanel_MouseDown here. During startup the panel and
            // splitter can briefly have different visibility values, which makes that
            // method take the open branch and prevents navigation to Settings.
            MainDockSplitter.Visibility = Visibility.Collapsed;
            BottomPaneResizeThumb.Visibility = Visibility.Collapsed;
            MainDockBottomPanel.Visibility = Visibility.Collapsed;
            BottomChrome.RowDefinitions[1].Height = new GridLength(0);
            BottomChrome.RowDefinitions[2].Height = new GridLength(0);
            TerminalToggleChevron.Data = Geometry.Parse("M0,5 L4,1 L8,5");
            ToggleBottomPanel.Foreground = MaxButton.Foreground;

            MainWindowGridRotateTransform.Angle = 0;
            SettingsGridRotateTransform.Angle = 0;
            Editor.Visibility = Visibility.Hidden;
            BottomChrome.Visibility = Visibility.Hidden;
            Settings.Visibility = Visibility.Visible;
            SettingsControl.UpdateFocus();
            isInSettingsAnimation = false;
        }

        private void BendMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            if (menu != null) menu.IsSubmenuOpen = true;
            e.Handled = true;
        }

        private void SettingsBack_Click(object sender, RoutedEventArgs e)
        {
            SettingsControl.ApplyOptions();
            BackImage_MouseDown(null, null);
        }
        private void NewShell_Click(object sender, RoutedEventArgs e) { CommandNew(sender, null); }
        private void TerminalToggle_Click(object sender, RoutedEventArgs e) { ToggleBottomPanel_MouseDown(sender, null); }
        private void TerminalClose_Click(object sender, RoutedEventArgs e)
        {
            if (MainDockBottomPanel.Visibility != Visibility.Visible || currentTerminalIndex < 0) return;
            CloseTerminal(terminalSessions[currentTerminalIndex]);
        }
        private void CloseTerminal(Console.TerminalControl terminal)
        {
            int terminalIndex = terminalSessions.IndexOf(terminal);
            if (terminalIndex < 0) return;
            TabTitle terminalTab = terminal.Tag as TabTitle;
            if (terminalTab != null) TerminalTabBar.Children.Remove(terminalTab);
            MainDockBottomPanel.Children.Remove(terminal);
            terminalSessions.RemoveAt(terminalIndex);
            if (terminalSessions.Count == 0)
            {
                // Terminal is declared inside the XAML host Border and remains its
                // logical child even when its tab is closed. Re-register the session;
                // adding it to MainDockBottomPanel would give it a second parent.
                terminalSessions.Add(Terminal);
                currentTerminalIndex = -1;
                ToggleBottomPanel_MouseDown(null, null);
            }
            else
            {
                SelectTerminal(Math.Min(terminalIndex, terminalSessions.Count - 1));
            }
            UpdateTerminalTabScrollIndicators();
        }
        private void NewTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (MainDockBottomPanel.Visibility != Visibility.Visible)
            {
                ToggleBottomPanel_MouseDown(sender, null);
                return;
            }

            Console.TerminalControl terminal = new Console.TerminalControl
            {
                StartupCommandLine = terminalStartupCommand,
                WorkingDirectory = currentFolderPath,
                Margin = new Thickness(8)
            };
            MainDockBottomPanel.Children.Add(terminal);
            terminalSessions.Add(terminal);
            ApplyTerminalTheme(terminal);
            terminal.StartTerminal();
            EnsureTerminalTab(terminal);
            SelectTerminal(terminalSessions.Count - 1);
        }
        private void EnsureTerminalTab(Console.TerminalControl terminal)
        {
            int terminalNumber = terminalSessions.IndexOf(terminal) + 1;
            foreach (TabTitle existingTab in TerminalTabBar.Children)
                if (ReferenceEquals(existingTab.Tag, terminal)) return;
            TabTitle terminalTab = new TabTitle(true)
            {
                Tag = terminal,
                Width = 155
            };
            terminalTab.TitleText = terminalNumber == 1 ? "Terminal" : "Terminal " + terminalNumber;
            terminalTab.CloseButton.Tag = terminal;
            terminalTab.CloseButtonClicked += TerminalTabClose_Click;
            terminalTab.MouseLeftButtonUp += TerminalTab_Click;
            TerminalTabBar.Children.Add(terminalTab);
            terminal.Tag = terminalTab;
            UpdateTerminalTabScrollIndicators();
        }
        private void TerminalTab_Click(object sender, MouseButtonEventArgs e)
        {
            TabTitle terminalTab = (TabTitle)sender;
            if (MainDockBottomPanel.Visibility != Visibility.Visible)
                ToggleBottomPanel_MouseDown(null, null);
            SelectTerminal(terminalSessions.IndexOf((Console.TerminalControl)terminalTab.Tag));
        }
        private void SelectTerminal(int index)
        {
            if (index < 0 || index >= terminalSessions.Count) return;
            for (int i = 0; i < terminalSessions.Count; i++)
                terminalSessions[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            currentTerminalIndex = index;
            for (int i = 0; i < terminalSessions.Count; i++)
            {
                TabTitle terminalTab = terminalSessions[i].Tag as TabTitle;
                if (terminalTab != null) terminalTab.Opacity = i == index ? 1 : 0.65;
            }
            terminalSessions[index].Terminal.Focus();
        }
        private void TerminalTabClose_Click(object sender, MouseButtonEventArgs e)
        {
            CloseTerminal((Console.TerminalControl)((FrameworkElement)sender).Tag);
            e.Handled = true;
        }
        private void TerminalTabScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateTerminalTabScrollIndicators();
        }
        private void UpdateTerminalTabScrollIndicators()
        {
            if (TerminalTabScrollViewer == null) return;
            bool canScrollLeft = TerminalTabScrollViewer.HorizontalOffset > 0;
            bool canScrollRight = TerminalTabScrollViewer.HorizontalOffset + TerminalTabScrollViewer.ViewportWidth < TerminalTabScrollViewer.ExtentWidth;
            TerminalTabScrollLeft.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
            TerminalTabScrollRight.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        }
        private void TerminalTabScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            TerminalTabScrollViewer.ScrollToHorizontalOffset(TerminalTabScrollViewer.HorizontalOffset - TerminalTabScrollViewer.ViewportWidth / 2);
        }
        private void TerminalTabScrollRight_Click(object sender, RoutedEventArgs e)
        {
            TerminalTabScrollViewer.ScrollToHorizontalOffset(TerminalTabScrollViewer.HorizontalOffset + TerminalTabScrollViewer.ViewportWidth / 2);
        }
        private void ApplyTerminalTheme(Console.TerminalControl terminal)
        {
            var theme = new TerminalTheme
            {
                DefaultBackground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorBackground,
                DefaultForeground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorForeground,
                DefaultSelectionBackground = PersistantStorage.StorageObject.CurrentTheme.TerminalColorSelectionBackground,
                CursorStyle = CursorStyle.BlinkingBar,
                ColorTable = PersistantStorage.StorageObject.CurrentTheme.TerminalColors,
            };
            terminal.Theme = theme;
        }
        private void ShellSelector_Click(object sender, RoutedEventArgs e)
        {
            OpenContextMenuBelow((Button)sender);
        }
        private bool IsPowerShellSelected { get { return ShellLabel.Text == "Pwsh"; } }
        private void SavedCommands_Click(object sender, RoutedEventArgs e)
        {
            OpenContextMenuBelow((Button)sender);
        }
        private static void OpenContextMenuBelow(Button button)
        {
            ContextMenu menu = button.ContextMenu;
            if (menu == null) return;

            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = 0;
            menu.IsOpen = true;
        }
        private void SavedCommandsMenu_Opened(object sender, RoutedEventArgs e)
        {
            RebuildSavedCommandsMenu();
        }
        private void RebuildSavedCommandsMenu()
        {
            if (SavedCommandsMenu == null) return;
            SavedCommandsMenu.Items.Clear();
            foreach (SavedTerminalCommand command in PersistantStorage.StorageObject.GetTerminalCommands(IsPowerShellSelected))
            {
                MenuItem item = new MenuItem { Header = command.Name, Tag = command };
                item.Click += SavedCommand_Click;
                SavedCommandsMenu.Items.Add(item);
            }
            if (SavedCommandsMenu.Items.Count > 0) SavedCommandsMenu.Items.Add(new Separator());
            MenuItem add = new MenuItem { Header = "+ Add Command" };
            add.Click += AddSavedCommand_Click;
            SavedCommandsMenu.Items.Add(add);
        }
        private void SavedCommand_Click(object sender, RoutedEventArgs e)
        {
            SavedTerminalCommand command = ((MenuItem)sender).Tag as SavedTerminalCommand;
            if (command == null || currentTerminalIndex < 0 || currentTerminalIndex >= terminalSessions.Count) return;
            Console.TerminalControl terminal = terminalSessions[currentTerminalIndex];
            if (terminal.ConPTYTerm != null) terminal.ConPTYTerm.WriteInput(command.CommandLine);
            terminal.Terminal.Focus();
        }
        private void AddSavedCommand_Click(object sender, RoutedEventArgs e)
        {
            bool powerShell = IsPowerShellSelected;
            ShowSettings();
            SettingsControl.SelectTerminalCommands(powerShell);
        }
        private void PowerShellShell_Click(object sender, RoutedEventArgs e)
        {
            ShellLabel.Text = "Pwsh";
            terminalStartupCommand = "pwsh.exe -NoLogo";
        }
        private void CommandPromptShell_Click(object sender, RoutedEventArgs e)
        {
            ShellLabel.Text = "Cmd";
            terminalStartupCommand = "c:\\windows\\system32\\cmd.exe";
        }
        private void MinimizeShell_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeShell_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void CloseShell_Click(object sender, RoutedEventArgs e) { Close(); }
        private void ExitMenu_Click(object sender, RoutedEventArgs e) { Close(); }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource != sender || (FindText != null && FindText.IsMouseOver)) return;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string selectedPath;
            if (ModernFolderPicker.TryShow(this, currentFolderPath ?? Environment.CurrentDirectory, out selectedPath))
                SetCurrentFolder(selectedPath);
            ToggleActivityPane("files", "FILES");
        }

        private string CurrentFolderPath { get { return this.currentFolderPath; } }

        private void SetCurrentFolder(string path, bool persist = true)
        {
            try { path = System.IO.Path.GetFullPath(path); }
            catch (ArgumentException) { return; }
            this.currentFolderPath = path;
            WorkspacePathText.Text = path;
            FilesPanel.RootPath = path;
            SearchPanel.RootPath = path;
            SourceControlPanel.WorkspacePath = path;
            if (persist)
            {
                PersistantStorage.StorageObject.LastWorkspaceFolder = path;
                try { PersistantStorage.Save(); }
                catch (Exception exception) { SetStatusText("WORKSPACE COULD NOT BE SAVED: " + exception.Message, StatusType.STATUS_OTHER); }
            }
        }

        private void FilesPanel_OpenFolderRequested(object sender, RoutedEventArgs e)
        {
            OpenFolder_Click(sender, e);
        }

        private void FilesPanel_FileInvoked(object sender, RoutedEventArgs e)
        {
            FolderTreeNode node = e.OriginalSource as FolderTreeNode;
            FolderTreeFileInvokedEventArgs fileEvent = e as FolderTreeFileInvokedEventArgs;
            if (node != null && node.NodeKind == FolderTreeNodeKind.File && fileEvent != null)
                OpenDocumentFromTree(node.FullPath, fileEvent.IsDoubleClick);
        }

        private void SearchPanel_ResultInvoked(object sender, RoutedEventArgs e)
        {
            SearchResultEventArgs resultEvent = e as SearchResultEventArgs;
            if (resultEvent == null || resultEvent.Result == null) return;
            OpenDocumentFromTree(resultEvent.Result.FullPath, true);
            if (this.CurrentTab != null)
            {
                Tab selectedTab = this.CurrentTab;
                SearchResult result = resultEvent.Result;
                Dispatcher.BeginInvoke(new Action(() => HighlightSearchResult(selectedTab, result, 0)),
                    DispatcherPriority.Render);
            }
        }

        private void HighlightSearchResult(Tab targetTab, SearchResult result, int attempt)
        {
            if (targetTab == null || result == null || String.IsNullOrEmpty(result.SearchText)) return;
            int matchIndex = FindMatchIndexOnLine(targetTab.TextEditor.Document.Text, result.Line, result.SearchText);
            if (matchIndex >= 0)
                targetTab.TextEditor.Select(matchIndex, (uint)result.SearchText.Length);
            else if (attempt < 4)
            {
                DispatcherTimer retryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                retryTimer.Tick += (sender, args) =>
                {
                    retryTimer.Stop();
                    HighlightSearchResult(targetTab, result, attempt + 1);
                };
                retryTimer.Start();
            }
            else
                targetTab.TextEditor.GoToLine(result.Line);
        }

        private static int FindMatchIndexOnLine(string text, int lineNumber, string searchText)
        {
            if (String.IsNullOrEmpty(text) || lineNumber < 1 || String.IsNullOrEmpty(searchText)) return -1;
            int lineStart = 0;
            for (int line = 1; line < lineNumber; line++)
            {
                int newline = text.IndexOf('\n', lineStart);
                if (newline < 0) return -1;
                lineStart = newline + 1;
            }
            int lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = text.Length;
            int matchIndex = text.IndexOf(searchText, lineStart, lineEnd - lineStart, StringComparison.OrdinalIgnoreCase);
            return matchIndex;
        }

        private void OpenDocumentFromTree(string path, bool isDoubleClick)
        {
            if (!System.IO.File.Exists(path))
            {
                SetStatusText("FILE NO LONGER EXISTS", StatusType.STATUS_OTHER);
                FilesPanel.RootPath = currentFolderPath;
                return;
            }
            string normalizedPath = System.IO.Path.GetFullPath(path);
            Tab targetTab = this.tab.FirstOrDefault(openTab => openTab.FullFileName != null
                && string.Equals(openTab.FullFileName, normalizedPath, StringComparison.OrdinalIgnoreCase));
            Tab priorPreviewTab = this.treePreviewTab;
            CloseTreePreviewIfAllowed(targetTab);
            CommandOpenFile(normalizedPath);
            if (this.currentTabIndex >= 0)
            {
                Tab openedTab = this.tab[this.currentTabIndex];
                bool targetWasDurable = targetTab != null && targetTab != priorPreviewTab;
                this.treePreviewTab = isDoubleClick || targetWasDurable ? null : openedTab;
            }
        }

        private void CloseTreePreviewIfAllowed(Tab targetTab)
        {
            if (this.treePreviewTab == null || this.treePreviewTab == targetTab)
                return;
            if (this.treePreviewTab.TextEditor.Document.HasUnsavedContent)
            {
                this.treePreviewTab = null;
                return;
            }
            int previewIndex = this.tab.IndexOf(this.treePreviewTab);
            if (previewIndex >= 0)
                this.TabClose(previewIndex);
            this.treePreviewTab = null;
        }

        private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (currentTabIndex < 0) return;
            SaveFileDialog dialog = new SaveFileDialog { Filter = FilterString };
            if (tab[currentTabIndex].FullFileName != null)
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(tab[currentTabIndex].FullFileName);
            if (dialog.ShowDialog(this) ?? false)
            {
                tab[currentTabIndex].SaveFile(dialog.FileName);
                WorkspacePathText.Text = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void SaveEncodingMenu_Click(object sender, RoutedEventArgs e)
        {
            if (currentTabIndex >= 0) FileEncodingMessageBox.Show(tab[currentTabIndex].TextEditor, false);
        }

        void TextEditor_CaretPositionChanged(int lineNumber, int columnNumber)
        {
            if (holdInitialReferenceStatus && lineNumber == 1 && columnNumber == 0) return;
            holdInitialReferenceStatus = false;
            Line.Content = lineNumber.ToString();
            Column.Content = columnNumber.ToString();
        }

        private void Document_LanguageChanged(object sender, EventArgs e)
        {
            if (CurrentTab != null && Object.ReferenceEquals(CurrentTab.TextEditor.Document, sender))
                UpdateDocumentTypeStatus();
        }

        private void UpdateDocumentTypeStatus()
        {
            if (CurrentTab == null)
            {
                DocumentTypeStatus.Text = "UTF-8    Plain Text";
                return;
            }

            Encoding encoding = CurrentTab.TextEditor.Document.CurrentEncoding;
            string encodingName = encoding == null ? "UTF-8" : encoding.WebName.ToUpperInvariant();
            DocumentTypeStatus.Text = encodingName + "    " + CurrentTab.TextEditor.Document.DetectedLanguage;
        }

        #endregion
    }
}
