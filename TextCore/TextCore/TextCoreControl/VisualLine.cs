using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    internal class VisualLine
    {
        public VisualLine(DWriteFactory dwriteFactory, 
            ShowFormattingService showFormattingService, 
            string lineText, 
            TextFormat defaultFormat, 
            int beginOrdinal, 
            int nextOrdinal,
            bool hasHardBreak)
        {
            this.lineText = lineText;
            string displayText;
            if (Settings.ShowFormatting)
            {
                // The EOF file /0 character needs to be ignored for show formatting purposes, ignoreLastCharacter achieves this.
                displayText = showFormattingService.PrepareShowFormatting(lineText, /*ignoreLastCharacter*/nextOrdinal == Document.UNDEFINED_ORDINAL);
            }
            else
            {
                displayText = lineText;
            }

            textLayout = dwriteFactory.CreateTextLayout(displayText, 
                defaultFormat, 
                float.MaxValue, 
                float.MaxValue);

            noEffectsTextLayout = dwriteFactory.CreateTextLayout(displayText,
                defaultFormat,
                float.MaxValue,
                float.MaxValue);

            height = 0;
            foreach (LineMetrics lm in textLayout.LineMetrics)
            {
                height += lm.Height;
                break;
            }

            this.beginOrdinal = beginOrdinal;
            this.nextOrdinal = nextOrdinal;
            this.hasHardBreak = hasHardBreak;            

            if (Settings.ShowFormatting)
            {
                showFormattingService.ApplyShowFormatting(lineText, dwriteFactory, textLayout);
                showFormattingService.ApplyShowFormatting(lineText, dwriteFactory, noEffectsTextLayout);
            }
        }

        /// <summary>
        ///     Sets a color for a range of text
        /// </summary>
        /// <param name="effect">The color</param>
        /// <param name="beginOffset">Offset from the begin of the line 0 to text.length</param>
        /// <param name="length">Length of the range</param>
        public void SetDrawingEffect(Brush effect, uint beginOffset, uint length)
        {
            this.textLayout.SetDrawingEffect(effect, new TextRange(beginOffset, length));
        }

        public void Draw(SolidColorBrush defaultForegroundBrush, RenderTarget renderTarget)
        {
            renderTarget.DrawTextLayout(this.position, this.textLayout, defaultForegroundBrush, DrawTextOptions.NoSnap);            
            System.Diagnostics.Debug.Assert(Caret.DBG_CARET_IS_PREPARED_FOR_RENDER, "Caret should be hidden.");
        }
                
        public void DrawWithoutEffects(SolidColorBrush defaultForegroundBrush, RenderTarget renderTarget)
        {
            renderTarget.DrawTextLayout(this.position, this.noEffectsTextLayout, defaultForegroundBrush, DrawTextOptions.NoSnap);            
            System.Diagnostics.Debug.Assert(Caret.DBG_CARET_IS_PREPARED_FOR_RENDER, "Caret should be hidden.");
        }

        public void HitTest(Point2F position, out uint offset)
        {
            HitTestInfo hitTestInfo = this.textLayout.HitTestPoint(position);
            if ((hitTestInfo.Metrics.Left + (hitTestInfo.Metrics.Width / 2)) > position.X)
            {
                // snap left
                offset = hitTestInfo.Metrics.TextPosition;
            }
            else
            {
                // snap right
                offset = hitTestInfo.Metrics.TextPosition + hitTestInfo.Metrics.Length;
            }

            // Return offset that is before the break '\r\n' or '\n' or '\r'
            if (this.hasHardBreak)
            {
                if (offset > 0 && this.Text[(int)offset - 1] == '\n')
                    offset--;
                if (offset > 0 && this.Text[(int)offset - 1] == '\r')
                    offset--;
            }
        }

        public float CharPosition(Document document, int ordinal)
        {
            if (ordinal < this.beginOrdinal) 
                return 0;

            // get local text position.
            uint localPosition = 0;
            int tempOrdinal = this.beginOrdinal;
            while (tempOrdinal < ordinal) { localPosition++; tempOrdinal = document.NextOrdinal(tempOrdinal); }

            if (tempOrdinal >= nextOrdinal)
            {
                return this.Width;
            }
            else
            {
                HitTestInfo hitTestInfo = textLayout.HitTestTextPosition(localPosition, /*isTrailingHit*/false);
                return hitTestInfo.Location.X;
            }
        }

        public List<RectF> GetRangeRectangles(Document document, int beginOrdinal, int endOrdinal)
        {
            List<RectF> rangeRectangles = new List<RectF>();

            if (beginOrdinal < this.nextOrdinal && endOrdinal > this.beginOrdinal)
            {
                // there is some intersection between line and range, inspect further.
                uint localBegin = 0;
                int tempOrdinal = this.beginOrdinal;
                if (this.beginOrdinal < beginOrdinal)
                {   while (tempOrdinal != beginOrdinal)
                    {
                        tempOrdinal = document.NextOrdinal(tempOrdinal);
                        localBegin++;
                    }
                }

                uint localEnd = localBegin;
                while (tempOrdinal != endOrdinal && tempOrdinal != this.nextOrdinal)
                {
                    tempOrdinal = document.NextOrdinal(tempOrdinal);
                    localEnd++;
                }

                int textPosition = 0;
                float rangeWidth = 0;
                float rangeLeft = 0;
                bool rangeStarted = false; ;
                foreach (ClusterMetrics clusterMetric in textLayout.ClusterMetrics)
                {
                    if (clusterMetric.IsNewline)
                        break;

                    if (textPosition >= localBegin)
                    {
                        rangeStarted = true;
                        if (textPosition < localEnd)
                        {
                            rangeWidth += clusterMetric.Width;
                        }
                        else
                        {
                            break;
                        }
                    }

                    textPosition += clusterMetric.Length;
                    if (!rangeStarted)
                    {
                        rangeLeft += clusterMetric.Width;
                    }
                }
                rangeRectangles.Add(new RectF(this.position.X + rangeLeft, this.position.Y, this.position.X + rangeLeft + rangeWidth, this.position.Y + this.height));
            }

            return rangeRectangles;
        }

        public float Height
        {
            get { return this.height; }
        }

        public float Width
        {
            get { return this.textLayout.Metrics.Width;  }
        }

        public Point2F Position
        {
            get { return this.position; }
            set { this.position = value;}
        }

        public int BeginOrdinal
        {
            get { return this.beginOrdinal; }
        }

        public int NextOrdinal
        {
            get { return this.nextOrdinal; }
        }

        public void OrdinalShift(int shiftBeginOrdinal, int shift)
        {
            Document.AdjustOrdinalForShift(shiftBeginOrdinal, shift, ref beginOrdinal);
            Document.AdjustOrdinalForShift(shiftBeginOrdinal, shift, ref nextOrdinal);
        }

        public bool HasHardBreak { get { return this.hasHardBreak;} }

        public string Text { get { return this.lineText; } }

        public bool ContainsOrdinal(int ordinal) { return this.beginOrdinal <= ordinal && this.nextOrdinal > ordinal; }

        private Point2F position;
        private TextLayout textLayout;
        private TextLayout noEffectsTextLayout;
        string lineText;
        private float height;
        private int beginOrdinal;
        private int nextOrdinal;
        private bool hasHardBreak;
    }
}
