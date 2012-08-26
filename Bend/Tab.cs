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
using Microsoft.Windows.Shell;
using Microsoft.Win32;
using TextCoreControl;

namespace Bend
{
    class Tab
    {
        #region Member data
            private WrapPanel title;
            private TextBlock titleText;            
            private TextCoreControl.TextEditor textEditor;
            private String fullFileName;
            private Image closeButton;   

            private static FontFamily fontFamilySegoeUI;
            private static FontFamily fontFamilyConsolas;
            public static readonly TextCoreControl.CopyPasteManager CopyPasteManager;

            private System.IO.FileSystemWatcher fileChangedWatcher;
            long lastFileChangeTime;
            private static System.Threading.Semaphore showFileModifiedDialog = new System.Threading.Semaphore(1, 1);
        #endregion

        #region Properties
            internal WrapPanel Title {
                get { return title; }
            }

            internal UIElement CloseButton {
                get { return closeButton; }
            }

            internal TextEditor TextEditor {
                get { return textEditor; }
            }
            internal String FullFileName {
                get { return fullFileName; }                
            }
        #endregion

        #region Constructor
            static Tab()
            {
                // Static constructor
                fontFamilySegoeUI = new FontFamily("Segoe UI");
                fontFamilyConsolas = new FontFamily("Consolas");
                CopyPasteManager = new CopyPasteManager();
            }

            public Tab()
            {
                title = new WrapPanel();                
                titleText = new TextBlock();
                titleText.Text = "New File";
                titleText.Width = 110;
                titleText.Height = 34;
                titleText.Padding = new Thickness(5, 2, 0, 0);
                titleText.VerticalAlignment = VerticalAlignment.Top;
                titleText.TextAlignment = TextAlignment.Center;
                titleText.FontFamily = Tab.fontFamilySegoeUI;
                Microsoft.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(titleText, /*isHitTestable*/true);
                title.Children.Add(titleText);

                Separator seperator = new Separator();
                seperator.Width = 5;
                seperator.Visibility = Visibility.Hidden;
                title.Children.Add(seperator);

                closeButton = new Image();
                closeButton.Width = 8;
                closeButton.Height = 8;
                BitmapImage closeImage = new BitmapImage();
                closeImage.BeginInit();
                closeImage.UriSource = new Uri("pack://application:,,,/Bend;component/Images/Close-dot.png");
                closeImage.EndInit();                
                closeButton.Source = closeImage;
                Microsoft.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(closeButton, /*isHitTestable*/true);                
                title.Children.Add(closeButton);

                Microsoft.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(title, /*isHitTestable*/true);

                textEditor = new TextEditor();
                textEditor.CopyPasteManager = Tab.CopyPasteManager;
                textEditor.HorizontalAlignment = HorizontalAlignment.Stretch;
                textEditor.Margin = new Thickness(0);
                textEditor.VerticalAlignment = VerticalAlignment.Stretch;
                TextCoreControl.Settings.ShowLineNumber = true;

                this.fileChangedWatcher = null;
                this.lastFileChangeTime = 1;
                this.LoadOptions();
            }
        #endregion

        #region Public API
        private void SetFullFileName(String fullFileName)
        {
            if (this.fullFileName != fullFileName)
            {
                 // File changed
                if (this.fileChangedWatcher != null)
                {
                    this.fileChangedWatcher.EnableRaisingEvents = false;
                    this.fileChangedWatcher.Dispose();
                    this.fileChangedWatcher = null;
                }

                try
                {
                    this.fileChangedWatcher = new System.IO.FileSystemWatcher(System.IO.Path.GetDirectoryName(fullFileName), System.IO.Path.GetFileName(fullFileName));
                    this.fileChangedWatcher.NotifyFilter = System.IO.NotifyFilters.LastWrite;
                    this.fileChangedWatcher.Changed += new System.IO.FileSystemEventHandler(fileChangedWatcher_Changed);
                    this.fileChangedWatcher.EnableRaisingEvents = true;
                }
                catch
                {
                    // For some reason openeing files from temp folder hits this.
                }
            }

            this.fullFileName = fullFileName;
            this.titleText.Text = System.IO.Path.GetFileName(fullFileName);
            this.title.ToolTip = fullFileName;            
        }

        internal void OpenFile(String fullFileName)
        {
            this.textEditor.LoadFile(fullFileName);            
            this.SetFullFileName(fullFileName);
        }

        internal void SaveFile(String fullFileName)
        {
            System.Threading.Interlocked.Exchange(ref this.lastFileChangeTime, System.DateTime.Now.AddSeconds(2).Ticks);
            this.TextEditor.SaveFile(fullFileName);            
            this.SetFullFileName(fullFileName);
        }

        void fileChangedWatcher_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            if (this.lastFileChangeTime < System.DateTime.Now.Ticks)
            {
                System.Threading.Interlocked.Exchange(ref this.lastFileChangeTime, System.DateTime.Now.AddSeconds(2).Ticks);
                object[] copyOfEventArgs = { e };
                if (showFileModifiedDialog.WaitOne(0))
                {
                    System.Threading.Interlocked.Exchange(ref this.lastFileChangeTime, System.DateTime.Now.AddSeconds(2).Ticks);
                    titleText.Dispatcher.BeginInvoke(new fileChangedWatcher_ChangedInUIThread_Delegate(fileChangedWatcher_ChangedInUIThread), copyOfEventArgs);
                }
            }
        }
                
        private delegate void fileChangedWatcher_ChangedInUIThread_Delegate(System.IO.FileSystemEventArgs e);
        internal void fileChangedWatcher_ChangedInUIThread(System.IO.FileSystemEventArgs e)
        {
            double originalOpacity = this.Title.Opacity;
            this.Title.Opacity = 0.2;
            if (StyledMessageBox.Show("FILE MODIFIED", e.FullPath + "\n\nwas modified outside this application, do you want to reload ?"))
            {
                this.OpenFile(this.fullFileName);
                System.Threading.Interlocked.Exchange(ref this.lastFileChangeTime, System.DateTime.Now.AddSeconds(2).Ticks);
            }
            this.Title.Opacity = originalOpacity;
            showFileModifiedDialog.Release();
        }

        internal void Close()
        {
            if (this.fileChangedWatcher != null)
            {
                this.fileChangedWatcher.EnableRaisingEvents = false;
                this.fileChangedWatcher.Dispose();
                this.fileChangedWatcher = null;
            }
        }

        internal void LoadOptions()
        {
            TextCoreControl.Settings.AutoWrap = PersistantStorage.StorageObject.TextWordWrap;
            TextCoreControl.Settings.UseStringForTab = PersistantStorage.StorageObject.TextUseSpaces;
            string tabString = "";
            for (int i = 0; i < PersistantStorage.StorageObject.TextIndent; i++)
            {
                tabString += " ";
            }
            TextCoreControl.Settings.TabString = tabString;
            TextCoreControl.Settings.AllowSmoothScrollBy = PersistantStorage.StorageObject.SmoothScrolling;

            // TODO: INTEGRATE:
            /*
            
            this.textEditor.Options.ShowBoxForControlCharacters = PersistantStorage.StorageObject.TextFormatControlCharacters;
            this.textEditor.Options.EnableHyperlinks = PersistantStorage.StorageObject.TextFormatHyperLinks;
            this.textEditor.Options.EnableEmailHyperlinks = PersistantStorage.StorageObject.TextFormatEmailLinks;
            if (PersistantStorage.StorageObject.TextShowFormatting)
            {
                this.textEditor.Options.ShowSpaces = true;
                this.textEditor.Options.ShowTabs = true;
                this.textEditor.Options.ShowEndOfLine = true;
            }
            else
            {
                this.textEditor.Options.ShowSpaces = false;
                this.textEditor.Options.ShowTabs = false;
                this.textEditor.Options.ShowEndOfLine = false;
            }
            
            */
            this.textEditor.RefreshDisplay();
        }
        #endregion
    }
}
