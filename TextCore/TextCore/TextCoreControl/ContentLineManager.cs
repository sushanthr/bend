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
        internal ContentLineManager(Document document, HwndRenderTarget renderTarget)
        {
            lineNumberBrush = renderTarget.CreateSolidColorBrush(Settings.LineNumberColor);
            document.ContentChange += new Document.ContentChangeEventHandler(document_ContentChange);

            this.cachedOrdinal = Document.UNDEFINED_ORDINAL;
            this.cachedLineNumber = 0;
            this.maxContentLine = 0;

            DebugHUD.ContentLineManager = this;
        }

        internal void DrawLineNumbers(
            int redrawBegin,
            int redrawEnd, 
            List<VisualLine> visualLines, 
            Document document, 
            RenderTarget renderTarget)
        {
            // TODO:Draw line numbers.
        }

        private void document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            // TODO: Complete this so that we update cachedOrdinal and maxLineNumber correctly.
        }

        internal int Width
        {
            // TODO: Complete this so that display manager can reserve the correct amount of space.
            get { return 0; }
        }

        /// <summary>
        ///     Count of the number of content lines that are present in the document.
        /// </summary>
        internal int MaxContentLines
        {
            set { this.maxContentLine = value; }
            get { return this.maxContentLine; }
        }

        #region Member Data

        // Cache of a known line number count.
        int cachedOrdinal;
        int cachedLineNumber;

        // Maximum number of lines in this document.
        int maxContentLine;

        SolidColorBrush lineNumberBrush;

        #endregion
    }
}
