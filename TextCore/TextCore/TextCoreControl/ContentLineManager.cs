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
    internal class ContentLineManager
    {
        const int LEFT_PADDING_PX = 15;
        const int RIGHT_ONE_PADDING_PX = 10;
        const int RIGHT_TWO_PADDING_PX = 10;
        const float VERTICAL_LINE_WIDTH_PX = 1.0f;
        const float PIXEL_SNAP_FACTOR_PX = 0.5f;

        internal ContentLineManager(Document document, HwndRenderTarget renderTarget, D2DFactory d2dFactory)
        {
            lineNumberBrush = renderTarget.CreateSolidColorBrush(Settings.LineNumberColor);
            float[] dashArray = { 0.75f, 2.25f };
            leftMarginStrokeStyle = d2dFactory.CreateStrokeStyle(new StrokeStyleProperties(CapStyle.Flat, CapStyle.Flat, CapStyle.Square, LineJoin.Round, 10.0f, DashStyle.Custom, 0.50f), dashArray);

            document.ContentChange += new Document.ContentChangeEventHandler(document_ContentChange);
            document.OrdinalShift += new Document.OrdinalShiftEventHandler(document_OrdinalShift);

            this.cachedOrdinal = Document.UNDEFINED_ORDINAL;
            this.cachedLineNumber = 0;
            this.maxContentLine = 0;

            DebugHUD.ContentLineManager = this;
        }

        #region Content change handling

        private void document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (Settings.ShowLineNumber)
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
        }

        void document_OrdinalShift(Document document, int beginOrdinal, int shift)
        {
            if (this.cachedOrdinal > beginOrdinal && this.cachedOrdinal != Document.UNDEFINED_ORDINAL) this.cachedOrdinal += shift;
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
            if (Settings.ShowLineNumber)
            {
                // Draw the vertical line
                Point2F topPoint = new Point2F(leftMargin - RIGHT_TWO_PADDING_PX + scrollOffset.Width + PIXEL_SNAP_FACTOR_PX, scrollOffset.Height);
                Point2F bottomPoint = new Point2F(topPoint.X, topPoint.Y + renderTarget.PixelSize.Height);
                renderTarget.DrawLine(topPoint, bottomPoint, lineNumberBrush, VERTICAL_LINE_WIDTH_PX, leftMarginStrokeStyle);

                if (visualLines.Count > 0)
                {
                    RectF rect = new RectF(LEFT_PADDING_PX, 0, leftMargin - LEFT_PADDING_PX, 0);
                    int maxNumberOfDigits = this.MaxNumberOfDigits;
                    bool lastLineHadHardBreak = (redrawBegin == 0);

                    int firstOrdinal = visualLines[redrawBegin].BeginOrdinal;
                    int lineNumber = this.GetLineNumber(document, firstOrdinal);

                    // Cache the line number info to speed up future request
                    this.cachedLineNumber = lineNumber;
                    this.cachedOrdinal = firstOrdinal;

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

            while (ordinal != cachedOrdinal && ordinal != Document.UNDEFINED_ORDINAL)
            {
                // Determine if ordinal is a hard break
                bool isHardBreak;
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
                isHardBreak = TextLayoutBuilder.IsHardBreakChar(firstChar, nextChar);

                if (ordinal < cachedOrdinal)
                {
                    if (isHardBreak)
                        lineNumber--;
                     ordinal = document.NextOrdinal(ordinal);
                }
                else if (ordinal > cachedOrdinal)
                {
                    if (isHardBreak)
                        lineNumber++;
                    ordinal = document.PreviousOrdinal(ordinal);
                }
            }

            return lineNumber;
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
        StrokeStyle     leftMarginStrokeStyle;

        #endregion
    }
}
