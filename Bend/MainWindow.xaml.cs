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
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Windows.Shell;
using Microsoft.Win32;
using System.Collections;
using TextCoreControl;

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
        System.Threading.Thread findOnPageThread;

        List<Tab> tab;
        int currentTabIndex;

        struct FindResult {
            internal FindResult(int beginIndex, uint length)
            {
                this.beginIndex = beginIndex;
                this.length = length;
            }

            internal int beginIndex;
            internal uint length;
        };
        List<FindResult> findResults;
        int currentSearchIndex;

        BitmapImage maximizeImage;
        BitmapImage restoreImage;

        WindowChrome windowChrome;        

        bool isFullScreen;
        bool isInSettingsAnimation;

        System.Threading.SemaphoreSlim showFindOnPageResult;
        #endregion

        #region Public API

        public MainWindow()
        {
            InitializeComponent();
            findAndReplaceWindow = null;
            var style = (Style)Resources["PlainStyle"];
            this.Style = style;
            tab = new List<Tab>();
            this.Top = PersistantStorage.StorageObject.mainWindowTop;
            this.Left = PersistantStorage.StorageObject.mainWindowLeft;
            this.Width = PersistantStorage.StorageObject.mainWindowWidth;
            this.Height = PersistantStorage.StorageObject.mainWindowHeight;
            this.windowChrome = new WindowChrome();
            this.windowChrome.ResizeBorderThickness = new Thickness(4);
            this.windowChrome.CaptionHeight = 40;
            this.windowChrome.GlassFrameThickness = new Thickness(1);
            this.windowChrome.CornerRadius = new CornerRadius(0);
            this.windowChrome.RecivedFileNameEvent += new WindowChrome.RecivedFileNameEventHandler(windowChrome_RecivedFileNameEvent);
            WindowChrome.SetWindowChrome(this, this.windowChrome);
            this.isFullScreen = false;
            this.findResults = new List<FindResult>();
            this.showFindOnPageResult = new System.Threading.SemaphoreSlim(0, 1);
        }

        internal Tab GetActiveTab()
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

        internal void LoadOptions()
        {
            StatusBar.Visibility = PersistantStorage.StorageObject.ShowStatusBar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
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

            maximizeImage = new BitmapImage();
            maximizeImage.BeginInit();
            maximizeImage.UriSource = new Uri("pack://application:,,,/Bend;component/Images/max.png");
            maximizeImage.EndInit();
            restoreImage = new BitmapImage();
            restoreImage.BeginInit();
            restoreImage.UriSource = new Uri("pack://application:,,,/Bend;component/Images/restore.png");
            restoreImage.EndInit();

            // Reopen from explorer or last session or create empty tab
            bool tabOpened = false;
            try
            {
                string[] fileNames;
                if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null)
                    fileNames = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                else
                    fileNames = null;

                if (fileNames == null || fileNames.Length <= 0)
                {
                    fileNames = PersistantStorage.StorageObject.mruFile;
                }
                if (fileNames != null)
                {
                    for (int mruCount = 0; mruCount < fileNames.Length; mruCount++)
                    {
                        string fileName = fileNames[mruCount];
                        if (System.IO.File.Exists(fileName))
                        {
                            this.AddNewTab();
                            int lastTab = this.tab.Count - 1;
                            this.tab[lastTab].OpenFile(fileName);
                            this.tab[lastTab].Title.Opacity = 0.5;
                            this.tab[lastTab].TextEditor.Visibility = Visibility.Hidden;
                            tabOpened = true;
                        }
                    }
                }
            }
            catch
            {
            }
            if (!tabOpened)
            {
                // Create default new file tab
                this.AddNewTab();
            }
            
            // this.tab.Count will atleast be 1 at this point
            this.currentTabIndex = this.tab.Count - 1;
            this.tab[this.currentTabIndex].Title.Opacity = 1;
            this.tab[this.currentTabIndex].TextEditor.Visibility = Visibility.Visible;
            tab[this.currentTabIndex].TextEditor.SetFocus();

            System.Windows.Media.Animation.Storyboard settingsAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("slideSettingsOut");
            settingsAnimation.Completed += new EventHandler(slideSettingsOutAnimation_Completed);
            settingsAnimation = (System.Windows.Media.Animation.Storyboard)FindResource("slideSettingsIn");
            settingsAnimation.Completed += new EventHandler(slideSettingsInAnimation_Completed);
            isInSettingsAnimation = false;            
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
            }
            catch
            {
            }

        }

        void windowChrome_RecivedFileNameEvent(string fileName)
        {
            // If the settings page is the one in view, come out of it.
            if (Settings.Visibility != System.Windows.Visibility.Hidden)
            {
                BackImage_MouseDown(null, null);
            }
            this.AddTabWithFile(fileName);
        }
        
        private void AddTabWithFile(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                Tab newTab = new Tab();
                tab.Add(newTab);
                // Hook up tab band event handlers
                newTab.Title.MouseLeftButtonUp += this.TabClick;
                newTab.Title.ContextMenu = (ContextMenu)Resources["TabTitleContextMenu"];
                newTab.CloseButton.MouseLeftButtonUp += this.TabClose;
                // TODO: INTEGRATE:
                // newTab.TextEditor.TextArea.Caret.PositionChanged += new EventHandler(Caret_PositionChanged);

                newTab.Title.Opacity = 0.5;
                newTab.TextEditor.Visibility = Visibility.Hidden;

                TabBar.Children.Add(newTab.Title);
                Editor.Children.Add(newTab.TextEditor);
                newTab.TextEditor.Document.ContentChange += new Document.ContentChangeEventHandler(Document_ContentChange);
                newTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);

                newTab.OpenFile(filePath);

                // Switch focus to the new file
                if (currentTabIndex >= 0)
                {
                    tab[currentTabIndex].TextEditor.Visibility = Visibility.Hidden;
                    tab[currentTabIndex].Title.Opacity = 0.5;
                }

                int newTabFocus = tab.Count - 1;
                this.currentTabIndex = newTabFocus;
                tab[newTabFocus].Title.Opacity = 1.0;
                tab[newTabFocus].TextEditor.Visibility = Visibility.Visible;
                tab[newTabFocus].TextEditor.SetFocus();
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data is System.Windows.DataObject &&
                ((System.Windows.DataObject)e.Data).ContainsFileDropList())
            {
                System.Collections.Specialized.StringCollection filePaths = ((System.Windows.DataObject)e.Data).GetFileDropList();
                foreach (string filePath in filePaths)
                {
                    this.AddTabWithFile(filePath);
                }
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
            Application.Current.Shutdown();
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
                // Neon Carrot (Crayola) (Hex: #FF9933) (RGB: 255, 153, 51)
                FullscreenButton.Foreground = new SolidColorBrush(Color.FromRgb(255, 153, 51));
            }
        }

        private void ResetFullScreen()
        {
            WindowChrome.SetWindowChrome(this, windowChrome);
            this.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
            this.ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip;
            this.WindowState = System.Windows.WindowState.Normal;
            FullscreenButton.Foreground = Brushes.Gray;
            this.isFullScreen = false;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                this.MainWindowGrid.Margin = new Thickness(0, 0, 6, 6);
                this.ResizeCrimp.Visibility = System.Windows.Visibility.Hidden;
                MaxButton.Source = this.restoreImage;
            }
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                this.MainWindowGrid.Margin = new Thickness(0);
                this.ResizeCrimp.Visibility = System.Windows.Visibility.Visible;
                MaxButton.Source = this.maximizeImage;
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

        private void CommandSave(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                try
                {
                    bool fileSaved = false;
                    if (this.tab[this.currentTabIndex].FullFileName != null)
                    {
                        this.tab[this.currentTabIndex].SaveFile(this.tab[this.currentTabIndex].FullFileName);
                        fileSaved = true;
                    }
                    else
                    {
                        SaveFileDialog dlg = new SaveFileDialog();
                        FileExtensions flex = new FileExtensions();
                        dlg.Filter = flex.GetFilterString();  
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
                            this.tab[this.currentTabIndex].SaveFile(dlg.FileName);
                            fileSaved = true;
                        }
                    }
                    if (fileSaved)
                    {
                        this.SetStatusText("FILE SAVED");
                        System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                        dispatcherTimer.Tick += new EventHandler(SaveDispatcherTimer_Tick);
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                        dispatcherTimer.Start();
                    }
                }
                catch (Exception exception)
                {
                    StyledMessageBox.Show("ERROR", "Error Saving File" + exception.ToString());
                }
            }
        }

        private void SaveDispatcherTimer_Tick(object sender, EventArgs e)
        {
            ((DispatcherTimer)sender).Stop();
            this.SetStatusText("");
        }

        private void CommandOpen(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            FileExtensions flex = new FileExtensions();
            dlg.Filter = flex.GetFilterString();            
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
                // No tabs / Non Empty new file / tab has some file open
                if (this.currentTabIndex < 0 || !this.tab[this.currentTabIndex].TextEditor.Document.IsEmpty || this.tab[this.currentTabIndex].FullFileName != null)
                {
                    if (this.currentTabIndex >= 0)
                    {
                        tab[this.currentTabIndex].Title.Opacity = 0.5;
                        tab[this.currentTabIndex].TextEditor.Visibility = Visibility.Hidden;
                    }

                    this.AddNewTab();

                    this.currentTabIndex = tab.Count - 1;
                    tab[this.currentTabIndex].TextEditor.SetFocus();
                }

                this.tab[this.currentTabIndex].OpenFile(dlg.FileName);
            }
        }

        private void CommandNew(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                tab[this.currentTabIndex].Title.Opacity = 0.5;
                tab[this.currentTabIndex].TextEditor.Visibility = Visibility.Hidden;
            }

            this.AddNewTab();
            
            this.currentTabIndex = tab.Count - 1;
            tab[this.currentTabIndex].TextEditor.SetFocus();
        }

        private void CommandRefresh(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0 && tab[this.currentTabIndex].FullFileName != null && System.IO.File.Exists(tab[this.currentTabIndex].FullFileName))
            {
                tab[this.currentTabIndex].OpenFile(tab[this.currentTabIndex].FullFileName);
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
            if (this.currentTabIndex >= 0 && tab[this.currentTabIndex].FullFileName != null && System.IO.File.Exists(tab[this.currentTabIndex].FullFileName))
            {
                try
                {
                    TextCoreControl.TextEditor textEditor = tab[this.currentTabIndex].TextEditor;
                    textEditor.DisplayManager.ScrollToContentLineNumber(lineNumber, /*moveCaret*/ true);
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
                textEditor.RefreshDisplay();
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
            e.Handled = true;
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
                    try
                    {
                        SaveFileDialog dlg = new SaveFileDialog();
                        if (this.currentTabIndex >= 0 && this.tab[this.currentTabIndex].FullFileName != null)
                        {
                            string initialDirectory = System.IO.Path.GetDirectoryName(this.tab[this.currentTabIndex].FullFileName);
                            if (initialDirectory != null && initialDirectory.Length != 0)
                            {
                                dlg.InitialDirectory = initialDirectory;
                            }
                        }

                        FileExtensions flex = new FileExtensions();
                        dlg.Filter = flex.GetFilterString();   
                        if (dlg.ShowDialog(this) ?? false)
                        {
                            this.tab[this.currentTabIndex].SaveFile(dlg.FileName);                            
                        }
                    }
                    catch (Exception exception)
                    {
                        StyledMessageBox.Show("ERROR", "Error Saving File" + exception.ToString());
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
        }
        
        private void BackImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
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

        #region Tab band management
        private void AddNewTab()
        {
            Tab newTab = new Tab();
            tab.Add(newTab);
            // Hook up tab band event handlers
            newTab.Title.MouseLeftButtonUp += this.TabClick;
            newTab.Title.ContextMenu = (ContextMenu)Resources["TabTitleContextMenu"];
            newTab.CloseButton.MouseLeftButtonUp += this.TabClose;
            // TODO: INTEGRATE:
            //newTab.TextEditor.TextArea.Caret.PositionChanged += new EventHandler(Caret_PositionChanged);

            TabBar.Children.Add(newTab.Title);            
            Editor.Children.Add(newTab.TextEditor);
            newTab.TextEditor.Document.ContentChange += new Document.ContentChangeEventHandler(Document_ContentChange);
            newTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);
        }

        void DisplayManager_ContextMenu()
        {
            if( Editor.ContextMenu != null )
            {
                Editor.ContextMenu.PlacementTarget = Editor;
                Editor.ContextMenu.IsOpen = true;
            }    
        }

        void Document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            this.findResults.RemoveRange(0, this.findResults.Count);
        }

        private void TabClick(object sender, MouseButtonEventArgs e)
        {
            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == sender)
                {
                    if (currentTabIndex >= 0)
                    {
                        tab[currentTabIndex].TextEditor.Visibility = Visibility.Hidden;
                        tab[currentTabIndex].Title.Opacity = 0.5;
                    }
                    // Clear find on page state
                    this.currentSearchIndex = 0;

                    this.currentTabIndex = i;                    
                    tab[i].Title.Opacity = 1.0;
                    tab[i].TextEditor.Visibility = Visibility.Visible;
                    tab[i].TextEditor.SetFocus();
                    break;
                }
            }
            // Tab was not found - fail silently
        }

        private void TabClose(object sender, MouseButtonEventArgs e)
        {
            WrapPanel wrapPanel = (WrapPanel)((Image)sender).Parent;
            // Find the tab title in tab collection
            for (int i = 0; i < tab.Count; i++)
            {
                if (tab[i].Title == wrapPanel)
                {
                    this.TabClose(i);
                }
            }
            // Tab was not found - fail silently
        }       

        private void ContextCloseAllButThis(object sender, RoutedEventArgs e)
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
                    Editor.Children.Remove(tab[i].TextEditor);
                    tab.RemoveAt(i);                 
                }
            }
            
            // Now set focus on the first tab.
            if (tab.Count > 0)
            {
                this.currentTabIndex = 0;
                tab[this.currentTabIndex].Title.Opacity = 1.0;
                tab[this.currentTabIndex].TextEditor.Visibility = Visibility.Visible;
                tab[this.currentTabIndex].TextEditor.SetFocus();
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

        private void TabClose(int tabIndex)
        {
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
                        tab[this.currentTabIndex].TextEditor.Visibility = Visibility.Visible;
                        tab[this.currentTabIndex].TextEditor.SetFocus();
                    }
                }
                else
                {
                    // After deletion all indexes after i shift.
                    this.currentTabIndex = tabIndex;

                    tab[this.currentTabIndex + 1].Title.Opacity = 1.0;
                    tab[this.currentTabIndex + 1].TextEditor.Visibility = Visibility.Visible;
                    tab[this.currentTabIndex + 1].TextEditor.SetFocus();
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

            // Clear find on page
            this.currentSearchIndex = 0;            

            // Delete current tab                    
            TabBar.Children.Remove(tab[tabIndex].Title);
            Editor.Children.Remove(tab[tabIndex].TextEditor);
            tab[tabIndex].TextEditor.Document.ContentChange -= Document_ContentChange;
            tab[tabIndex].Close();
            tab.RemoveAt(tabIndex);
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
                        tab[i].TextEditor.LoadFile(tab[i].FullFileName);
                    }
                    break;
                }
            }
        }

        private delegate void SetStatusText_Delegate(string status);
        internal void SetStatusText(string statusText)        
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
        }
        #endregion

        #region Editor Context Menu
        private void ContextCopy(object sender, RoutedEventArgs e)
        {
            if (this.currentTabIndex >= 0)
            {
                Tab.CopyPasteManager.Copy(this.tab[this.currentTabIndex].TextEditor);
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
            if (this.currentTabIndex >= 0)
            {
                if (e.Key == Key.Enter)
                {
                    if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                    {
                        this.HighlightPreviousMatch();
                    }
                    else
                    {
                        this.HighlightNextMatch();
                    }

                    this.showFindOnPageResult.Wait(0);
                    this.showFindOnPageResult.Release();
                }
                else if (e.Key == Key.Escape)
                {
                    this.tab[this.currentTabIndex].TextEditor.CancelSelect();
                    this.tab[this.currentTabIndex].TextEditor.SetFocus();
                    this.currentSearchIndex = 0;
                    this.SetStatusText("");
                }
            }
        }

        private void FindText_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.currentSearchIndex = 0;
            this.findResults.RemoveRange(0, this.findResults.Count);
            this.FindOnPage(FindText.Text, /*matchCase*/false, /*useRegEx*/ false);            
        }

        private void FindText_LostFocus(object sender, RoutedEventArgs e)
        {
            this.SetStatusText("");
        }
        
        public void ReplaceSelectedText(string replacementText)
        {
            if (this.currentTabIndex >= 0 && this.currentTabIndex < this.tab.Count)
            {
                if (this.tab[this.currentTabIndex].TextEditor.SelectedText.Length > 0)
                {
                    this.tab[this.currentTabIndex].TextEditor.SelectedText = replacementText;
                }
            }
        }

        /// <summary>
        /// Replaces all occurances of the searchString with the replacementText in this document.
        /// </summary>
        public void ReplaceText(string searchString, string replacementText, bool matchCase, bool useRegex)
        {
            int count = this.tab[this.currentTabIndex].TextEditor.Document.ReplaceText(searchString, replacementText, matchCase, useRegex);
            if (count == 0)
            {
                this.SetStatusText("NO MATCHES FOUND");
            }
            else
            {
                this.tab[this.currentTabIndex].TextEditor.RefreshDisplay();
                this.SetStatusText(count + " MATCHES REPLACED");
            }
        }

        /// <summary>
        ///     Find searchstring and highlights the first instance. Also populates this.findResults.
        /// </summary>
        public void FindOnPage(string findText, bool matchCase, bool useRegex)
        {
            if (this.currentTabIndex >= 0)
            {
                string text = tab[this.currentTabIndex].TextEditor.Document.Text;
                TextEditor textEditor = tab[this.currentTabIndex].TextEditor;
                if (this.findOnPageThread != null)
                {
                    this.findOnPageThread.Abort();
                    this.findOnPageThread = null;
                }

                if (findText.Length > 0 && text.Length > 0)
                {
                    this.findResults.RemoveRange(0, this.findResults.Count);
                    this.findOnPageThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(FindOnPage_WorkerThread));
                    this.findOnPageThread.IsBackground = true;
                    Object[] parameters = new Object[5];
                    parameters[0] = text;
                    parameters[1] = findText;                    
                    parameters[2] = matchCase;
                    parameters[3] = useRegex;                    
                    parameters[4] = textEditor;
                    this.findOnPageThread.Start(parameters);
                }
                else
                {
                    textEditor.CancelSelect();
                    if (findText.Length == 0)
                        this.SetStatusText("");
                    else
                        this.SetStatusText("NO MATCHES FOUND");
                }
            }          
        }

        private void FindOnPage_WorkerThread(object parameters)
        {
            string text = (string)((object[])parameters)[0];
            string findText = (string)((object[])parameters)[1];
            bool matchCase = (bool)((object[])parameters)[2];
            bool useRegEx = (bool)((object[])parameters)[3];
            TextEditor textEditor = (TextEditor)((object[])parameters)[4];
            long startTicks = System.DateTime.Now.Ticks;

            int findIndex = 0;
            int matchLength = -1;
            List<FindResult> findResults = new List<FindResult>();
            System.Text.RegularExpressions.Regex regEx = null;
            if (useRegEx)
            {
                try
                {
                    regEx = new System.Text.RegularExpressions.Regex(findText, matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                catch
                {
                    return;
                }
            }

            while (true)
            {
                matchLength = -1;
                if (useRegEx)
                {
                    try
                    {
                        System.Text.RegularExpressions.Match regExMatch = regEx.Match(text, findIndex);
                        if (regExMatch.Success)
                        {
                            findIndex = regExMatch.Index;
                            matchLength = regExMatch.Length;
                        }                            
                    }
                    catch {}
                }
                else
                {
                    findIndex = text.IndexOf(findText, findIndex, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    matchLength = findIndex >= 0 ? findText.Length : -1;
                }

                if (matchLength >= 0)
                {
                    findResults.Add(new FindResult(findIndex, (uint)matchLength));
                    if (findResults.Count % 10 == 0)
                    {
                        this.Dispatcher.BeginInvoke(new SetStatusText_Delegate(this.SetStatusText), findResults.Count.ToString() + " MATCHES");
                    }
                    findIndex += findText.Length;
                    if (findIndex >= text.Length)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            TimeSpan elapsedTime;            
            elapsedTime = new TimeSpan(System.DateTime.Now.Ticks - startTicks);
            if ((int)elapsedTime.TotalMilliseconds < 1000)
            {
                this.showFindOnPageResult.Wait(1000 - (int)elapsedTime.TotalMilliseconds);
            }
            
            this.Dispatcher.BeginInvoke(new Action(delegate {
                this.currentSearchIndex = -1;
                this.findResults = findResults;
                this.HighlightNextMatch();
            }));            
        }

        public void HighlightNextMatch()            
        {
            this.currentSearchIndex++;
            if (this.findResults.Count == 0)
            {
                this.currentSearchIndex = 0;
                this.SetStatusText("NO MATCHES FOUND");
                this.tab[this.currentTabIndex].TextEditor.CancelSelect();
            }
            else if (this.currentSearchIndex == this.findResults.Count)
            {
                // No more results to show
                this.SetStatusText("NO MORE MATCHES");
                this.tab[this.currentTabIndex].TextEditor.CancelSelect();
            }
            else
            {
                if (this.currentSearchIndex > this.findResults.Count)
                {
                    // loop over results
                    this.currentSearchIndex = 0;
                }
                FindResult findResult = this.findResults[this.currentSearchIndex];
                this.tab[this.currentTabIndex].TextEditor.Select(findResult.beginIndex, findResult.length);
                this.SetStatusText("MATCH " + (this.currentSearchIndex + 1) + " OF " + this.findResults.Count);
            }
        }

        public void HighlightPreviousMatch()
        {
            if (this.findResults.Count == 0)
            {
                this.currentSearchIndex = 0;
                this.SetStatusText("NO MATCHES FOUND");
                this.tab[this.currentTabIndex].TextEditor.CancelSelect();
            }
            else if (this.currentSearchIndex == 0)
            {
                // No more results to show
                this.SetStatusText("NO MORE MATCHES");
                this.tab[this.currentTabIndex].TextEditor.CancelSelect();
                this.currentSearchIndex--;
            }
            else
            {
                this.currentSearchIndex--;
                if (this.currentSearchIndex < 0)
                {
                    // loop over results
                    this.currentSearchIndex = this.findResults.Count - 1;
                }
                FindResult findResult = this.findResults[this.currentSearchIndex];
                this.tab[this.currentTabIndex].TextEditor.Select(findResult.beginIndex, findResult.length);
                this.SetStatusText("MATCH " + (this.currentSearchIndex + 1) + " OF " + this.findResults.Count);
            }
        }
        #endregion

        #region Status Bar
        void Caret_PositionChanged(object sender, EventArgs e)
        {
            // TODO: INTEGRATE:
            //Tab activeTab = this.GetActiveTab();
            //if (activeTab != null)
            //{
            //    Line.Content = activeTab.TextEditor.TextArea.Caret.Line.ToString();
            //    Column.Content = activeTab.TextEditor.TextArea.Caret.VisualColumn.ToString();
            //}
        }
        #endregion        
    }
}
