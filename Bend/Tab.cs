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
using Microsoft.Win32;
using TextCoreControl;

namespace Bend
{
    class Tab
    {
        #region Member data
            private TabTitle title;   
            private TextCoreControl.TextEditor textEditor;
            private String fullFileName;

            public static readonly TextCoreControl.CopyPasteManager CopyPasteManager;

            private System.IO.FileSystemWatcher fileChangedWatcher;
            long lastFileChangeTime;
            private static System.Threading.Semaphore showFileModifiedDialog = new System.Threading.Semaphore(1, 1);

            struct FindResult
            {
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
            static System.Threading.Thread findOnPageThread;
            static System.Threading.SemaphoreSlim accessFindOnPageData;
            string findText;
            bool findUseRegex;
            bool findMatchCase;
            bool encodingChecked;
        #endregion

        #region Properties
            internal TabTitle Title {
                get { return title; }
            }

            internal TextEditor TextEditor {
                get { return textEditor; }
            }

            internal String FullFileName {
                get { return fullFileName; }                
            }

            internal String FindText {
                get { return findText; }
            }
        #endregion

        #region Constructor
            static Tab()
            {
                // Static constructor
                CopyPasteManager = new CopyPasteManager();
                accessFindOnPageData = new System.Threading.SemaphoreSlim(1, 1);
            }

            public Tab()
            {
                this.title = new TabTitle();                

                textEditor = new TextEditor();
                textEditor.CopyPasteManager = Tab.CopyPasteManager;
                textEditor.HorizontalAlignment = HorizontalAlignment.Stretch;
                textEditor.Margin = new Thickness(0);
                textEditor.VerticalAlignment = VerticalAlignment.Stretch;
                TextCoreControl.Settings.ShowLineNumber = true;

                this.fileChangedWatcher = null;
                this.lastFileChangeTime = 1;
                this.LoadOptions();
                this.findResults = new List<FindResult>();
                this.currentSearchIndex = 0;
                this.findText = null;

                this.TextEditor.Document.ContentChange += new Document.ContentChangeEventHandler(Document_ContentChange);
                this.TextEditor.Document.OrdinalShift += new Document.OrdinalShiftEventHandler(Document_OrdinalShift);
            }
        #endregion

        #region Public API
        internal void SetFullFileName(String fullFileName)
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
            this.title.TitleText = System.IO.Path.GetFileName(fullFileName);
            this.title.ToolTip = fullFileName;            
        }

        internal void OpenFile(String fullFileName)
        {
            try
            {
                this.textEditor.LoadFile(fullFileName);
                this.SetFullFileName(fullFileName);
                this.encodingChecked = false;
            }
            catch (Exception exception)
            {
                StyledMessageBox.Show("ERROR", "Error Opening File: " + exception.ToString());
            }
        }

        internal void CheckEncoding()
        {
            if (!this.encodingChecked && this.TextEditor.DisplayManager.HasSeenNonAsciiCharacters && this.TextEditor.Document.CurrentEncoding == Encoding.ASCII)
            {
                // Potential data loss. Show the File Encoding dialog.
                FileEncodingMessageBox.Show(this.TextEditor, /*warningMode*/true);
                this.encodingChecked = true;
            }
        }

        internal void SaveFile(String fullFileName)
        {
            System.Threading.Interlocked.Exchange(ref this.lastFileChangeTime, System.DateTime.Now.AddSeconds(2).Ticks);
            try
            {
                this.TextEditor.SaveFile(fullFileName);
            }
            catch (Exception exception)
            {
                StyledMessageBox.Show("ERROR", "Error Saving File: " + exception.ToString());
            }
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
                    title.Dispatcher.BeginInvoke(new fileChangedWatcher_ChangedInUIThread_Delegate(fileChangedWatcher_ChangedInUIThread), copyOfEventArgs);
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
                try
                {
                    this.textEditor.LoadFile(fullFileName);
                    System.Threading.Interlocked.Exchange(ref this.lastFileChangeTime, System.DateTime.Now.AddSeconds(2).Ticks);
                }
                catch
                {
                    
                }
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
            TextCoreControl.Settings.EnableSyntaxHighlighting = PersistantStorage.StorageObject.SyntaxHighlighting;
            TextCoreControl.Settings.SetFontFamily(PersistantStorage.StorageObject.DefaultFontFamily);

            TextCoreControl.Settings.ShowFormatting = PersistantStorage.StorageObject.TextShowFormatting;
            TextCoreControl.Settings.PreserveIndentLevel = PersistantStorage.StorageObject.PreserveIndent;

            this.textEditor.NotifyOfSettingsChange();
        }
        #endregion

        #region Find On Page

        void Document_OrdinalShift(Document document, int beginOrdinal, int shift)
        {
            if (Tab.findOnPageThread != null)
            {
                Tab.findOnPageThread.Join();
                Tab.findOnPageThread = null;
            }

            // Only care about content deletion
            for (int i = this.findResults.Count - 1; i >= 0; i--)
            {
                int beginIndex = this.findResults[i].beginIndex;
                Document.AdjustOrdinalForShift(beginOrdinal, shift, ref beginIndex);
                this.findResults[i] = new FindResult(beginIndex, this.findResults[i].length);
            }
        }

        void Document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (Tab.findOnPageThread != null)
            {
                Tab.findOnPageThread.Join();
                Tab.findOnPageThread = null;
            }

            if (beginOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // full reset - clear everything
                this.ClearFindOnPage();
            }
            else
            {
                // Only care about content deletion
                if (beginOrdinal == endOrdinal)
                {
                    int indexShift = 0;
                    for (int i = this.findResults.Count - 1; i >= 0; i--)
                    {
                        if (this.findResults[i].beginIndex == beginOrdinal)
                        {
                            this.findResults.RemoveAt(i);
                            if (this.currentSearchIndex >= i) indexShift++;
                        }
                    }
                    this.currentSearchIndex -= indexShift;
                }
            }
        }
        
        public delegate void SetStatusText_Delegate(string status);

        /// <summary>
        ///     Find searchstring and highlights the first instance. Also populates this.findResults.
        /// </summary>
        public void StartFindOnPage(MainWindow mainWindow, string findText, bool matchCase, bool useRegex)
        {
            // This check is needed so that we dont start find on page again when we switch back to this tab from another tab.
            if (this.findText != findText || this.findMatchCase != matchCase || this.findUseRegex != useRegex)
            { 
                Tab.accessFindOnPageData.Wait();
                if (Tab.findOnPageThread != null)
                {
                    Tab.findOnPageThread.Abort();
                    Tab.findOnPageThread = null;
                }
                Tab.accessFindOnPageData.Release();

                string text = this.TextEditor.Document.Text;
                TextEditor textEditor = this.TextEditor;
                this.findText = findText;
                this.findMatchCase = matchCase;
                this.findUseRegex = useRegex;
                if (findText.Length > 0 && text.Length > 0)
                {
                    this.findResults.RemoveRange(0, this.findResults.Count);
                    Tab.findOnPageThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(FindOnPage_WorkerThread));
                    Object[] parameters = new Object[6];
                    parameters[0] = text;
                    parameters[1] = findText;
                    parameters[2] = matchCase;
                    parameters[3] = useRegex;
                    parameters[4] = textEditor;
                    parameters[5] = mainWindow;
                    Tab.findOnPageThread.Start(parameters);
                }
                else
                {
                    this.ClearFindOnPage();
                    if (findText.Length == 0)
                        mainWindow.SetStatusText("", MainWindow.StatusType.STATUS_CLEAR);
                    else
                        mainWindow.SetStatusText("NO MATCHES FOUND", MainWindow.StatusType.STATUS_FINDONPAGE);
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
            MainWindow mainWindow = (MainWindow) ((object[])parameters)[5];
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
                    catch { }
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
                        mainWindow.Dispatcher.BeginInvoke(
                          new Action(
                              delegate {
                                  mainWindow.SetStatusText(findResults.Count.ToString() + " MATCHES", MainWindow.StatusType.STATUS_FINDONPAGE);
                              }
                          ));                        
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
            
            // No lock needed as all consumers Join the thread before accessing findResults.
            Tab.accessFindOnPageData.Wait();
            this.currentSearchIndex = -1;
            this.findResults = findResults;
            Tab.findOnPageThread = null;
            Tab.accessFindOnPageData.Release();

            mainWindow.Dispatcher.BeginInvoke(
                new Action(
                    delegate {
                        mainWindow.SetStatusText(this.HighlightNextMatch(), MainWindow.StatusType.STATUS_FINDONPAGE);
                    }
                )
            );            
        }

        public void ClearFindOnPage()
        {
            if (Tab.findOnPageThread != null)
            {
                Tab.findOnPageThread.Join();
            }
            System.Diagnostics.Debug.Assert(Tab.findOnPageThread == null);
                        
            if (findResults.Count != 0)
            {
                this.findResults.RemoveRange(0, this.findResults.Count);                
            }
            this.TextEditor.CancelSelect();
            this.currentSearchIndex = 0;
            this.findText = null;
        }                

        public string HighlightNextMatch()
        {
            if (Tab.findOnPageThread != null)
            {
                Tab.findOnPageThread.Join();
            }
            System.Diagnostics.Debug.Assert(Tab.findOnPageThread == null);
            
            string status = "";
            this.currentSearchIndex++;
            if (this.findResults.Count == 0)
            {
                this.currentSearchIndex = 0;
                status = ("NO MATCHES FOUND");
                this.TextEditor.CancelSelect();
            }
            else if (this.currentSearchIndex == this.findResults.Count)
            {
                // No more results to show
                status = ("NO MORE MATCHES");
                this.TextEditor.CancelSelect();
            }
            else
            {
                if (this.currentSearchIndex > this.findResults.Count)
                {
                    // loop over results
                    this.currentSearchIndex = 0;
                }
                FindResult findResult = this.findResults[this.currentSearchIndex];
                this.TextEditor.Select(findResult.beginIndex, findResult.length);
                status = ("MATCH " + (this.currentSearchIndex + 1) + " OF " + this.findResults.Count);
            }

            return status;
        }

        public string HighlightPreviousMatch()
        {
            if (Tab.findOnPageThread != null)
            {
                Tab.findOnPageThread.Join();
            }
            System.Diagnostics.Debug.Assert(Tab.findOnPageThread == null);

            string status = "";
            if (this.findResults.Count == 0)
            {
                this.currentSearchIndex = 0;
                status = ("NO MATCHES FOUND");
                this.TextEditor.CancelSelect();
            }
            else if (this.currentSearchIndex == 0)
            {
                // No more results to show
                status = ("NO MORE MATCHES");
                this.TextEditor.CancelSelect();
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
                this.TextEditor.Select(findResult.beginIndex, findResult.length);
                status = ("MATCH " + (this.currentSearchIndex + 1) + " OF " + this.findResults.Count);
            }

            return status;
        }

        public string HighlightCurrentMatch()
        {
            string status = "";
            if (this.findText != null)
            {
                if (Tab.findOnPageThread != null)
                {
                    Tab.findOnPageThread.Join();
                }
                System.Diagnostics.Debug.Assert(Tab.findOnPageThread == null);

                if (this.findResults.Count == 0)
                {
                    this.currentSearchIndex = 0;
                    status = ("NO MATCHES FOUND");
                    this.TextEditor.CancelSelect();
                }
                else if (this.currentSearchIndex >= 0 && this.currentSearchIndex < this.findResults.Count)
                {
                    FindResult findResult = this.findResults[this.currentSearchIndex];
                    status = ("MATCH " + (this.currentSearchIndex + 1) + " OF " + this.findResults.Count);
                    this.TextEditor.Select(findResult.beginIndex, findResult.length);
                }
            }
            return status;
        }
        #endregion
    }
}
