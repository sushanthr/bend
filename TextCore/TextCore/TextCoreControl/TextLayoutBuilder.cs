using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    internal class TextLayoutBuilder
    {
        internal TextLayoutBuilder()
        {
            this.glyphTable = new GlyphTable(Settings.DefaultTextFormat);
            this.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
        }

        internal VisualLine GetNextLine(Document document, int beginOrdinal, float layoutWidth, out int nextOrdinal)
        {
            // Estimate how long the line is
            string lineText = "";
            float lineWidth = 0;
            nextOrdinal = beginOrdinal;
            while (nextOrdinal != Document.UNDEFINED_ORDINAL)
            {
                char letter = document.CharacterAt(nextOrdinal);
                if (letter == '\n' || letter == '\v')
                {
                    lineText += letter;
                    nextOrdinal = document.NextOrdinal(nextOrdinal);
                    break;
                }

                if (letter == '\r')
                {
                    int tempNextOrdinal = document.NextOrdinal(nextOrdinal);
                    if (document.CharacterAt(tempNextOrdinal) != '\n' || document.NextOrdinal(tempNextOrdinal) == Document.UNDEFINED_ORDINAL)
                    {
                        // The file terminating \n gets its own line.
                        lineText += letter;
                        nextOrdinal = tempNextOrdinal;
                        break;
                    }
                }

                if (Settings.AutoWrap)
                {
                    lineWidth += glyphTable.GetCharacterWidth(letter);
                    if (lineWidth > layoutWidth)
                        break;
                }
                lineText += letter;
                nextOrdinal = document.NextOrdinal(nextOrdinal);
            }

            VisualLine textLine = new VisualLine(this.dwriteFactory, lineText, glyphTable.DefaultFormat, beginOrdinal, nextOrdinal);
            return textLine;
        }

        internal VisualLine GetPreviousLine(Document document, int nextOrdinal, float layoutWidth, out int beginOrdinal)
        {
            // Estimate how long the line is
            string lineText = "";
            float lineWidth = 0;
            bool characterSeen = false;
            beginOrdinal = nextOrdinal;
            beginOrdinal = document.PreviousOrdinal(beginOrdinal);
            while (beginOrdinal != Document.BEFOREBEGIN_ORDINAL)
            {
                char letter = document.CharacterAt(beginOrdinal);
                if ((letter == '\n' || letter == '\v') && characterSeen)
                {   
                    break;
                }

                if (letter == '\r' && lineText != "\n" && characterSeen)
                {
                    // if this is a \r\n pair need to ignore this \r
                    break;
                }

                if (Settings.AutoWrap)
                {
                    lineWidth += glyphTable.GetCharacterWidth(letter);
                    if (lineWidth > layoutWidth)
                        break;
                }

                characterSeen = true;
                lineText = (letter + lineText);
                beginOrdinal = document.PreviousOrdinal(beginOrdinal);
            }

            beginOrdinal = document.NextOrdinal(beginOrdinal);
            VisualLine textLine = new VisualLine(this.dwriteFactory, lineText, glyphTable.DefaultFormat, beginOrdinal, nextOrdinal);
            return textLine;
        }

        internal float AverageLineHeight()
        {
            VisualLine textLine = new VisualLine(this.dwriteFactory, "qM", glyphTable.DefaultFormat, Document.BEFOREBEGIN_ORDINAL, Document.UNDEFINED_ORDINAL);
            return textLine.Height;
        }

        private GlyphTable glyphTable;
        private readonly DWriteFactory dwriteFactory;
    }
}
