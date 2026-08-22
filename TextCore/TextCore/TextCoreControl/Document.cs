using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextCoreControl
{
    public class Document
    {
        public const int UNDEFINED_ORDINAL = int.MaxValue;
        public const int BEFOREBEGIN_ORDINAL = -1;

        public Document()
        {
            this.fileContents = new StringBuilder("\0");
            this.LanguageDetector = new SyntaxHighlighting.LanguageDetector(this);
            this.hasUnsavedContent = false;
            this.currentEncoding = Encoding.ASCII;
        }

        public void LoadFile(string fullFilePath)
        {
            string contents;
            Encoding detectedEncoding;
            using (System.IO.StreamReader streamReader = new System.IO.StreamReader(fullFilePath, System.Text.Encoding.Default, true))
            {
                contents = streamReader.ReadToEnd();
                detectedEncoding = streamReader.CurrentEncoding;
            }

            lock (this)
            {
                fileContents = new StringBuilder(contents + "\0");
                this.currentEncoding = detectedEncoding;
                this.hasUnsavedContent = false;
            }
            this.LanguageDetector.NotifyOfFileNameChange(fullFilePath);
            ContentChangeEventHandler contentChange = this.ContentChange;
            if (contentChange != null)
                contentChange(UNDEFINED_ORDINAL, UNDEFINED_ORDINAL, null);
        }

        public void SaveFile(string fullFilePath)
        {
            lock (this)
            {
                if (fileContents == null || fileContents.Length == 0 || fileContents[fileContents.Length - 1] != '\0')
                    throw new InvalidOperationException("Document content is not correctly terminated.");

                // Do not temporarily remove the sentinel: a failed write must leave the document usable.
                string contents = fileContents.ToString(0, fileContents.Length - 1);
                string directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(fullFilePath));
                string temporaryPath = System.IO.Path.Combine(directory, "." + System.IO.Path.GetFileName(fullFilePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new System.IO.FileStream(temporaryPath, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
                    using (var writer = new System.IO.StreamWriter(stream, this.currentEncoding))
                    {
                        writer.Write(contents);
                        writer.Flush();
                        stream.Flush(true);
                    }
                    if (System.IO.File.Exists(fullFilePath))
                        System.IO.File.Replace(temporaryPath, fullFilePath, null);
                    else
                        System.IO.File.Move(temporaryPath, fullFilePath);
                }
                finally
                {
                    if (System.IO.File.Exists(temporaryPath))
                    {
                        try { System.IO.File.Delete(temporaryPath); }
                        catch (System.IO.IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                this.hasUnsavedContent = false;
            }
            this.LanguageDetector.NotifyOfFileNameChange(fullFilePath);
        }

        internal char CharacterAt(int ordinal)
        {
            return fileContents[ordinal];
        }

        internal int FirstOrdinal()
        {
            return fileContents != null && fileContents.Length > 0 ? 0 : UNDEFINED_ORDINAL;
        }

        public bool IsEmpty
        {
            get { return fileContents == null || fileContents.Length == 0 || (fileContents[0] == '\0' && fileContents.Length == 1); }
        }
        
        internal int LastOrdinal()
        {
            return fileContents != null && fileContents.Length > 0 ? fileContents.Length - 1 : UNDEFINED_ORDINAL;
        }

        public int NextOrdinal(int ordinal, uint offset = 1)
        {
            ordinal += (int)offset;
            if (ordinal < fileContents.Length)
                return ordinal;
            return UNDEFINED_ORDINAL;
        }

        public int PreviousOrdinal(int ordinal, uint offset = 1)
        {
            if (ordinal == Document.UNDEFINED_ORDINAL)
                return Document.UNDEFINED_ORDINAL;

            ordinal -= (int)offset;
            if (ordinal < 0)
                return BEFOREBEGIN_ORDINAL;
         
            return ordinal;
        }

        internal void GetWordBoundary(int ordinal, out int beginOrdinal, out int endOrdinal)
        {
            for (beginOrdinal = ordinal; beginOrdinal > this.FirstOrdinal(); beginOrdinal = this.PreviousOrdinal(beginOrdinal))
            {
                char character = this.CharacterAt(beginOrdinal);
                if (!char.IsLetterOrDigit(character))
                    break;
            }

            if (beginOrdinal != this.FirstOrdinal() && this.NextOrdinal(beginOrdinal) != Document.UNDEFINED_ORDINAL) 
                beginOrdinal = NextOrdinal(beginOrdinal);

            for (endOrdinal = ordinal; this.NextOrdinal(endOrdinal) != Document.UNDEFINED_ORDINAL; endOrdinal = this.NextOrdinal(endOrdinal))
            {   
                char character = this.CharacterAt(endOrdinal);
                if (!char.IsLetterOrDigit(character))
                    break;
            }
        }

        /// <summary>
        ///      Inserts a string into the document
        /// </summary>
        /// <param name="ordinal">
        ///     Content ordinal to insert at. For example text: "0123" insert at 2 text t will result in 01t23. 
        ///     The Caret would have been at shown to the left of 2, since caret is always drawn to the left of
        ///     and index.
        ///  </param>
        /// <param name="content">String to insert</param>
        internal void InsertAt(int ordinal, string content)
        {
            if (content == null)
                throw new ArgumentNullException("content");
            if (content.Length == 0)
                return;

            int previousOrdinal;
            int followingOrdinal;
            lock (this)
            {
                if (ordinal < 0 || ordinal >= fileContents.Length)
                    throw new ArgumentOutOfRangeException("ordinal");
                previousOrdinal = this.PreviousOrdinal(ordinal);
                followingOrdinal = this.NextOrdinal(ordinal);
            }

            PreContentChangeEventHandler preContentChange = this.PreContentChange;
            if (preContentChange != null)
                preContentChange(previousOrdinal, followingOrdinal);

            int endOrdinal;
            lock (this)
            {
                fileContents = fileContents.Insert(ordinal, content);
                endOrdinal = this.NextOrdinal(ordinal, (uint)content.Length);
                this.hasUnsavedContent = true;
            }

            OrdinalShiftEventHandler ordinalShift = this.OrdinalShift;
            if (ordinalShift != null)
                ordinalShift(this, ordinal, content.Length);
            ContentChangeEventHandler contentChange = this.ContentChange;
            if (contentChange != null)
                contentChange(ordinal, endOrdinal, content);
        }

        /// <summary>
        ///     Deletes "length" number of characters from index "ordinal" including "ordinal"
        /// </summary>
        /// <param name="ordinal">Ordinal to delete from</param>
        /// <param name="length">Length of string to delete< /param>
        internal void DeleteAt(int ordinal, int length)
        {
            if (length <= 0)
                return;

            string content;
            int endOrdinal;
            int previousOrdinal;
            int followingOrdinal;
            lock (this)
            {
                if (ordinal < 0 || ordinal >= this.fileContents.Length)
                    throw new ArgumentOutOfRangeException("ordinal");
                if (length > this.fileContents.Length - ordinal - 1)
                    throw new ArgumentOutOfRangeException("length", "The document terminator cannot be deleted.");

                content = fileContents.ToString(ordinal, length);
                endOrdinal = this.NextOrdinal(ordinal, (uint)length);
                previousOrdinal = this.PreviousOrdinal(ordinal);
                followingOrdinal = this.NextOrdinal(endOrdinal);
            }

            PreContentChangeEventHandler preContentChange = this.PreContentChange;
            if (preContentChange != null)
                preContentChange(previousOrdinal, followingOrdinal);

            lock (this)
            {
                fileContents = fileContents.Remove(ordinal, length);
                this.hasUnsavedContent = true;
            }

            OrdinalShiftEventHandler ordinalShift = this.OrdinalShift;
            if (ordinalShift != null)
                ordinalShift(this, endOrdinal, -length);
            ContentChangeEventHandler contentChange = this.ContentChange;
            if (contentChange != null)
                contentChange(ordinal, ordinal, content);
        }

        public static void AdjustOrdinalForShift(int shiftBeginOrdinal , int shift, ref int ordinal)
        {
            if (ordinal != Document.UNDEFINED_ORDINAL)
            {
                if (ordinal >= shiftBeginOrdinal)
                    ordinal += shift;
                else if (shift < 0 && ordinal > shiftBeginOrdinal + shift)
                    ordinal = shiftBeginOrdinal + shift;
            }
        }

        internal int ReplaceAllText(string findText, string replaceText, bool matchCase, bool useRegEx, int beginOrdinal, int endOrdinal)
        {
            if (String.IsNullOrEmpty(findText))
                return 0;
            if (replaceText == null)
                replaceText = String.Empty;

            int count = 0;
            string newFileContents;
            int replaceLength = 0;
            if (beginOrdinal == UNDEFINED_ORDINAL)
            {
                replaceLength = this.fileContents.Length - 1;
                newFileContents = this.fileContents.ToString(0, replaceLength);
            }
            else
            {
                newFileContents = this.fileContents.ToString(beginOrdinal, endOrdinal - beginOrdinal);
                // replacedLength needs to be computed upfront since the lenght of newFileContent will
                // change after doing the replacement operation.
                replaceLength = newFileContents.Length;
            }
            
            lock (this)
            {
                if (useRegEx)
                {
                    try
                    {
                        System.Text.RegularExpressions.Regex regEx;
                        regEx = new System.Text.RegularExpressions.Regex(findText, matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        System.Text.RegularExpressions.MatchCollection matches = regEx.Matches(newFileContents);
                        count = matches.Count;
                        if (count != 0)
                        {
                            newFileContents = regEx.Replace(newFileContents, replaceText);
                        }
                    }
                    catch (ArgumentException exception)
                    {
                        DebugLog.Write(exception);
                        count = 0;
                    }
                }
                else
                {
                    if (matchCase)
                    {
                        int startIndex = -1;
                        while ((startIndex = newFileContents.IndexOf(findText, startIndex + 1, StringComparison.Ordinal)) >= 0)
                        {
                            count++;
                        }
                        newFileContents = newFileContents.Replace(findText, replaceText);
                    }
                    else
                    {
                        // Ignore case and replace string.
                        int startIndex = newFileContents.Length - 1;
                        StringBuilder tempString = new StringBuilder(newFileContents);
                        do
                        {
                            startIndex = newFileContents.LastIndexOf(findText, startIndex, StringComparison.OrdinalIgnoreCase);
                            if (startIndex >= 0)
                            {
                                tempString.Remove(startIndex, findText.Length);
                                tempString.Insert(startIndex, replaceText);
                                count++;
                                startIndex--;
                            }
                            else
                            {
                                break;
                            }
                        }
                        while (true);
                        newFileContents = tempString.ToString();
                    }
                }               
            }

            if (count != 0)
            {
                int insertIndex = (beginOrdinal == UNDEFINED_ORDINAL ? 0 : beginOrdinal);
                this.DeleteAt(insertIndex, replaceLength);
                this.InsertAt(insertIndex, newFileContents);
            }
            return count;
        }

        internal void ReplaceWithRegexAtOrdinal(string findText, string replaceText, bool matchCase, int beginOrdinal)
        {
            if (String.IsNullOrEmpty(findText))
                return;
            if (replaceText == null)
                replaceText = String.Empty;

            lock (this)
            {
                try
                {
                    string currentText = this.fileContents.ToString(0, this.fileContents.Length - 1);
                    if (beginOrdinal < 0 || beginOrdinal > currentText.Length)
                        return;

                    System.Text.RegularExpressions.Regex regEx = new System.Text.RegularExpressions.Regex(
                        findText,
                        matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    System.Text.RegularExpressions.Match match = regEx.Match(currentText, beginOrdinal);
                    if (match.Success)
                    {
                        string replacement = match.Result(replaceText);
                        if (match.Length > 0)
                            this.DeleteAt(match.Index, match.Length);
                        if (replacement.Length > 0)
                            this.InsertAt(match.Index, replacement);
                    }
                }
                catch (ArgumentException exception)
                {
                    DebugLog.Write(exception);
                }
            }
        }

        public string Text
        {
            get
            {
                lock (this)
                {
                    return this.fileContents.ToString();
                }
            }
        }

        internal string GetTextSnapshot(int maximumLength)
        {
            lock (this)
            {
                int contentLength = Math.Max(0, this.fileContents.Length - 1);
                int length = Math.Min(contentLength, Math.Max(0, maximumLength));
                return this.fileContents.ToString(0, length);
            }
        }

        public int GetOrdinalForTextIndex(int textIndex)
        {
            return textIndex;
        }

        public int GetOrdinalCharacterDelta(int beginOrdinal, int endOrdinal)
        {
            return endOrdinal - beginOrdinal;
        }

        public bool HasUnsavedContent
        {
            get { return this.hasUnsavedContent; }
            set { this.hasUnsavedContent = value; }
        }

        public Encoding CurrentEncoding
        {
            get { return this.currentEncoding; }
            set { this.currentEncoding = value; }
        }
        
        // A delegate type for hooking up change notifications.
        public delegate void ContentChangeEventHandler(int beginOrdinal, int endOrdinal, string content);
        public event ContentChangeEventHandler ContentChange;

        // A delegate type for hooking up change notifications. 
        // All ordinals greater than or equal to endOrdinal will be unaffected by the actual content change.
        // All ordinals less than or equal to beginOrdinal will be unaffected by the actual content change.
        public delegate void PreContentChangeEventHandler(int beginOrdinal, int endOrdinal);
        public event PreContentChangeEventHandler PreContentChange;

        /// <summary>
        ///     Event handler raised when ordinals are shifted around
        /// </summary>
        /// <param name="document">Document object</param>
        /// <param name="beginOrdinal">All ordinals greater than or equal to beginOrdinal are shifted</param>
        /// <param name="shift">Shift amount</param>
        public delegate void OrdinalShiftEventHandler(Document document, int beginOrdinal, int shift);
        public event OrdinalShiftEventHandler OrdinalShift;

        private StringBuilder fileContents;
        internal readonly SyntaxHighlighting.LanguageDetector LanguageDetector;

        private bool hasUnsavedContent;
        private Encoding currentEncoding;
    }
}
