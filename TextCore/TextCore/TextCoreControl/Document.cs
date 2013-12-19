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
        }

        public void LoadFile(string fullFilePath)
        {
            System.IO.StreamReader streamReader = new System.IO.StreamReader(fullFilePath, System.Text.Encoding.Default, true);
            lock (this)
            {
                fileContents = new StringBuilder(streamReader.ReadToEnd() + "\0");
                streamReader.Close();
                streamReader.Dispose();
                this.LanguageDetector.NotifyOfFileNameChange(fullFilePath);
                if (this.ContentChange != null)
                {
                    this.ContentChange(UNDEFINED_ORDINAL, UNDEFINED_ORDINAL, null);
                }
                this.hasUnsavedContent = false;
            }
        }

        public void SaveFile(string fullFilePath)
        {
            System.Diagnostics.Debug.Assert(fileContents[fileContents.Length - 1] == 0, "File content must terminate with a null character.");
            this.LanguageDetector.NotifyOfFileNameChange(fullFilePath);
            fileContents.Remove(fileContents.Length - 1, 1);
            // When endcoding is not specified windows creates a UTF-8 file for text with unicode characters
            // outside ASCII range and creates ASCII files otherwise.
            System.IO.File.WriteAllText(fullFilePath, fileContents.ToString());
            fileContents.Append('\0');
            this.hasUnsavedContent = false;
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
            lock (this)
            {
                fileContents = fileContents.Insert(ordinal, content);

                if (this.OrdinalShift != null)
                {
                    this.OrdinalShift(this, ordinal, content.Length);
                }

                if (this.ContentChange != null)
                {
                    int endOrdinal = this.NextOrdinal(ordinal, (uint)content.Length);
                    this.ContentChange(ordinal, endOrdinal, content);
                }
                this.hasUnsavedContent = true;
            }
        }

        /// <summary>
        ///     Deletes "length" number of characters from index "ordinal" including "ordinal"
        /// </summary>
        /// <param name="ordinal">Ordinal to delete from</param>
        /// <param name="length">Length of string to delete< /param>
        internal void DeleteAt(int ordinal, int length)
        {
            lock (this)
            {
                System.Diagnostics.Debug.Assert(length > 0);

                // Last ordinal is reserved for \n
                if (ordinal + length < this.fileContents.Length)
                {
                    string content = fileContents.ToString(ordinal, length);

                    int endOrdinal = this.NextOrdinal(ordinal, (uint)length - 1);

                    fileContents = fileContents.Remove(ordinal, length);

                    if (this.OrdinalShift != null)
                    {
                        this.OrdinalShift(this, endOrdinal, -length);
                    }

                    if (this.ContentChange != null)
                    {
                        this.ContentChange(ordinal, ordinal, content);
                    }
                    this.hasUnsavedContent = true;
                }
            }
        }

        public static void AdjustOrdinalForShift(int shiftBeginOrdinal , int shift, ref int ordinal)
        {
            if (ordinal != Document.UNDEFINED_ORDINAL)
            {
                if (ordinal > shiftBeginOrdinal)
                    ordinal += shift;
                else if (shift < 0 && ordinal > shiftBeginOrdinal + shift)
                    ordinal = shiftBeginOrdinal + 1 + shift;
            }
        }

        internal int ReplaceAllText(string findText, string replaceText, bool matchCase, bool useRegEx)
        {
            int count = 0;
            string newFileContents = this.fileContents.ToString();
            lock (this)
            {
                int lastFindEndIndex = 0;
                StringBuilder outputString = new StringBuilder();

                if (useRegEx)
                {
                    try
                    {
                        System.Text.RegularExpressions.Regex regEx;
                        regEx = new System.Text.RegularExpressions.Regex(findText, matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        System.Text.RegularExpressions.MatchCollection matches = regEx.Matches(newFileContents);

                        for (int i = 0; i < matches.Count; i++)
                        {
                            outputString.Append(newFileContents.Substring(lastFindEndIndex, matches[i].Index - lastFindEndIndex));
                            outputString.Append(replaceText);
                            lastFindEndIndex = matches[i].Index + matches[i].Length;
                        }
                        count = matches.Count;
                    }
                    catch
                    {
                        count = 0;
                    }
                }
                else
                {
                    int currentFindIndex = 0;

                    while (true)
                    {
                        currentFindIndex = newFileContents.IndexOf(findText, currentFindIndex, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        if (currentFindIndex >= 0)
                        {
                            outputString.Append(newFileContents.Substring(lastFindEndIndex, currentFindIndex - lastFindEndIndex));
                            outputString.Append(replaceText);
                            count++;
                            currentFindIndex = currentFindIndex + findText.Length;
                            lastFindEndIndex = currentFindIndex;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (count != 0)
                {
                    outputString.Append(newFileContents.Substring(lastFindEndIndex));
                    newFileContents = outputString.ToString();
                }
            }

            if (count != 0)
            {
                this.DeleteAt(0, this.fileContents.Length - 1);
                this.InsertAt(0, newFileContents);
            }
            return count;
        }

        public string Text
        {
            get { return this.fileContents.ToString(); }
        }

        public int GetOrdinalForTextIndex(int textIndex)
        {
            return textIndex;
        }

        public bool HasUnsavedContent
        {
            get { return this.hasUnsavedContent; }
        }
        
        // A delegate type for hooking up change notifications.
        public delegate void ContentChangeEventHandler(int beginOrdinal, int endOrdinal, string content);
        public event ContentChangeEventHandler ContentChange;

        /// <summary>
        ///     Event handler raised when ordinals are shifted around
        /// </summary>
        /// <param name="document">Document object</param>
        /// <param name="beginOrdinal">All ordinals greater than beginOrdinal are shifted</param>
        /// <param name="shift">Shift amount</param>
        public delegate void OrdinalShiftEventHandler(Document document, int beginOrdinal, int shift);
        public event OrdinalShiftEventHandler OrdinalShift;

        private StringBuilder fileContents;
        internal readonly SyntaxHighlighting.LanguageDetector LanguageDetector;

        private bool hasUnsavedContent;
    }
}