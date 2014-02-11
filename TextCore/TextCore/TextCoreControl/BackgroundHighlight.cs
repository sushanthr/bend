using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    internal class BackgroundHighlight
    {
        internal BackgroundHighlight(HwndRenderTarget renderTarget, D2DFactory d2dFactory)
        {
            this.backgroundHighlightBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultBackgroundHighlightColor);
            this.d2dFactory = d2dFactory;
            this.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            this.ResetBackgroundHighlight();
        }

        internal void Draw(
            List<VisualLine> visualLines,
            int firstVisibleLine,
            int lastVisibleLine,
            Document document,
            SizeF scrollOffset,
            RenderTarget renderTarget)
        {
               if (highlightEndOrdinal != Document.UNDEFINED_ORDINAL &&
                   hightlightBeginOrdinal != Document.UNDEFINED_ORDINAL)
               {
                   for (int i = firstVisibleLine; i <= lastVisibleLine; i++)
                   {
                       List<RectF> lineRectangles = visualLines[i].GetRangeRectangles(document, this.hightlightBeginOrdinal, this.highlightEndOrdinal);
                       if (lineRectangles.Count != 0)
                       {
                           List<RectF>.Enumerator enumerator = lineRectangles.GetEnumerator();
                           while (enumerator.MoveNext())
                           {
                               renderTarget.FillRectangle(enumerator.Current, backgroundHighlightBrush);
                           }
                       }                       
                   }
               }
        }

        internal void SetBackgroundHighlight(int beginOrdinal, int endOrdinal)
        {
            this.hightlightBeginOrdinal = beginOrdinal;
            this.highlightEndOrdinal = endOrdinal;
        }

        internal void ResetBackgroundHighlight()
        {
            this.hightlightBeginOrdinal = Document.UNDEFINED_ORDINAL;
            this.highlightEndOrdinal = Document.UNDEFINED_ORDINAL;
        }

        internal void NotifyOfOrdinalShift(int beginOrdinal, int shift)
        {
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.hightlightBeginOrdinal);
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.highlightEndOrdinal);
        }

          
        int hightlightBeginOrdinal;
        int highlightEndOrdinal;      

        SolidColorBrush backgroundHighlightBrush;
        D2DFactory d2dFactory;
        DWriteFactory dwriteFactory;
    }
}
