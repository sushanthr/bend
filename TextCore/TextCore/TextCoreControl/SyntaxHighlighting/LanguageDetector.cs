using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace TextCoreControl.SyntaxHighlighting
{
    /// <summary>
    ///     Detects the current document language and picks a syntax 
    ///     definition file for hightlighting.
    /// </summary>
    internal class LanguageDetector
    {
        private static readonly string SyntaxHighlightingDirectory =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyntaxHighlighting");

        private static string GetSyntaxDefinitionPath(string syntaxFile)
        {
            return Path.Combine(SyntaxHighlightingDirectory, "Definitions", syntaxFile);
        }

        internal LanguageDetector(Document document)
        {
            this.filenameExtensionChecked = false;
            this.heuristicsEnabled = true;
            this.filenameExtensions = null;
            this.extSyntaxFileNames = null;
            this.heuristics = null;
            this.hSytaxFileNames = null;
            this.currentSyntaxDefinitionFile = null;
            this.languageDetectorWorkerThread = new BackgroundWorker();
            this.languageDetectorWorkerThread.WorkerReportsProgress = false;
            this.languageDetectorWorkerThread.WorkerSupportsCancellation = true;
            this.languageDetectorWorkerThread.DoWork += new DoWorkEventHandler(languageDetectorWorkerThread_DoWork);
            this.languageDetectorWorkerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(languageDetectorWorkerThread_RunWorkerCompleted);
            this.document = document;
            this.document.ContentChange += new Document.ContentChangeEventHandler(document_ContentChange);
            this.LanguageChange = null;

            DebugHUD.LanguageDetector = this;
        }

        #region Background worker

        void languageDetectorWorkerThread_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            e.Cancel = false;
            e.Result = null;

            // Load configuration
            if (this.filenameExtensions == null ||
                this.extSyntaxFileNames == null ||
                this.heuristics == null ||
                this.hSytaxFileNames == null)
            {
                this.LoadConfig(Path.Combine(SyntaxHighlightingDirectory, "LanguageDetector.config"));
            }

            if (worker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            // Check based on filename extension
            string fileNameExtension;
            bool fileNameExtensionChecked;
            bool heuristicsEnabled;
            string syntaxFile = null;
            lock (this)
            {
                fileNameExtension = this.currentFilenameExtension;
                fileNameExtensionChecked = this.filenameExtensionChecked;
                this.filenameExtensionChecked = true;
                heuristicsEnabled = this.heuristicsEnabled;
            }
            if (!fileNameExtensionChecked)
            {
                syntaxFile = this.GetSyntaxFileForExtension(fileNameExtension);
                if (syntaxFile != null)
                {
                    lock (this)
                    {
                        // Prevent further detection based on heuristics.
                        this.heuristicsEnabled = false;
                        heuristicsEnabled = false;
                    }
                }
            }

            // Check based on heuristics, if file extension check failed
            if (heuristicsEnabled)
            {
                // Work on an immutable snapshot; the UI thread may edit the document concurrently.
                string immutableString = document.GetTextSnapshot(1000).Replace('\r', '\n');
                if (immutableString.Length >= 100)
                {
                    // We have more than 100 characters in this document 
                    // time to stop running heuristics. But run one last time.
                    lock (this)
                    {
                        this.heuristicsEnabled = false;
                    }
                }
                syntaxFile = GetSyntaxFileUsingHeuristics(immutableString);
            }

            if (syntaxFile != null)
            {
                if (this.currentSyntaxDefinitionFile != syntaxFile)
                {
                    try
                    {
                        SyntaxHighlighterService syntaxHighlighterService;
                        if (syntaxFile == "none")
                        {
                            syntaxHighlighterService = null;
                        }
                        else
                        {
                            syntaxHighlighterService = new SyntaxHighlighterService(GetSyntaxDefinitionPath(syntaxFile), this.document);
                        }
                        this.currentSyntaxDefinitionFile = syntaxFile;
                        e.Result = syntaxHighlighterService;
                    }
                    catch (Exception exception)
                    {
                        DebugLog.Write(exception);
                    }
                }
                return;
            }
        }

        void languageDetectorWorkerThread_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                // A restart was requested.
                this.languageDetectorWorkerThread.RunWorkerAsync(this);
            }
            else if (e.Error == null && e.Result != null)
            {
                // We have detected a language change, notify the world
                SyntaxHighlighterService syntaxHighlighterService = (SyntaxHighlighterService)e.Result;
                if (this.LanguageChange != null)
                {
                    this.LanguageChange(syntaxHighlighterService);
                }
            }
        }

        #endregion

        #region Config file support

        private string GetSyntaxFileForExtension(string filenameExtension)
        {
            if (filenameExtension != null)
            {
                filenameExtension = filenameExtension.Trim();
                filenameExtension = filenameExtension.ToLower();
                if (filenameExtension.Length != 0 && extSyntaxFileNames != null)
                {
                    for (int i = 0; i < filenameExtensions.Length; i++)
                    {
                        if (filenameExtensions[i] == filenameExtension)
                        {
                            string syntaxFile = extSyntaxFileNames[i];
                            if (syntaxFile == "none" || File.Exists(GetSyntaxDefinitionPath(syntaxFile)))
                            {
                                return syntaxFile;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private string GetSyntaxFileUsingHeuristics(string documentString)
        {
            if (this.heuristics == null || this.hSytaxFileNames == null)
                return null;

            int bestMatchIndex = -1;
            int bestMatchCount = 0;
            for (int i = 0; i < this.heuristics.Length; i++)
            {
                Regex regex = this.heuristics[i];
                int matchCount = regex.Matches(documentString).Count;
                if (matchCount > bestMatchCount)
                {
                    bestMatchCount = matchCount;
                    bestMatchIndex = i;
                }
            }

            if (bestMatchIndex != -1)
            {
                string syntaxFile = this.hSytaxFileNames[bestMatchIndex];
                if (File.Exists(GetSyntaxDefinitionPath(syntaxFile)))
                {
                    return syntaxFile;
                }
            }
            return null;
        }

        private void LoadConfig(string dataFile)
        {
            try
            {
                bool isReadingExtension = false;
                bool isReadingHeuristics = false;
                ArrayList filenameExtensions = new ArrayList();
                ArrayList extSytaxFileNames = new ArrayList();
                ArrayList heuristics = new ArrayList();
                ArrayList hSytaxFileNames = new ArrayList();

                using (StreamReader fileStream = new StreamReader(dataFile))
                {
                    while (!fileStream.EndOfStream)
                    {
                        string line = fileStream.ReadLine();
                        if (line.Length == 0 || line[0] == ';')
                        {
                            continue;
                        }
                        else if (line[0] == '[')
                        {
                            isReadingExtension = (line.IndexOf("[Filename Extensions Map]") >= 0);
                            isReadingHeuristics = (line.IndexOf("[Language Hueristics Map]") >= 0);
                        }
                        else if (isReadingExtension)
                        {
                            string[] parts = line.Split('\t');
                            if (parts.Length == 2)
                            {
                                string fileExtension = parts[0].Trim().ToLowerInvariant();
                                string syntaxFileName = parts[1].Trim();
                                if (fileExtension.Length > 0 && syntaxFileName.Length > 0)
                                {
                                    filenameExtensions.Add(fileExtension);
                                    extSytaxFileNames.Add(syntaxFileName);
                                }
                            }
                            else
                                isReadingExtension = false;
                        }
                        else if (isReadingHeuristics)
                        {
                            string[] parts = line.Split('\t');
                            if (parts.Length == 2)
                            {
                                string heuristic = parts[0].Trim();
                                string hSytaxFileName = parts[1].Trim();
                                if (heuristic.Length > 0 && hSytaxFileName.Length > 0)
                                {
                                    heuristics.Add(new Regex(heuristic, RegexOptions.Multiline | RegexOptions.IgnoreCase));
                                    hSytaxFileNames.Add(hSytaxFileName);
                                }
                            }
                            else
                                isReadingHeuristics = false;
                        }
                    }
                }

                // Make the array fixed size now
                this.filenameExtensions = (string[])filenameExtensions.ToArray(typeof(string));
                this.extSyntaxFileNames = (string[])extSytaxFileNames.ToArray(typeof(string));
                System.Diagnostics.Debug.Assert(this.extSyntaxFileNames.Length == this.filenameExtensions.Length);
                this.heuristics = (Regex[])heuristics.ToArray(typeof(Regex));
                this.hSytaxFileNames = (string[])hSytaxFileNames.ToArray(typeof(string));
                System.Diagnostics.Debug.Assert(this.heuristics.Length == this.hSytaxFileNames.Length);
            }
            catch (Exception exception)
            {
                DebugLog.Write(exception);
                // Missing or malformed optional configuration disables detection safely.
                this.filenameExtensions = new string[0];
                this.extSyntaxFileNames = new string[0];
                this.heuristics = new Regex[0];
                this.hSytaxFileNames = new string[0];
            }
        }

        private string[] filenameExtensions;
        private string[] extSyntaxFileNames;
        private Regex[] heuristics;
        private string[] hSytaxFileNames;
        private string currentSyntaxDefinitionFile;
        #endregion

        #region Incoming events
        
        /// <summary>
        ///     Method to call when the file name of the document changes.
        /// </summary>
        /// <param name="newFileName">The new file name</param>
        internal void NotifyOfFileNameChange(string newFileName)
        {
            if (newFileName == null)
                return;

            string newFilenameExtension = Path.GetExtension(newFileName);
            if (newFilenameExtension.StartsWith("."))
                newFilenameExtension = newFilenameExtension.Substring(1);

            if (!String.Equals(newFilenameExtension, currentFilenameExtension, StringComparison.OrdinalIgnoreCase))
            {
                lock (this)
                {
                    currentFilenameExtension = newFilenameExtension;
                    this.filenameExtensionChecked = false;
                    this.heuristicsEnabled = true;
                }

                if (languageDetectorWorkerThread.IsBusy)
                    languageDetectorWorkerThread.CancelAsync();
                else
                    this.languageDetectorWorkerThread.RunWorkerAsync(this);
                }
        }

        /// <summary>
        ///     Handles content change on the document and triggers running hueristics again on the document.
        /// </summary>
        /// <param name="beginOrdinal"></param>
        /// <param name="endOrdinal"></param>
        /// <param name="content"></param>
        void document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (this.heuristicsEnabled && !this.languageDetectorWorkerThread.IsBusy)
            {
                this.languageDetectorWorkerThread.RunWorkerAsync(this);
            }
        }

        #endregion

        #region Outgoing events

        public delegate void LanguageChangeEventHandler(SyntaxHighlighterService syntaxHighlightingService);
        public event LanguageChangeEventHandler LanguageChange;

        #endregion

        #region Accessors
        internal string SyntaxDefinitionFile
        {
            get 
            {
                if (this.currentSyntaxDefinitionFile == null || 
                    this.currentSyntaxDefinitionFile.Length == 0)
                {
                    return "none";
                }
                return this.currentSyntaxDefinitionFile; 
            }
        }
        #endregion

        #region Member Data
        private string currentFilenameExtension;
        private bool filenameExtensionChecked;
        private bool heuristicsEnabled;

        private BackgroundWorker languageDetectorWorkerThread;
        private readonly Document document;
        #endregion
    }
}
