using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    public class VisualLine
    {
        public VisualLine(DWriteFactory dwriteFactory, string lineText, TextFormat defaultFormat, int beginOrdinal, int nextOrdinal)
        {
            textLayout = dwriteFactory.CreateTextLayout(lineText, 
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
        }

        public void Draw(RenderTarget renderTarget, SolidColorBrush foregroundBrush)
        {
            renderTarget.DrawTextLayout(this.position, this.textLayout, foregroundBrush);
        }

        public void DrawInverted(RenderTarget renderTarget, 
            Document document, 
            int selectionBeginOrdinal,
            int selectionEndOrdinal, 
            SolidColorBrush foregroundBrush,
            SolidColorBrush backgrondBrush
            )
        {
            SolidColorBrush invertedWhite = renderTarget.CreateSolidColorBrush(new ColorF(1f, 1f, 1f));
            if (this.BeginOrdinal > selectionBeginOrdinal && this.NextOrdinal < selectionEndOrdinal)
            {
                // Fully selected line.
                renderTarget.DrawTextLayout(this.position, this.textLayout, invertedWhite);
            }
            else
            {
                float xBegin = -1;
                if (this.BeginOrdinal < selectionBeginOrdinal && this.NextOrdinal > selectionBeginOrdinal)
                {
                    // line contains begin 
                    xBegin = this.CharPosition(document, selectionBeginOrdinal);
                    RectF leftRect = new RectF(this.position.X, this.position.Y, xBegin, this.position.Y + this.Height);
                    renderTarget.PushAxisAlignedClip(leftRect, AntiAliasMode.PerPrimitive);
                    renderTarget.DrawTextLayout(this.position, this.textLayout, foregroundBrush);
                    renderTarget.PopAxisAlignedClip();
                }

                float xEnd = -1;
                if (this.beginOrdinal < selectionEndOrdinal && this.nextOrdinal > selectionEndOrdinal)
                {
                    // line contains end
                    xEnd = this.CharPosition(document, selectionEndOrdinal);
                    RectF rightRect = new RectF(this.position.X + xEnd, this.position.Y, this.position.X + this.Width, this.position.Y + this.Height);
                    renderTarget.PushAxisAlignedClip(rightRect, AntiAliasMode.PerPrimitive);
                    renderTarget.DrawTextLayout(this.position, this.textLayout, foregroundBrush);
                    renderTarget.PopAxisAlignedClip();
                }

                if (xBegin == -1 && xEnd == -1)
                {
                    // This line is completely outside selection
                    renderTarget.DrawTextLayout(this.position, this.textLayout, foregroundBrush);
                }
                else
                {
                    RectF rightRect = new RectF(xBegin == -1 ? this.position.X : this.position.X + xBegin, 
                        this.position.Y, 
                        xEnd == -1 ? this.position.X + this.Width : this.position.X + xEnd,
                        this.position.Y + this.Height);
                    renderTarget.PushAxisAlignedClip(rightRect, AntiAliasMode.PerPrimitive);
              
                    renderTarget.DrawTextLayout(this.position, this.textLayout, invertedWhite);

                    renderTarget.PopAxisAlignedClip();
                }
            }
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

        public float CharPosition(Document document, int ordinal)
        {
            // get local text position.
            uint localPosition = 0;
            int tempOrdinal = this.beginOrdinal;
            while (tempOrdinal != ordinal) { localPosition++; tempOrdinal = document.NextOrdinal(tempOrdinal); }

            HitTestInfo hitTestInfo = textLayout.HitTestTextPosition(localPosition, /*isTrailingHit*/false);
            return hitTestInfo.Location.X;
        }

        private Point2F position;
        private TextLayout textLayout;
        private float height;
        private int beginOrdinal;
        private int nextOrdinal;
    }
}
