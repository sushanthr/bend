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
            this.averageLineHeight = -1;
            this.averageDigitWidth = -1;
        }

        internal void NotifyOfSettingsChange()
        {
            this.glyphTable = new GlyphTable(Settings.DefaultTextFormat);
            this.averageLineHeight = -1;
            this.averageDigitWidth = -1;
        }

        internal VisualLine GetNextLine(Document document, int beginOrdinal, float layoutWidth, out int nextOrdinal)
        {
            nextOrdinal = beginOrdinal;

            // Compute line contents
            string lineText = "";
            float autoWrapLineWidth = 0;
            bool hasHardBreak = false;

            while (nextOrdinal != Document.UNDEFINED_ORDINAL)
            {
                char letter = document.CharacterAt(nextOrdinal);

                if (letter == '\r')
                {
                    // Need to treat \r\n as one character.
                    int tempNextOrdinal = document.NextOrdinal(nextOrdinal);
                    if (tempNextOrdinal != Document.UNDEFINED_ORDINAL &&
                        document.CharacterAt(tempNextOrdinal) == '\n')
                    {
                        lineText += letter;
                        nextOrdinal = tempNextOrdinal;
                        letter = '\n';
                    }
                }

                if (letter == '\n' || letter == '\v' || letter == '\r')
                {
                    lineText += letter;
                    nextOrdinal = document.NextOrdinal(nextOrdinal);
                    hasHardBreak = true;
                    break;
                }
                else if (Settings.AutoWrap)
                {
                    if (TextLayoutBuilder.IsBreakOppertunity(letter))
                    {
                        float charWidth = glyphTable.GetCharacterWidth(letter);
                        bool mustBreak = autoWrapLineWidth + charWidth > layoutWidth;

                        // If we must break, but this is the first character on the line then 
                        // include it inorder to consume atleast one ordinal.
                        if (!mustBreak || nextOrdinal == beginOrdinal)
                        {
                            lineText += letter;
                            autoWrapLineWidth += charWidth;
                            nextOrdinal = document.NextOrdinal(nextOrdinal);
                        }

                        if (mustBreak)
                            break;
                    }
                    else
                    {
                        // form the next word
                        string nextWord = letter.ToString();
                        int tempOrdinal = document.NextOrdinal(nextOrdinal);
                        float wordWidth = glyphTable.GetCharacterWidth(letter);
                        if (autoWrapLineWidth + wordWidth > layoutWidth)
                            break;

                        bool IsFirstWord = (beginOrdinal == nextOrdinal);
                        char tempChar;
                        bool wordFitsInLine = true;

                        while (tempOrdinal != Document.UNDEFINED_ORDINAL)
                        {
                            tempChar = document.CharacterAt(tempOrdinal);
                            if (TextLayoutBuilder.IsBreakOppertunity(tempChar))
                                break;

                            float charWidth = glyphTable.GetCharacterWidth(tempChar);
                            if ((autoWrapLineWidth + wordWidth + charWidth) > layoutWidth)
                            {
                                if (!IsFirstWord)
                                {
                                    // The word will not fit in the line
                                    wordFitsInLine = false;
                                }
                                // Else let the half word live on the line

                                break;
                            }

                            nextWord += tempChar;
                            wordWidth += charWidth;
                            tempOrdinal = document.NextOrdinal(tempOrdinal);
                        }

                        if (wordFitsInLine)
                        {
                            // We have a valid word that fits in the line
                            lineText += nextWord;
                            autoWrapLineWidth += wordWidth;
                            nextOrdinal = tempOrdinal;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // We dont update line width because it only matters when AutoWrap is enabled.
                    lineText += letter;
                    nextOrdinal = document.NextOrdinal(nextOrdinal);
                }
            }

            System.Diagnostics.Debug.Assert(beginOrdinal != nextOrdinal, "Line building should have consumed atleast 1 ordinal.");
            if (nextOrdinal == Document.UNDEFINED_ORDINAL) hasHardBreak = true;
            VisualLine textLine = new VisualLine(this.dwriteFactory, lineText, glyphTable.DefaultFormat, beginOrdinal, nextOrdinal, hasHardBreak);
            return textLine;
        }

        private static bool IsBreakOppertunity(char letter)
        {
            return !Char.IsLetterOrDigit(letter) && !Char.IsPunctuation(letter);
        }

        internal List<VisualLine> GetPreviousLines(Document document, int nextOrdinal, float layoutWidth, out int beginOrdinal)
        {
            beginOrdinal = document.FirstOrdinal();
            List<VisualLine> visualLines = new List<VisualLine>();

            // Find the nearest hard break before nextOrdinal
            int firstHardBreakOrdinal = document.PreviousOrdinal(nextOrdinal, 1);
            if (firstHardBreakOrdinal != Document.BEFOREBEGIN_ORDINAL)
            {
                if (document.CharacterAt(firstHardBreakOrdinal) == '\n')
                {
                    // this could be a \r\n pair, that needs an additional skip.
                    int temp = document.PreviousOrdinal(firstHardBreakOrdinal, 1);
                    if (temp != Document.BEFOREBEGIN_ORDINAL && document.CharacterAt(temp) == '\r')
                    {
                        firstHardBreakOrdinal = temp;
                    }
                }

                // Start looking from one character earlier
                firstHardBreakOrdinal = document.PreviousOrdinal(firstHardBreakOrdinal, 1);
                while (firstHardBreakOrdinal != Document.BEFOREBEGIN_ORDINAL)
                {
                    char letter = document.CharacterAt(firstHardBreakOrdinal);
                    if (letter == '\r' || letter == '\n' || letter == '\v')
                        break;

                    firstHardBreakOrdinal = document.PreviousOrdinal(firstHardBreakOrdinal);
                }
                beginOrdinal = document.NextOrdinal(firstHardBreakOrdinal);
            }

            // Generate lines
            int tempBeginOrdinal = beginOrdinal;
            while (tempBeginOrdinal < nextOrdinal)
            {
                VisualLine vl = this.GetNextLine(document, tempBeginOrdinal, layoutWidth, out tempBeginOrdinal);
                visualLines.Add(vl);
            }

            return visualLines;
        }

        /// <summary>
        ///     Returns if a character pair constitute a hard break
        /// </summary>
        /// <param name="letter">First character</param>
        /// <param name="nextLetter">Next character (pass \0, if there are no more characters)</param>
        /// <returns>True if character pair constitute a hard break</returns>
        internal static bool IsHardBreakChar(char letter, char nextLetter)
        {
            return letter == '\n' || letter == '\v' || (letter == '\r' && nextLetter != '\n');
        }

        internal float AverageLineHeight()
        {
            if (this.averageLineHeight == -1)
            {
                VisualLine textLine = new VisualLine(this.dwriteFactory, "qM", glyphTable.DefaultFormat, Document.BEFOREBEGIN_ORDINAL, Document.UNDEFINED_ORDINAL, /*hasHardBreak*/true);
                this.averageLineHeight = textLine.Height;
            }
            return this.averageLineHeight;
        }

        internal float AverageDigitWidth()
        {
            if (this.averageDigitWidth == -1)
            {
                VisualLine textLine = new VisualLine(this.dwriteFactory, "0123456789", glyphTable.DefaultFormat, Document.BEFOREBEGIN_ORDINAL, Document.UNDEFINED_ORDINAL, /*hasHardBreak*/true);
                this.averageDigitWidth = (textLine.Width / 10);
            }
            return this.averageDigitWidth;
        }

        private GlyphTable glyphTable;
        private readonly DWriteFactory dwriteFactory;
        private float averageLineHeight;
        private float averageDigitWidth;
    }
}
