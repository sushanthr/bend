using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    /// <summary>
    ///     Takes care of tracking and displaying document line numbers when enabled.
    /// </summary>
    public class ContentLineManager
    {
        const int LEFT_PADDING_PX = 5;
        const int RIGHT_ONE_PADDING_PX = 10;
        const int RIGHT_TWO_PADDING_PX = 10;
        const float VERTICAL_LINE_WIDTH_PX = 1.0f;
        const float PIXEL_SNAP_FACTOR_PX = 0.5f;

        internal ContentLineManager(Document document, HwndRenderTarget renderTarget, D2DFactory d2dFactory)
        {
            lineNumberBrush = renderTarget.CreateSolidColorBrush(Settings.LineNumberColor);
            backgroundBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultBackgroundColor);
            float[] dashArray = { 0.75f, 2.25f };
            leftMarginStrokeStyle = d2dFactory.CreateStrokeStyle(new StrokeStyleProperties(CapStyle.Flat, CapStyle.Flat, CapStyle.Square, LineJoin.Round, 10.0f, DashStyle.Custom, 0.50f), dashArray);

            document.ContentChange += new Document.ContentChangeEventHandler(document_ContentChange);
            document.OrdinalShift += new Document.OrdinalShiftEventHandler(document_OrdinalShift);

            this.cachedOrdinal = 0;
            this.cachedLineNumber = 0;
            this.maxContentLine = 0;

            DebugHUD.ContentLineManager = this;
        }

        #region Content change handling

        private void document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (beginOrdinal == Document.UNDEFINED_ORDINAL && endOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // Change affects the cachedOrdinal itself, clear cache.
                cachedOrdinal = Document.BEFOREBEGIN_ORDINAL;
                cachedLineNumber = 0;
            }
            else
            {
                int breakCount = 0;
                for (int i = 0; i < content.Length - 1; i++)
                {
                    if (TextLayoutBuilder.IsHardBreakChar(content[i], content[i + 1]))
                    {
                        breakCount++;
                    }
                }
                if (content.Length != 0)
                {
                    if (TextLayoutBuilder.IsHardBreakChar(content[content.Length - 1], '\0'))
                    {
                        breakCount++;
                    }
                }
                if (endOrdinal == Document.UNDEFINED_ORDINAL)
                {
                    breakCount++;
                }

                if (breakCount != 0)
                {
                    int lineNumberDelta;
                    if (beginOrdinal < endOrdinal)
                    {
                        // insertion
                        lineNumberDelta = breakCount;
                    }
                    else
                    {
                        lineNumberDelta = -breakCount;
                    }

                    maxContentLine += lineNumberDelta;
                    if (cachedOrdinal > endOrdinal)
                    {
                        cachedLineNumber += lineNumberDelta;
                    }
                    else if (cachedOrdinal >= beginOrdinal && cachedOrdinal <= endOrdinal)
                    {
                        // Change affects the cachedOrdinal itself, clear cache.
                        cachedOrdinal = Document.BEFOREBEGIN_ORDINAL;
                        cachedLineNumber = 0;
                    }
                }
            }
        }

        void document_OrdinalShift(Document document, int beginOrdinal, int shift)
        {
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.cachedOrdinal);
        }

        internal void NotifyOfSettingsChange()
        {
            cachedOrdinal = Document.BEFOREBEGIN_ORDINAL;
            cachedLineNumber = 0;
        }

        #endregion

        #region Render and display helpers

        internal void DrawLineNumbers(
            int redrawBegin,
            int redrawEnd, 
            List<VisualLine> visualLines, 
            Document document, 
            SizeF scrollOffset,
            int   leftMargin,
            RenderTarget renderTarget)
        {
            if (Settings.ShowLineNumber && redrawEnd >= 0)
            {
                System.Diagnostics.Debug.Assert(redrawBegin >= 0 && redrawBegin < visualLines.Count);
                System.Diagnostics.Debug.Assert(redrawEnd >= 0 && redrawEnd < visualLines.Count);

                if (visualLines.Count > 0)
                {
                    RectF rect = new RectF(LEFT_PADDING_PX + scrollOffset.Width, 0, leftMargin - LEFT_PADDING_PX + scrollOffset.Width, 0);
                    RectF rectBack = new RectF(scrollOffset.Width, 0, leftMargin - LEFT_PADDING_PX + scrollOffset.Width, 0);
                    int maxNumberOfDigits = this.MaxNumberOfDigits;
                    bool lastLineHadHardBreak = false;
                    if (redrawBegin <= 0)
                    {
                        lastLineHadHardBreak = true;
                    }
                    else
                    {
                        if (visualLines[redrawBegin - 1].Position.Y < scrollOffset.Height)
                        {
                            // redrawBegin is the first line that is visible.
                            lastLineHadHardBreak = true;
                        }
                        else if (visualLines[redrawBegin -1].HasHardBreak)
                        {
                            lastLineHadHardBreak = true;
                        }
                    }

                    int firstOrdinal = visualLines[redrawBegin].BeginOrdinal;
                    int lineNumber = this.GetLineNumber(document, firstOrdinal);

                    // Cache the line number info to speed up future request
                    this.cachedLineNumber = lineNumber;
                    this.cachedOrdinal = firstOrdinal;
                    
                    rectBack.Top    = visualLines[redrawBegin].Position.Y;
                    rectBack.Bottom = visualLines[redrawEnd].Position.Y + visualLines[redrawEnd].Height;
                    renderTarget.FillRectangle(rectBack, backgroundBrush);

                    for (int i = redrawBegin; i <= redrawEnd; i++)
                    {
                        if (lastLineHadHardBreak)
                        {
                            rect.Top = visualLines[i].Position.Y;
                            rect.Bottom = rect.Top + visualLines[i].Height;
                            int visualLineNumber = lineNumber + 1;
                            string text = visualLineNumber.ToString();
                            text = text.PadLeft(maxNumberOfDigits);
                            renderTarget.DrawText(text, Settings.DefaultTextFormat, rect, lineNumberBrush);
                        }

                        lastLineHadHardBreak = visualLines[i].HasHardBreak;
                        if (lastLineHadHardBreak) lineNumber++;
                    }
                }

                // Draw the vertical line
                Point2F topPoint = new Point2F(leftMargin - RIGHT_TWO_PADDING_PX + scrollOffset.Width + PIXEL_SNAP_FACTOR_PX, scrollOffset.Height);
                Point2F bottomPoint = new Point2F(topPoint.X, topPoint.Y + renderTarget.PixelSize.Height);
                renderTarget.DrawLine(topPoint, bottomPoint, lineNumberBrush, VERTICAL_LINE_WIDTH_PX, leftMarginStrokeStyle);
            }
        }

        internal int LayoutWidth(float averageDigitWidth)
        {
            if (Settings.ShowLineNumber)
            {
                int numberOfDigits = this.MaxNumberOfDigits;
                float width = averageDigitWidth * numberOfDigits;
                width = width + LEFT_PADDING_PX + RIGHT_ONE_PADDING_PX + RIGHT_TWO_PADDING_PX;
                return (int)System.Math.Ceiling(width);
            }
            else
            {
                return 0;
            }
        }

        private int MaxNumberOfDigits
        {
            get
            {
                int numberOfDigits;
                if (this.maxContentLine != 0)
                {
                    int a = ((int)System.Math.Floor(System.Math.Log10(this.maxContentLine)) + 1);
                    numberOfDigits = System.Math.Max(a, Settings.MinLineNumberDigits);
                }
                else
                {
                    numberOfDigits = Settings.MinLineNumberDigits;
                }
                return numberOfDigits;
            }
        }

        #endregion

        #region Other API

        internal int GetLineNumber(Document document, int ordinal)
        {
            int lineNumber = cachedLineNumber;
            int ordinalBegin;
            int ordinalEnd;
            bool addDelta;

            if (ordinal == cachedOrdinal)
            {
                return lineNumber;
            }
            else if (ordinal < cachedOrdinal)
            {
                ordinalBegin = ordinal;
                ordinalEnd = cachedOrdinal;
                addDelta = false;
            }
            else
            {
                ordinalBegin = cachedOrdinal;
                ordinalEnd = ordinal;
                addDelta = true;
            }

            if (ordinalBegin == Document.BEFOREBEGIN_ORDINAL)
                ordinalBegin = document.FirstOrdinal();

            int delta = 0;
            while (ordinalBegin != ordinalEnd && ordinalBegin != Document.UNDEFINED_ORDINAL)
            {
                if (IsHardBreakOrdinal(document, ordinalBegin))
                    delta++;
                ordinalBegin = document.NextOrdinal(ordinalBegin);
            }

            return addDelta ? lineNumber + delta : lineNumber - delta;
        }

        /// <summary>
        ///     Finds the first ordinal for a line number.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="lineNumber"></param>
        /// <returns>The first ordinal for a line number</returns>
        internal int GetBeginOrdinal(Document document, int lineNumber)
        {
            if (lineNumber == this.cachedLineNumber)
                return this.cachedOrdinal;

            bool isBackWardSearch = this.cachedLineNumber > lineNumber;
            int currentOrdinal = this.cachedOrdinal;
            int currentLineNumber = this.cachedLineNumber;
            while (currentOrdinal != Document.UNDEFINED_ORDINAL && 
                currentOrdinal != Document.BEFOREBEGIN_ORDINAL && 
                currentLineNumber != lineNumber)
            {                
                bool isLookingForward = currentLineNumber < lineNumber;
                if (IsHardBreakOrdinal(document, currentOrdinal))
                    currentLineNumber = isLookingForward ? currentLineNumber + 1 : currentLineNumber - 1;

                if (isLookingForward)
                    currentOrdinal = document.NextOrdinal(currentOrdinal);
                else
                    currentOrdinal = document.PreviousOrdinal(currentOrdinal);
            }

            if (isBackWardSearch)
            {
                // If found searching backward, then we need to move more to find the first ordinal
                while (currentOrdinal != Document.BEFOREBEGIN_ORDINAL)
                {
                    if (IsHardBreakOrdinal(document, currentOrdinal))
                    {
                        break;
                    }
                    currentOrdinal = document.PreviousOrdinal(currentOrdinal);
                }
                currentOrdinal = document.NextOrdinal(currentOrdinal);
            }

            return currentOrdinal;
        }

        private static bool IsHardBreakOrdinal(Document document, int ordinal)
        {
            // Determine if ordinal is a hard break
            char firstChar = document.CharacterAt(ordinal);
            char nextChar;
            int nextOrdinal = document.NextOrdinal(ordinal);
            if (nextOrdinal != Document.UNDEFINED_ORDINAL)
            {
                nextChar = document.CharacterAt(nextOrdinal);
            }
            else
            {
                nextChar = '\0';
            }
            return TextLayoutBuilder.IsHardBreakChar(firstChar, nextChar);
        }

        /// <summary>
        ///     Count of the number of content lines that are present in the document.
        /// </summary>
        internal int MaxContentLines
        {
            set { this.maxContentLine = value; }
            get { return this.maxContentLine; }
        }

        #endregion

        #region Member Data

        // Cache of a known line number count.
        int             cachedOrdinal;
        int             cachedLineNumber;

        // Maximum number of lines in this document.
        int             maxContentLine;

        SolidColorBrush lineNumberBrush;
        SolidColorBrush backgroundBrush;
        StrokeStyle     leftMarginStrokeStyle;
        
        #endregion
    }
}
