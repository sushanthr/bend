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
            bool hasHardBreak = false;
            while (nextOrdinal != Document.UNDEFINED_ORDINAL)
            {
                char letter = document.CharacterAt(nextOrdinal);
                if (letter == '\n' || letter == '\v')
                {
                    lineText += letter;
                    nextOrdinal = document.NextOrdinal(nextOrdinal);
                    hasHardBreak = true;
                    break;
                }
                else if (letter == '\r')
                {
                    int tempNextOrdinal = document.NextOrdinal(nextOrdinal);
                    if (document.CharacterAt(tempNextOrdinal) != '\n')
                    {
                        lineText += letter;
                        nextOrdinal = tempNextOrdinal;
                        hasHardBreak = true;
                        break;
                    }
                    else
                    {
                        // /r /n combo
                        lineText += letter;
                        lineText += '\n';
                        nextOrdinal = document.NextOrdinal(tempNextOrdinal);
                        hasHardBreak = true;
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

            VisualLine textLine = new VisualLine(this.dwriteFactory, lineText, glyphTable.DefaultFormat, beginOrdinal, nextOrdinal, hasHardBreak);
            return textLine;
        }

        internal List<VisualLine> GetPreviousLines(Document document, int nextOrdinal, float layoutWidth, out int beginOrdinal)
        {
            beginOrdinal = Document.BEFOREBEGIN_ORDINAL;
            List<VisualLine> visualLines = new List<VisualLine>();

            // Find the nearest hard break before nextOrdinal
            int firstHardBreakOrdinal = document.PreviousOrdinal(nextOrdinal, 3);
            while (firstHardBreakOrdinal != Document.BEFOREBEGIN_ORDINAL)
            {
                char letter = document.CharacterAt(firstHardBreakOrdinal);
                if (letter == '\r' || letter == '\n' || letter == '\v')
                    break;

                firstHardBreakOrdinal = document.PreviousOrdinal(firstHardBreakOrdinal);
            }
            beginOrdinal = document.NextOrdinal(firstHardBreakOrdinal);

            // Generate lines
            int tempBeginOrdinal = beginOrdinal;
            while (tempBeginOrdinal < nextOrdinal)
            {
                VisualLine vl = this.GetNextLine(document, tempBeginOrdinal, layoutWidth, out tempBeginOrdinal);
                visualLines.Add(vl);
            }

            return visualLines;
        }

        internal float AverageLineHeight()
        {
            VisualLine textLine = new VisualLine(this.dwriteFactory, "qM", glyphTable.DefaultFormat, Document.BEFOREBEGIN_ORDINAL, Document.UNDEFINED_ORDINAL, /*hasHardBreak*/true);
            return textLine.Height;
        }

        private GlyphTable glyphTable;
        private readonly DWriteFactory dwriteFactory;
    }
}
