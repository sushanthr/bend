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

        public void Draw(HwndRenderTarget renderTarget, SolidColorBrush foregroundBrush)
        {
            renderTarget.DrawTextLayout(this.position, this.textLayout, foregroundBrush);
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
            int tempOrdinal = this.beginOrdinal;
            float xPos = 0;
            foreach (ClusterMetrics cm in this.textLayout.ClusterMetrics) 
            {
                if (tempOrdinal == ordinal)
                    break;

                xPos += cm.Width;
                tempOrdinal = document.NextOrdinal(tempOrdinal);
            }
            return xPos;
        }

        private Point2F position;
        private TextLayout textLayout;
        private float height;
        private int beginOrdinal;
        private int nextOrdinal;
    }
}
