using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextCoreControl
{
    public class Document
    {
        public const int UNDEFINED_ORDINAL = int.MaxValue;

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

        internal int NextOrdinal(int ordinal, int offset = 1)
        {
            ordinal += offset;
            if (ordinal < fileContents.Length)
                return ordinal;
            return UNDEFINED_ORDINAL;
        }

        internal int PreviousOrdinal(int ordinal, int offset = 1)
        {
            ordinal -= offset;
            if (ordinal < 0)
                return UNDEFINED_ORDINAL;
            return ordinal;
        }

        internal void InsertStringBefore(int ordinal, string content)
        {
            fileContents.Insert(ordinal, content);
        }

        internal void DeleteFrom(int ordinal, int length)
        {
            fileContents.Remove(ordinal, length);
        }

        // A delegate type for hooking up change notifications.
        public delegate void ContentChangeEventHandler(int beginOrdinal, int endOrdinal);
        public event ContentChangeEventHandler ContentChange;
        private string fileContents;
    }
}