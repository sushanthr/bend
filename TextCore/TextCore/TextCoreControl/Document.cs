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
            this.fileContents = "\n";
        }

        public void LoadFile(string fullFilePath)
        {
            fileContents = System.IO.File.OpenText(fullFilePath).ReadToEnd();
            if (this.ContentChange != null)
            {
                this.ContentChange(UNDEFINED_ORDINAL, UNDEFINED_ORDINAL);
            }
        }

        internal char CharacterAt(int ordinal)
        {
            return fileContents[ordinal];
        }

        internal int FirstOrdinal()
        {
            return fileContents != null && fileContents.Length > 0 ? 0 : UNDEFINED_ORDINAL;
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

        internal void InsertStringAfter(int ordinal, string content)
        {
            fileContents = fileContents.Insert(ordinal, content);
            if (this.OrdinalShift != null)
            {
                this.OrdinalShift(ordinal, content.Length);
            }

            if (this.ContentChange != null)
            {
                int endOrdinal = this.NextOrdinal(ordinal, (uint)content.Length);
                this.ContentChange(ordinal, endOrdinal);
            }
        }

        internal void DeleteFrom(int ordinal, int length)
        {
            fileContents = fileContents.Remove(ordinal, length);

            ordinal = this.PreviousOrdinal(ordinal);
            if (this.OrdinalShift != null)
            {
                this.OrdinalShift(ordinal, - length);
            }

            if (this.ContentChange != null)
            {
                int endOrdinal = this.NextOrdinal(ordinal);
                this.ContentChange(ordinal, endOrdinal);
            }
        }

        // A delegate type for hooking up change notifications.
        public delegate void ContentChangeEventHandler(int beginOrdinal, int endOrdinal);
        public event ContentChangeEventHandler ContentChange;

        public delegate void OrdinalShiftEventHandler(int beginOrdinal, int shift);
        public event OrdinalShiftEventHandler OrdinalShift;

        private string fileContents;
    }
}