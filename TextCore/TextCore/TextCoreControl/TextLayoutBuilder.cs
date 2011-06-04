using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    public class TextLayoutBuilder
    {
        internal TextLayoutBuilder(TextFormat defaultFormat, bool autoWrap)
        {
            this.glyphTable = new GlyphTable(defaultFormat);
            this.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            this.AutoWrap = autoWrap;
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
                    if (document.CharacterAt(tempNextOrdinal) != '\n')
                    {
                        lineText += letter;
                        nextOrdinal = tempNextOrdinal;
                        break;
                    }
                }

                if (AutoWrap)
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

        private GlyphTable glyphTable;
        private readonly DWriteFactory dwriteFactory;
        public bool AutoWrap;
    }
}
