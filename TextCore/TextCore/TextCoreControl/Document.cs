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
            this.fileContents = "\0";
            this.LanguageDetector = new SyntaxHighlighting.LanguageDetector(this);
        }

        public void LoadFile(string fullFilePath)
        {
            System.IO.StreamReader streamReader = new System.IO.StreamReader(fullFilePath, System.Text.Encoding.Default, true);
            fileContents = streamReader.ReadToEnd();
            fileContents += "\0";
            streamReader.Close();
            streamReader.Dispose();
            this.LanguageDetector.NotifyOfFileNameChange(fullFilePath);
            if (this.ContentChange != null)
            {
                this.ContentChange(UNDEFINED_ORDINAL, UNDEFINED_ORDINAL, null);
            }
        }

        public void SaveFile(string fullFilePath)
        {
            System.Diagnostics.Debug.Assert(fileContents[fileContents.Length - 1] == '\0');
            this.LanguageDetector.NotifyOfFileNameChange(fullFilePath);
            System.IO.File.WriteAllText(fullFilePath, fileContents.Remove(fileContents.Length - 1, 1), System.Text.Encoding.Default);
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

        internal int NextOrdinal(int ordinal, uint offset = 1)
        {
            ordinal += (int)offset;
            if (ordinal < fileContents.Length)
                return ordinal;
            return UNDEFINED_ORDINAL;
        }

        internal int PreviousOrdinal(int ordinal, uint offset = 1)
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
        }

        /// <summary>
        ///     Deletes "length" number of characters from index "ordinal" including "ordinal"
        /// </summary>
        /// <param name="ordinal">Ordinal to delete from</param>
        /// <param name="length">Length of string to delete< /param>
        internal void DeleteAt(int ordinal, int length)
        {
            System.Diagnostics.Debug.Assert(length > 0);

            // Last ordinal is reserved for \n
            if (ordinal + length < this.fileContents.Length)
            {
                string content = fileContents.Substring(ordinal, length);

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

        public int ReplaceText(string findText, string replaceText, bool matchCase, bool useRegEx)
        {
            int count = 0;
            if (useRegEx)
            {
                try
                {
                    System.Text.RegularExpressions.Regex regEx;
                    regEx = new System.Text.RegularExpressions.Regex(findText, matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    System.Text.RegularExpressions.MatchCollection matches = regEx.Matches(this.fileContents);

                    int delta = 0;
                    for (int i = 0; i < matches.Count; i++)
                    {
                        int findIndex = matches[i].Index + delta;
                        this.fileContents = this.fileContents.Remove(findIndex, matches[i].Length);
                        this.fileContents = this.fileContents.Insert(findIndex, replaceText);
                        delta += (replaceText.Length - matches[i].Length);
                    }
                    count = matches.Count;
                }
                catch
                {
                    count =  0;
                }
            }
            else
            {
                int findIndex = 0;
                while (true)
                {
                    findIndex = this.fileContents.IndexOf(findText, findIndex, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    if (findIndex >= 0)
                    {
                        this.fileContents.Remove(findIndex, findText.Length);
                        this.fileContents.Insert(findIndex, replaceText);
                        findIndex += replaceText.Length;
                        count++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
                        
            return count;
        }

        public string Text
        {
            get { return this.fileContents; }
        }

        public int GetOrdinalForTextIndex(int textIndex)
        {
            return textIndex;
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

        private string fileContents;
        internal readonly SyntaxHighlighting.LanguageDetector LanguageDetector;
    }
}