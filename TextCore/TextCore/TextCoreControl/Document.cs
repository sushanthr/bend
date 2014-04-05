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
            System.IO.StreamReader streamReader = new System.IO.StreamReader(fullFilePath, System.Text.Encoding.Default, true);
            lock (this)
            {
                fileContents = new StringBuilder(streamReader.ReadToEnd() + "\0");
                this.currentEncoding = streamReader.CurrentEncoding;
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
            System.IO.File.WriteAllText(fullFilePath, fileContents.ToString(), this.currentEncoding);
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
                if (this.PreContentChange != null)
                {
                    this.PreContentChange(this.PreviousOrdinal(ordinal), this.NextOrdinal(ordinal));
                }

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

                // Last ordinal is reserved for \0
                if (ordinal + length < this.fileContents.Length)
                {
                    string content = fileContents.ToString(ordinal, length);

                    int endOrdinal = this.NextOrdinal(ordinal, (uint)length);

                    if (this.PreContentChange != null)
                    {
                        this.PreContentChange(this.PreviousOrdinal(ordinal), this.NextOrdinal(endOrdinal));
                    }

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
                if (ordinal >= shiftBeginOrdinal)
                    ordinal += shift;
                else if (shift < 0 && ordinal > shiftBeginOrdinal + shift)
                    ordinal = shiftBeginOrdinal + shift;
            }
        }

        internal int ReplaceAllText(string findText, string replaceText, bool matchCase, bool useRegEx)
        {
            int count = 0;
            string newFileContents = this.fileContents.ToString(0, this.fileContents.Length - 1);
            
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
                        if (replaceText !=  String.Empty && count != 0)
                        {
                            newFileContents = regEx.Replace(newFileContents, replaceText);
                        }
                    }
                    catch
                    {
                        count = 0;
                    }
                }
                else
                {
                    int startIndex = -1;
                    do
                    {
                        startIndex = newFileContents.IndexOf(findText, startIndex + 1);
                        count++;
                    } 
                    while (startIndex >= 0);
                    newFileContents = newFileContents.Replace(findText, replaceText);                    
                }               
            }

            if (count != 0)
            {
                this.DeleteAt(0, this.fileContents.Length -1);
                this.InsertAt(0, newFileContents);
            }
            return count;
        }

        internal void ReplaceWithRegexAtOrdinal(string findText, string replaceText, bool matchCase, int beginOrdinal)
        {
            int count = 0;
            string newFileContents = this.fileContents.ToString();
            lock (this)
            {  
                try
                {
                    System.Text.RegularExpressions.Regex regEx;
                    regEx = new System.Text.RegularExpressions.Regex(findText, matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    System.Text.RegularExpressions.MatchCollection matches = regEx.Matches(newFileContents, beginOrdinal);
                    count = matches.Count;
                    if (replaceText != String.Empty && count != 0)
                    {
                        int oldLength = newFileContents.Length;
                        int matchLength = matches[0].Length;
                        newFileContents = regEx.Replace(newFileContents, replaceText, 1, beginOrdinal);
                        int newLength = newFileContents.Length;
                        this.DeleteAt(beginOrdinal, matchLength);
                        this.InsertAt(beginOrdinal, newFileContents.Substring(beginOrdinal, matchLength + (newLength - oldLength)));
                    }
                }
                catch
                {

                }               
            }
        }

        public string Text
        {
            get { return this.fileContents.ToString(); }
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
            set { this.hasUnsavedContent = true; }
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