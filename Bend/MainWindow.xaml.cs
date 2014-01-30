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
using System.Windows.Shell;
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

        List<Tab> tab;
        int currentTabIndex;

        BitmapImage maximizeImage;
        BitmapImage restoreImage;

        WindowChrome windowChrome;        

        bool isFullScreen;
        bool isInSettingsAnimation;

        StatusType currentStatusType;

        InterBendCommunication interBendCommuncation;
        
        object dragDropSource;
        System.Windows.Threading.DispatcherTimer tabDropMarkerTimer;
        TabDragVisual tabDragVisual;
        Cursor grabCursor;
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
            WindowChrome.SetWindowChrome(this, this.windowChrome);
            this.isFullScreen = false;
            this.currentStatusType = StatusType.STATUS_OTHER;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(Logo, /*hitTestVisible*/true);            
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(BackButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(FullscreenButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(MaxButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(MinButton, /*hitTestVisible*/true);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(QuitButton, /*hitTestVisible*/true);
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
            StatusBar.Visibility = PersistantStorage.StorageObject.ShowStatusBar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

            Application.Current.Resources["BackgroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.BackgroundColor);
            Application.Current.Resources["ForegroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.ForegroundColor);
            Application.Current.Resources["ScrollButtonBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.ScrollButtonColor);
            Application.Current.Resources["LogoForegroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.LogoForegroundColor);
            Application.Current.Resources["LogoBackgroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.LogoBackgroundColor);
            Application.Current.Resources["MenuSelectedBackgroundBrush"] = new SolidColorBrush(PersistantStorage.StorageObject.CurrentTheme.MenuSelectedBackgroundColor);
            
            BitmapImage backgroundImage = new BitmapImage();
            backgroundImage.BeginInit();
            backgroundImage.UriSource = new Uri("pack://application:,,,/Bend;component/" + PersistantStorage.StorageObject.CurrentTheme.BaseBackgroundImage);
            backgroundImage.EndInit();
            BaseBackgroundImage.ImageSource = backgroundImage;

            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultForegroundColor, ref TextCoreControl.Settings.DefaultForegroundColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultBackgroundColor, ref TextCoreControl.Settings.DefaultBackgroundColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultSelectionColor, ref TextCoreControl.Settings.DefaultSelectionColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultSelectionOutlineColor, ref TextCoreControl.Settings.DefaultSelectionOutlineColor);
            TextCoreControl.Settings.CopyColor(PersistantStorage.StorageObject.CurrentTheme.DefaultSelectionDimColor, ref TextCoreControl.Settings.DefaultSelectionDimColor);
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

            interBendCommuncation = new InterBendCommunication(mainWindow);
            interBendCommuncation.RecivedFileNameEvent += new InterBendCommunication.RecivedFileNameEventHandler(RecivedFileNameEvent);

            this.GiveFeedback += TabDrag_GiveFeedback;
            String grabCursorFilePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Images\\Grab.cur";
            this.grabCursor = new Cursor(grabCursorFilePath);
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
                newTab.TextEditor.DisplayManager.CaretPositionChanged += TextEditor_CaretPositionChanged;
                newTab.Title.MouseMove += TabTitle_MouseMove;

                newTab.Title.Opacity = 0.5;
                newTab.TextEditor.Visibility = Visibility.Hidden;

                TabBar.Children.Add(newTab.Title);
                Editor.Children.Add(newTab.TextEditor);
                newTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);
                newTab.TextEditor.DisplayManager.SelectionChange += DisplayManager_SelectionChange;
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

        #region Drag Drop
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

                        if (insertAfterTabIndex != currentTabIndex)
                        {
                            // Need to move a tab from currentTabIndex to after insertAfterTabIndex;                            
                            if (insertAfterTabIndex < currentTabIndex)
                            {
                                insertAfterTabIndex++;
                            }

                            // Switch focus to non existant tab.
                            this.SwitchTabFocusTo(-1);
                            Tab tabBeingMoved = tab[currentTabIndex];
                            tab.RemoveAt(currentTabIndex);
                            TabBar.Children.RemoveAt(currentTabIndex);
                            Editor.Children.RemoveAt(currentTabIndex);
                            tab.Insert(insertAfterTabIndex, tabBeingMoved);
                            TabBar.Children.Insert(insertAfterTabIndex, tabBeingMoved.Title);
                            Editor.Children.Insert(insertAfterTabIndex, tabBeingMoved.TextEditor);
                            this.SwitchTabFocusTo(insertAfterTabIndex);
                        }
                    }
                }
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
                else if ((dataFormats.Length == 2 && dataFormats[1] == "BEND_FILE_SERIALIZED" && dataFormats[0] == "BEND_FILE_NAME"))
                {
                    // Another bend is trying to send us a tab.   
                }                
            }
            
            if (tabDropMarkerTimer != null)
                tabDropMarkerTimer.Stop();
            
            for (int i = 0; i < tab.Count; i++)
            {
                tab[i].Title.Margin = new Thickness(0);
            }
        }
        
        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            if (this.tabDropMarkerTimer == null)
            {
                tabDropMarkerTimer = new System.Windows.Threading.DispatcherTimer();
                tabDropMarkerTimer.Tick += tabDropMarkerTimer_Tick;
                tabDropMarkerTimer.Interval = new TimeSpan(0, 0, 1);             
            }            
            tabDropMarkerTimer.Start();
        }

        void tabDropMarkerTimer_Tick(object sender, EventArgs e)
        {
            tabDropMarkerTimer.Stop();
            for (int i = 0; i < tab.Count; i++)
            {
                tab[i].Title.Margin = new Thickness(0);
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

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data is System.Windows.DataObject)
            {
                DataObject dataObject = (System.Windows.DataObject)e.Data;
                string[] dataFormats = dataObject.GetFormats();
                if (dataObject.ContainsFileDropList() || (dataFormats.Length == 2 && dataFormats[1] == "BEND_FILE_SERIALIZED" && dataFormats[0] == "BEND_FILE_NAME"))
                {
                    if (tabDropMarkerTimer != null)
                        tabDropMarkerTimer.Stop();

                    double mouseX = e.GetPosition(WindowDrag).X;
                    int insertAfterTabIndex = FindTabDropPosition(mouseX);

                    for (int i = 0; i < tab.Count; i++)
                    {
                        tab[i].Title.Margin = new Thickness(0);
                    }
                    if (insertAfterTabIndex >= 0)
                    {
                        if (tab[insertAfterTabIndex].Title != dragDropSource)
                        { 
                            tab[insertAfterTabIndex].Title.Margin = new Thickness(0, 0, 100, 0);
                        }
                    }
                    else
                    {
                        if (tab[0].Title != dragDropSource)
                        {
                            tab[0].Title.Margin = new Thickness(100, 0, 0, 0);
                        }
                    }
                }
            }
        }
        
        void TabTitle_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                string fullFileName = null;
                // Find the Tab
                int tabIndex = 0;
                for (int i = 0; i < tab.Count; i++)
                {
                    if (tab[i].Title == sender)
                    {
                        fullFileName = tab[i].FullFileName;
                        tabIndex = i;
                    }
                }

                if (fullFileName == null)
                {
                    fullFileName = System.IO.Path.GetTempFileName();
                    tab[tabIndex].TextEditor.SaveFile(fullFileName);
                }
                                
                // Package the data.
                DataObject data = new DataObject();
                    
                if(Keyboard.IsKeyDown(Key.LeftAlt))
                { 
                    // Copy the file to any other application.
                    System.Collections.Specialized.StringCollection fileList = new System.Collections.Specialized.StringCollection();
                    fileList.Add(fullFileName);
                    data.SetFileDropList(fileList);
                
                    // Initiate the drag-and-drop operation.    
                    DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);                        
                }
                else
                { 
                    // Move the tab to another bend.
                    data.SetData("BEND_FILE_SERIALIZED", fullFileName);
                    data.SetData("BEND_FILE_NAME", tab[tabIndex].FullFileName);                    
                    
                    this.tabDragVisual = new TabDragVisual(tab[tabIndex].TextEditor, tab[tabIndex].TitleText);
                    this.dragDropSource = sender;
                    // Initiate the drag-and-drop operation.    
                    DragDrop.DoDragDrop(this, data, DragDropEffects.All);
                    this.dragDropSource = null;
                    this.tabDragVisual.Close();
                    this.tabDragVisual = null;
                }
            }
        }

        void TabDrag_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (tabDragVisual != null)
            {
               this.tabDragVisual.UpdatePosition();
               this.tabDragVisual.Show();              
               Mouse.SetCursor(grabCursor);
               e.Handled = true;
            }
        }
        #endregion

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
                ClientAreaGrid.Margin = new Thickness(4);
                this.windowChrome.GlassFrameThickness = new Thickness(0);
                this.ResizeCrimp.Visibility = System.Windows.Visibility.Hidden;
                MaxButton.Source = this.restoreImage;                                                
            }
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                ClientAreaGrid.Margin = new Thickness(0);
                this.windowChrome.GlassFrameThickness = new Thickness(1);
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
            if (this.currentTabIndex >= 0)
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
                        this.tab[this.currentTabIndex].SaveFile(dlg.FileName);
                        fileSaved = true;
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
                textEditor.NotifyOfSettingsChange();
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
                    SaveFileDialog dlg = new SaveFileDialog();
                    if (this.currentTabIndex >= 0 && this.tab[this.currentTabIndex].FullFileName != null)
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

        #region Tab bar management
        private void AddNewTab()
        {
            Tab newTab = new Tab();
            tab.Add(newTab);
            // Hook up tab band event handlers
            newTab.Title.MouseLeftButtonUp += this.TabClick;
            newTab.Title.ContextMenu = (ContextMenu)Resources["TabTitleContextMenu"];
            newTab.CloseButton.MouseLeftButtonUp += this.TabClose;
            newTab.TextEditor.DisplayManager.CaretPositionChanged += TextEditor_CaretPositionChanged;
            newTab.Title.MouseMove += TabTitle_MouseMove;            

            TabBar.Children.Add(newTab.Title);
            Editor.Children.Add(newTab.TextEditor);
            newTab.TextEditor.DisplayManager.ContextMenu += new DisplayManager.ShowContextMenuEventHandler(DisplayManager_ContextMenu);
            newTab.TextEditor.DisplayManager.SelectionChange += DisplayManager_SelectionChange;
        }
                
        void DisplayManager_ContextMenu()
        {
            if( Editor.ContextMenu != null )
            {
                Editor.ContextMenu.PlacementTarget = Editor;
                Editor.ContextMenu.IsOpen = true;
                object element = Editor.ContextMenu.FindName("OpenLink");
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
                tab[currentTabIndex].TextEditor.Visibility = Visibility.Hidden;
                tab[currentTabIndex].Title.Opacity = 0.5;
            }
            
            if (tabIndex >= 0)
            { 
                this.currentTabIndex = tabIndex;
                tab[tabIndex].Title.Opacity = 1.0;
                tab[tabIndex].TextEditor.Visibility = Visibility.Visible;
                tab[tabIndex].TextEditor.SetFocus();
                this.FindText.Text = tab[tabIndex].FindText;
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

            // Delete current tab                    
            TabBar.Children.Remove(tab[tabIndex].Title);
            Editor.Children.Remove(tab[tabIndex].TextEditor);
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
            if (this.CurrentTab != null)
            {
                if (e.Key == Key.Enter)
                {
                    if (this.CurrentTab.FindText == this.FindText.Text)
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
                        this.CurrentTab.StartFindOnPage(this, FindText.Text, /*matchCase*/false, /*useRegex*/false);
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
            if (this.CurrentTab != null)
            {
                this.CurrentTab.StartFindOnPage(this, FindText.Text, /*matchCase*/false, /*useRegex*/false);
            }
        }
        
        void DisplayManager_SelectionChange()
        {
            if (this.currentStatusType == StatusType.STATUS_FINDONPAGE)
                this.SetStatusText("", StatusType.STATUS_CLEAR);
        }
        #endregion

        #region Status Bar

        void TextEditor_CaretPositionChanged(int lineNumber, int columnNumber)
        {
            Line.Content = lineNumber.ToString();
            Column.Content = columnNumber.ToString();
        }

        #endregion        
    }
}
 