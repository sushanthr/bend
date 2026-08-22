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
            long lastSavedWriteTimeUtc;
            private int fileChangeNotificationPending;

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
            System.Threading.CancellationTokenSource findCancellation;
            readonly object findResultsLock = new object();
            FindOptions findOptions;
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

            internal FindOptions FindOptions {
                get { return this.findOptions; }
            }
        #endregion

        #region Constructor
            static Tab()
            {
                // Static constructor
                CopyPasteManager = new CopyPasteManager();
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
                this.lastSavedWriteTimeUtc = 0;
                this.LoadOptions();
                this.findResults = new List<FindResult>();
                this.currentSearchIndex = 0;

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

                // Register a new file watcher
                if (fullFileName != String.Empty)
                {
                    try
                    {
                        this.fileChangedWatcher = new System.IO.FileSystemWatcher(System.IO.Path.GetDirectoryName(fullFileName), System.IO.Path.GetFileName(fullFileName));
                        this.fileChangedWatcher.NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.Size;
                        this.fileChangedWatcher.Changed += new System.IO.FileSystemEventHandler(fileChangedWatcher_Changed);
                        this.fileChangedWatcher.Created += new System.IO.FileSystemEventHandler(fileChangedWatcher_Changed);
                        this.fileChangedWatcher.Deleted += new System.IO.FileSystemEventHandler(fileChangedWatcher_Changed);
                        this.fileChangedWatcher.Renamed += new System.IO.RenamedEventHandler(fileChangedWatcher_Changed);
                        this.fileChangedWatcher.EnableRaisingEvents = true;
                    }
                    catch
                    {
                        // For some reason openeing files from temp folder hits this.
                    }
                    this.title.TitleText = System.IO.Path.GetFileName(fullFileName);
                    this.fullFileName = fullFileName;
                    this.title.ToolTip = fullFileName;  
                }
                else
                {
                    this.fullFileName = String.Empty;
                    this.title.ToolTip = null;
                } 
            }       
        }

        internal bool OpenFile(String fullFileName)
        {
            try
            {
                this.textEditor.LoadFile(fullFileName);
                this.SetFullFileName(fullFileName);
                this.encodingChecked = false;
                return true;
            }
            catch (Exception exception)
            {
                StyledMessageBox.Show("ERROR", "Error Opening File: " + exception.ToString());
                return false;
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

        internal bool SaveFile(String fullFileName)
        {
            try
            {
                this.TextEditor.SaveFile(fullFileName);
                System.Threading.Interlocked.Exchange(ref this.lastSavedWriteTimeUtc, System.IO.File.GetLastWriteTimeUtc(fullFileName).Ticks);
                this.SetFullFileName(fullFileName);
                return true;
            }
            catch (Exception exception)
            {
                StyledMessageBox.Show("ERROR", "Error Saving File: " + exception.ToString());
                return false;
            }
        }

        void fileChangedWatcher_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            long currentWriteTime = System.IO.File.Exists(fullFileName) ? System.IO.File.GetLastWriteTimeUtc(fullFileName).Ticks : 0;
            if (currentWriteTime == 0 || currentWriteTime != System.Threading.Interlocked.Read(ref this.lastSavedWriteTimeUtc))
            {
                if (System.Threading.Interlocked.Exchange(ref this.fileChangeNotificationPending, 1) == 0)
                {
                    object[] copyOfEventArgs = { e };
                    title.Dispatcher.BeginInvoke(new fileChangedWatcher_ChangedInUIThread_Delegate(fileChangedWatcher_ChangedInUIThread), copyOfEventArgs);
                }
            }
        }
                
        private delegate void fileChangedWatcher_ChangedInUIThread_Delegate(System.IO.FileSystemEventArgs e);
        internal void fileChangedWatcher_ChangedInUIThread(System.IO.FileSystemEventArgs e)
        {
            double originalOpacity = this.Title.Opacity;
            try
            {
                this.Title.Opacity = 0.2;
                if (!System.IO.File.Exists(fullFileName))
                {
                    StyledMessageBox.Show("FILE REMOVED", e.FullPath + "\n\nwas removed or renamed outside Bend. Your open document has been kept.");
                    this.TextEditor.Document.HasUnsavedContent = true;
                    return;
                }

                string warning = this.TextEditor.Document.HasUnsavedContent
                    ? "\n\nwas modified outside Bend. Reloading will discard your unsaved changes. Reload?"
                    : "\n\nwas modified outside Bend. Reload?";
                if (StyledMessageBox.Show("FILE MODIFIED", e.FullPath + warning))
                {
                    try
                    {
                        this.textEditor.LoadFile(fullFileName);
                        System.Threading.Interlocked.Exchange(ref this.lastSavedWriteTimeUtc, System.IO.File.GetLastWriteTimeUtc(fullFileName).Ticks);
                    }
                    catch (Exception exception)
                    {
                        StyledMessageBox.Show("ERROR", "Error Reloading File: " + exception.Message);
                    }
                }
            }
            finally
            {
                this.Title.Opacity = originalOpacity;
                System.Threading.Interlocked.Exchange(ref this.fileChangeNotificationPending, 0);
            }
        }

        internal void Close()
        {
            CancelFind();
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
            CancelFind();

            lock (findResultsLock)
            {
                for (int i = this.findResults.Count - 1; i >= 0; i--)
                {
                    int beginIndex = this.findResults[i].beginIndex;
                    Document.AdjustOrdinalForShift(beginOrdinal, shift, ref beginIndex);
                    this.findResults[i] = new FindResult(beginIndex, this.findResults[i].length);
                }
            }
        }

        void Document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            CancelFind();

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
                    lock (findResultsLock)
                    {
                        for (int i = this.findResults.Count - 1; i >= 0; i--)
                        {
                            if (this.findResults[i].beginIndex == beginOrdinal)
                            {
                                this.findResults.RemoveAt(i);
                                if (this.currentSearchIndex >= i) indexShift++;
                            }
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
        public void StartFindOnPage(MainWindow mainWindow, FindOptions findOptions)
        {
            // This check is needed so that we dont start find on page again when we switch back to this tab from another tab.
            if (this.findOptions != findOptions)
            {
                CancelFind();
                string text = this.TextEditor.Document.Text;
                this.findOptions = findOptions;
                if (findOptions.FindText.Length > 0 && text.Length > 0)
                {
                    lock (findResultsLock) this.findResults.Clear();
                    var cancellation = new System.Threading.CancellationTokenSource();
                    this.findCancellation = cancellation;
                    System.Threading.Tasks.Task.Run(() => FindOnPage(text, findOptions, cancellation.Token))
                        .ContinueWith(task => CompleteFind(mainWindow, findOptions, cancellation, task),
                            System.Threading.CancellationToken.None,
                            System.Threading.Tasks.TaskContinuationOptions.None,
                            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                }
                else
                {
                    this.ClearFindOnPage();
                    if (findOptions.FindText.Length == 0)
                        mainWindow.SetStatusText("", MainWindow.StatusType.STATUS_CLEAR);
                    else
                        mainWindow.SetStatusText("NO MATCHES FOUND", MainWindow.StatusType.STATUS_FINDONPAGE);
                }
            }
        }
        
        private List<FindResult> FindOnPage(string text, FindOptions findOptions, System.Threading.CancellationToken cancellationToken)
        {
            int findIndex = 0;
            int matchLength = -1;
            List<FindResult> findResults = new List<FindResult>();
            System.Text.RegularExpressions.Regex regEx = null;
            if (findOptions.FindUseRegex)
            {
                var options = findOptions.FindMatchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                regEx = new System.Text.RegularExpressions.Regex(findOptions.FindText, options, TimeSpan.FromSeconds(2));
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                matchLength = -1;
                if (findOptions.FindUseRegex)
                {
                    System.Text.RegularExpressions.Match regExMatch = regEx.Match(text, findIndex);
                    if (regExMatch.Success)
                    {
                        findIndex = regExMatch.Index;
                        matchLength = regExMatch.Length;
                    }
                }
                else
                {
                    findIndex = text.IndexOf(findOptions.FindText, findIndex, findOptions.FindMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    matchLength = findIndex >= 0 ? findOptions.FindText.Length : -1;
                }

                if (matchLength >= 0)
                {
                    findResults.Add(new FindResult(findIndex, (uint)matchLength));
                    findIndex += Math.Max(matchLength, 1);
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
            
            return findResults;
        }

        private void CompleteFind(MainWindow mainWindow, FindOptions options, System.Threading.CancellationTokenSource cancellation, System.Threading.Tasks.Task<List<FindResult>> task)
        {
            if (this.findCancellation != cancellation || cancellation.IsCancellationRequested)
            {
                cancellation.Dispose();
                return;
            }
            this.findCancellation = null;
            cancellation.Dispose();
            if (task.IsFaulted)
            {
                mainWindow.SetStatusText(task.Exception.InnerException is System.Text.RegularExpressions.RegexMatchTimeoutException ? "SEARCH TIMED OUT" : "INVALID SEARCH", MainWindow.StatusType.STATUS_FINDONPAGE);
                return;
            }

            List<FindResult> results = task.Result;
            if (options.IsFindAndReplaceInSelection)
                results = results.Where(result => this.TextEditor.IsInBackgroundHighlight(this.TextEditor.Document.GetOrdinalForTextIndex(result.beginIndex))).ToList();
            lock (findResultsLock) this.findResults = results;
            this.currentSearchIndex = -1;
            mainWindow.SetStatusText(this.HighlightNextMatch(), MainWindow.StatusType.STATUS_FINDONPAGE);
        }

        private void CancelFind()
        {
            var cancellation = this.findCancellation;
            this.findCancellation = null;
            if (cancellation != null)
                cancellation.Cancel();
        }

        public void ClearFindOnPage()
        {
            CancelFind();
            lock (findResultsLock) this.findResults.Clear();
            this.TextEditor.CancelSelect();
            this.currentSearchIndex = 0;
            this.findOptions = new FindOptions();
        }                

        public string HighlightNextMatch()
        {
            string status = "";
            lock (findResultsLock)
            {
                this.currentSearchIndex++;
                if (this.findResults.Count == 0)
                {
                    this.currentSearchIndex = 0;
                    status = ("NO MATCHES FOUND");
                    this.TextEditor.CancelSelect();
                }
                else if (this.currentSearchIndex == this.findResults.Count)
                {
                    status = ("NO MORE MATCHES");
                    this.TextEditor.CancelSelect();
                }
                else
                {
                    if (this.currentSearchIndex > this.findResults.Count)
                        this.currentSearchIndex = 0;
                    FindResult findResult = this.findResults[this.currentSearchIndex];
                    this.TextEditor.Select(findResult.beginIndex, findResult.length);
                    status = ("MATCH " + (this.currentSearchIndex + 1) + " OF " + this.findResults.Count);
                }
            }

            return status;
        }

        public string HighlightPreviousMatch()
        {
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
            if (this.findOptions.FindText != null)
            {
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
