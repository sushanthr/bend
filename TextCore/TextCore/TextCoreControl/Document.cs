using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextCoreControl
{
    internal class Document
    {
        public const int UNDEFINED_ORDINAL = int.MaxValue;
        public const int BEFOREBEGIN_ORDINAL = -1;

        public Document()
        {
            this.fileContents = "\0";
        }

        public void LoadFile(string fullFilePath)
        {
            fileContents = System.IO.File.OpenText(fullFilePath).ReadToEnd();
            fileContents += "\0";

            if (this.ContentChange != null)
            {
                this.ContentChange(UNDEFINED_ORDINAL, UNDEFINED_ORDINAL, null);
            }
        }

        public void SaveFile(string fullFilePath)
        {
            System.Diagnostics.Debug.Assert(fileContents[fileContents.Length - 1] == '\0');
            System.IO.File.WriteAllText(fullFilePath, fileContents.Remove(fileContents.Length - 1, 1));
        }

        internal char CharacterAt(int ordinal)
        {
            return fileContents[ordinal];
        }

        internal int FirstOrdinal()
        {
            return fileContents != null && fileContents.Length > 0 ? 0 : UNDEFINED_ORDINAL;
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
                if (char.IsSeparator(character) || char.IsControl(character))
                    break;
            }

            if (beginOrdinal != this.FirstOrdinal() && this.NextOrdinal(beginOrdinal) != Document.UNDEFINED_ORDINAL) 
                beginOrdinal = NextOrdinal(beginOrdinal);

            for (endOrdinal = ordinal; this.NextOrdinal(endOrdinal) != Document.UNDEFINED_ORDINAL; endOrdinal = this.NextOrdinal(endOrdinal))
            {   
                char character = this.CharacterAt(endOrdinal);
                if (char.IsSeparator(character) || char.IsControl(character))
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
    }
}