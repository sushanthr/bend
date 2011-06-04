using System;
using System.Windows;
using System.Collections;
using System.Runtime.InteropServices;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.WindowsImagingComponent;

namespace TextCoreControl
{
    class DisplayManager
    {
        internal DisplayManager(RenderHost renderHost, Document document)
        {
            // Create the DWrite Factory
            dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);

            this.renderHost = renderHost;
            renderHost.Loaded += new RoutedEventHandler(RenderHost_Loaded);
            renderHost.SizeChanged += new SizeChangedEventHandler(RenderHost_SizeChanged);
   
            this.document = document;
            document.ContentChange += this.Document_ContentChanged;
            document.OrdinalShift += this.Document_OrdinalShift;

            this.d2dFactory = D2DFactory.CreateFactory();
        }

        #region Event Handling

        void RenderHost_Loaded(object sender, RoutedEventArgs e)
        {
            // Start rendering now
            this.renderHost.Render       = Render;
            this.renderHost.MouseHandler = MouseHandler;
            this.renderHost.KeyHandler = KeyHandler;
            this.renderHost.OtherHandler = this.OtherHandler;
            this.pageBeginOrdinal   = 0;
            this.pageEndOrdinal     = Document.UNDEFINED_ORDINAL;
            this.visualLines        = new ArrayList(50);
        }

        void RenderHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (hwndRenderTarget != null)
            {
                // Resize the render target to the actual host size
                hwndRenderTarget.Resize(new SizeU((uint)(renderHost.ActualWidth), (uint)(renderHost.ActualHeight)));

                int changeStart, changeEnd;
                this.UpdateVisualLinesAndCaret(/*visualLineStartIndex*/ 0, /*forceRelayout*/ true, out changeStart, out changeEnd);
            }
        }

        void Document_ContentChanged(int beginOrdinal, int endOrdinal)
        {
            if (beginOrdinal == Document.UNDEFINED_ORDINAL)
            {
                this.pageBeginOrdinal = document.FirstOrdinal();
                this.pageEndOrdinal = Document.UNDEFINED_ORDINAL;

                int changeStart, changeEnd;
                this.UpdateVisualLinesAndCaret(/*visualLineStartIndex*/ 0, /*forceRelayout*/ false, out changeStart, out changeEnd);
            }
            else
            {
                int visualLineStartIndex = -1;
                for (int i = 0; i < visualLines.Count; i++)
                {
                    VisualLine vl = (VisualLine)visualLines[i];
                    if (vl.BeginOrdinal == Document.BEFOREBEGIN_ORDINAL ||
                        vl.BeginOrdinal <= beginOrdinal && vl.NextOrdinal >= beginOrdinal)
                    {
                        visualLines[i] = null;
                        if (visualLineStartIndex == -1)
                        {
                            visualLineStartIndex = i;
                        }
                    }
                }

                if (this.pageBeginOrdinal == Document.BEFOREBEGIN_ORDINAL)
                {
                    this.pageBeginOrdinal = this.document.FirstOrdinal();
                }

                visualLineStartIndex = (visualLineStartIndex > 0) ? visualLineStartIndex - 1 : 0;
                int changeStart, changeEnd;
                this.UpdateVisualLinesAndCaret(visualLineStartIndex, /*forceRelayout*/ false, out changeStart, out changeEnd);

                hwndRenderTarget.BeginDraw();
                this.RenderToRenderTarget(hwndRenderTarget, changeStart, changeEnd);
                hwndRenderTarget.Flush();
                hwndRenderTarget.EndDraw();
            }
        }

        void Document_OrdinalShift(int beginOrdinal, int shift)
        {
            for (int i = 0; i < visualLines.Count; i++)
            {
                VisualLine vl = (VisualLine)visualLines[i];
                vl.OrdinalShift(beginOrdinal, shift);
            }

            if (this.selectionManager != null)
            {
                this.selectionManager.OrdinalShift(beginOrdinal, shift);
            }

            if (this.caret.Ordinal >= beginOrdinal)
            {
                this.caret.MoveCaretOrdinal(this.document, shift);
            }

            if (this.pageBeginOrdinal > beginOrdinal && this.pageBeginOrdinal != Document.UNDEFINED_ORDINAL ) this.pageBeginOrdinal += shift;
            if (this.pageEndOrdinal > beginOrdinal && this.pageEndOrdinal != Document.UNDEFINED_ORDINAL ) this.pageEndOrdinal += shift;
        }

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        private void MouseHandler(int x, int y, int type, int flags)
        {
            switch (type)
            {
                case 0x0201:
                    // WM_LBUTTONDOWN
                    {
                        SetFocus(renderHost.Handle);

                        int selectionBeginOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionBeginOrdinal, out iLine))
                        {
                            VisualLine vl = (VisualLine)this.visualLines[iLine];
                            this.caret.MoveCaretVisual(vl, this.document, selectionBeginOrdinal);

                            this.hwndRenderTarget.BeginDraw();
                            this.selectionManager.ResetSelection(selectionBeginOrdinal, this.visualLines, this.document, this.hwndRenderTarget);
                            this.hwndRenderTarget.EndDraw();
                        }
                    }
                    break;
                case 0x0202:
                    // WM_LBUTTONUP

                    break;
                case 0x0205:
                    // WM_RBUTTONUP

                    break;
                case 0X0204:
                    // WM_RBUTTONDOWN
                    break;
                case 0x0200:
                    // WM_MOUSEMOVE
                    if (flags == 1)
                    {
                        // Left mouse is down.
                        int selectionEndOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionEndOrdinal, out iLine))
                        {
                            VisualLine vl = (VisualLine)this.visualLines[iLine];
                            this.caret.MoveCaretVisual(vl, this.document, selectionEndOrdinal);

                            this.hwndRenderTarget.BeginDraw();
                            this.selectionManager.ExpandSelection(selectionEndOrdinal, visualLines, document, this.hwndRenderTarget);
                            this.hwndRenderTarget.EndDraw();
                        }
                    }
                    break;
            }
        }

        private void OtherHandler(int type, int wparam, int lparam)
        {
            switch (type)
            {
                /*WM_SETFOCUS*/
                case 0x0007:
                    if (this.caret != null) this.caret.OnGetFocus();
                    break;
                /*WM_KILLFOCUS*/
                case 0x0008:
                    if (this.caret != null) this.caret.OnLostFocus();
                    break;
            }
        }

        private void KeyHandler(int wparam, int lparam)
        {
            char key = (char)wparam;

            if (key == '\b')
            {
                if (this.caret.Ordinal > document.FirstOrdinal())
                {
                    document.DeleteFrom(document.PreviousOrdinal(this.caret.Ordinal), 1);
                }
            }
            else
            {
                int insertOrdinal = this.caret.Ordinal;
                document.InsertStringAfter(insertOrdinal, key.ToString());
            }
        }

        #endregion

        void CreateDeviceResources()
        {
            // Only calls if resources have not been initialize before
            if (hwndRenderTarget == null)
            {
                // Create the render target
                SizeU size = new SizeU((uint)renderHost.ActualWidth, (uint)renderHost.ActualHeight);
                RenderTargetProperties props = new RenderTargetProperties(
                    RenderTargetType.Hardware,
                    new PixelFormat(),
                    96,
                    96,
                    RenderTargetUsages.GdiCompatible,
                    Microsoft.WindowsAPICodePack.DirectX.Direct3D.FeatureLevel.Default);
                
                HwndRenderTargetProperties hwndProps = new HwndRenderTargetProperties(renderHost.Handle, size, PresentOptions.None);
                // Create the D2D Factory
                hwndRenderTarget = this.d2dFactory.CreateHwndRenderTarget(props, hwndProps);

                // Default rendering options
                defaultForegroundBrush = hwndRenderTarget.CreateSolidColorBrush(new ColorF(0, 0, 0, 1));
                defaultBackgroundBrush = hwndRenderTarget.CreateSolidColorBrush(new ColorF(1, 1, 1, 1));
                defaultTextFormat = dwriteFactory.CreateTextFormat("Consolas", 14);
                // defaultSelectionBrush has to be solid color and not alpha
                defaultSelectionBrush = hwndRenderTarget.CreateSolidColorBrush(new ColorF(0.414f, 0.484f, 0.625f, 1.0f));
   
                this.textLayoutBuilder = new TextLayoutBuilder(defaultTextFormat, /*autoWrap*/true);
                this.selectionManager = new SelectionManager(hwndRenderTarget, this.d2dFactory);
         
                int changeStart, changeEnd;
                this.UpdateVisualLinesAndCaret(/*visualLineStartIndex*/ 0, /*forceRelayout*/ false, out changeStart, out changeEnd);
                if (this.visualLines.Count > 0)
                {
                    this.caret = new Caret(this.hwndRenderTarget, (int)((VisualLine)this.visualLines[0]).Height);
                }
                else
                {
                    this.caret = new Caret(this.hwndRenderTarget, (int)(defaultTextFormat.FontSize * 1.3f));
                }
            }
        }

        private void UpdateVisualLinesAndCaret(
            int visualLineStartIndex, 
            bool forceRelayout,
            out int changeStartIndex,
            out int changeEndIndex)
        {
            int ordinal = this.pageBeginOrdinal;
            float y;
            if (forceRelayout)
            {
                this.visualLines.Clear();
                visualLineStartIndex = 0;
                y = 0;
            }
            else
            {
                if (this.visualLines.Count > visualLineStartIndex && this.visualLines[visualLineStartIndex] != null)
                {
                    ordinal = ((VisualLine)this.visualLines[visualLineStartIndex]).BeginOrdinal;
                    y = ((VisualLine)this.visualLines[visualLineStartIndex]).Position.Y;
                }
                else
                {
                    visualLineStartIndex = 0;
                    y = 0;
                }
            }

            changeStartIndex = -1;
            changeEndIndex = -1;
            while (ordinal != Document.UNDEFINED_ORDINAL && y < renderHost.ActualHeight)
            {
                VisualLine visualLine = textLayoutBuilder.GetNextLine(this.document, ordinal, (float)renderHost.ActualWidth, out ordinal);

                visualLine.Position = new Point2F(0, y);
                y += visualLine.Height;

                changeEndIndex = visualLineStartIndex;
                if (changeStartIndex == -1)
                {
                    changeStartIndex = changeEndIndex;
                }

                if (visualLineStartIndex + 1 > this.visualLines.Count)
                {
                    this.visualLines.Add(visualLine);
                    visualLineStartIndex++;
                }
                else
                {
                    this.visualLines[visualLineStartIndex] = visualLine;
                    visualLineStartIndex++;
                    if (!forceRelayout && visualLineStartIndex < this.visualLines.Count && this.visualLines[visualLineStartIndex] != null)
                    {
                        if (visualLine.NextOrdinal == ((VisualLine)this.visualLines[visualLineStartIndex]).BeginOrdinal)
                        {
                            // We have reflowed enough, things are the same from here on.
                            break;
                        }
                    }
                }
            }

            // Remove any trailing lines.
            for (int d = changeEndIndex; d < this.visualLines.Count; d++)
            {
                if (this.visualLines[d] == null)
                {
                    // everything after this must go.
                    this.visualLines.RemoveRange(d, this.visualLines.Count - d);
                    break;
                }
            }

            // Update caret
            if (this.caret != null &&  this.caret.Ordinal != Document.UNDEFINED_ORDINAL)
            {
                for (int i = 0; i < this.visualLines.Count; i++)
                {
                    VisualLine vl = (VisualLine)this.visualLines[i];
                    if (vl.BeginOrdinal <= this.caret.Ordinal && vl.NextOrdinal > this.caret.Ordinal)
                    {
                        this.caret.MoveCaretVisual(vl, this.document, this.caret.Ordinal);
                        break;
                    }
                }
            }
        }

        private void Render()
        {
            CreateDeviceResources();
            if (hwndRenderTarget.IsOccluded)
                return;

            hwndRenderTarget.BeginDraw();
            this.RenderToRenderTarget(hwndRenderTarget, /*redrawBegin*/ 0, /*redrawEnd*/ this.visualLines.Count - 1);
            hwndRenderTarget.Flush();
            hwndRenderTarget.EndDraw();
        }

        private void RenderToRenderTarget(RenderTarget renderTarget, int redrawBegin, int redrawEnd)
        {
            RectF wipeBounds;
            if (redrawBegin == 0 && redrawEnd == visualLines.Count - 1)
            {
                renderTarget.Clear(defaultBackgroundBrush.Color);
            }
            else
            {
                VisualLine beginLine = (VisualLine)this.visualLines[redrawBegin];
                VisualLine endLine = (VisualLine)this.visualLines[redrawEnd];
                wipeBounds = new RectF(0.0f, beginLine.Position.Y, renderTarget.Size.Width, endLine.Position.Y + endLine.Height);
                renderTarget.FillRectangle(wipeBounds, defaultBackgroundBrush);
            }
            
            int selectionBeginOrdinal = this.selectionManager.GetSelectionBeginOrdinal();
            int selectionEndOrdinal = this.selectionManager.GetSelectionEndOrdinal();

            for (int i = redrawBegin; i <= redrawEnd; i++)
            {
                VisualLine visualLine = (VisualLine)this.visualLines[i];
                visualLine.Draw(renderTarget);
            }

            selectionManager.DrawSelection(
                selectionManager.GetSelectionBeginOrdinal(),
                selectionManager.GetSelectionEndOrdinal(), 
                this.visualLines, 
                this.document, 
                renderTarget);
        }

        private bool HitTest(Point2F point, out int ordinal, out int lineIndex)
        {
            for (int i = 0; i < this.visualLines.Count; i++)
            {
                VisualLine visualLine = (VisualLine)this.visualLines[i];
                if (visualLine.Position.Y < point.Y && visualLine.Position.Y + visualLine.Height > point.Y)
                {
                    point.Y -= visualLine.Position.Y;
                    uint offset;
                    visualLine.HitTest(point, out offset);
                    ordinal = document.NextOrdinal(visualLine.BeginOrdinal, (uint)offset);
                    lineIndex = i;

                    return true;
                }
            }

            ordinal = Document.UNDEFINED_ORDINAL;
            lineIndex = -1;
            return false;
        }

        #region Rasterize
        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        /// <summary>
        ///    Performs a bit-block transfer of the color data corresponding to a
        ///    rectangle of pixels from the specified source device context into
        ///    a destination device context.
        /// </summary>
        /// <param name="hdc">Handle to the destination device context.</param>
        /// <param name="nXDest">The leftmost x-coordinate of the destination rectangle (in pixels).</param>
        /// <param name="nYDest">The topmost y-coordinate of the destination rectangle (in pixels).</param>
        /// <param name="nWidth">The width of the source and destination rectangles (in pixels).</param>
        /// <param name="nHeight">The height of the source and the destination rectangles (in pixels).</param>
        /// <param name="hdcSrc">Handle to the source device context.</param>
        /// <param name="nXSrc">The leftmost x-coordinate of the source rectangle (in pixels).</param>
        /// <param name="nYSrc">The topmost y-coordinate of the source rectangle (in pixels).</param>
        /// <param name="dwRop">A raster-operation code.</param>
        /// <returns>
        ///    <c>true</c> if the operation succeeded, <c>false</c> otherwise.
        /// </returns>
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, TernaryRasterOperations dwRop);

        public enum TernaryRasterOperations : uint
        {
            SRCCOPY = 0x00CC0020,
            SRCPAINT = 0x00EE0086,
            SRCAND = 0x008800C6,
            SRCINVERT = 0x00660046,
            SRCERASE = 0x00440328,
            NOTSRCCOPY = 0x00330008,
            NOTSRCERASE = 0x001100A6,
            MERGECOPY = 0x00C000CA,
            MERGEPAINT = 0x00BB0226,
            PATCOPY = 0x00F00021,
            PATPAINT = 0x00FB0A09,
            PATINVERT = 0x005A0049,
            DSTINVERT = 0x00550009,
            BLACKNESS = 0x00000042,
            WHITENESS = 0x00FF0062
        }

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool DeleteDC(IntPtr hdc);

        public System.Windows.Media.Imaging.BitmapSource Rasterize()
        {
            IntPtr hBitmap = IntPtr.Zero;

            hwndRenderTarget.BeginDraw();

            this.RenderToRenderTarget(hwndRenderTarget, 0, this.visualLines.Count - 1);

            IntPtr windowDC = hwndRenderTarget.GdiInteropRenderTarget.GetDC(DCInitializeMode.Copy);
            IntPtr compatibleDC = CreateCompatibleDC(windowDC);

            int nWidth = (int)Math.Ceiling(hwndRenderTarget.Size.Width);
            int nHeight = (int)Math.Ceiling(hwndRenderTarget.Size.Height);
            hBitmap = CreateCompatibleBitmap(windowDC,
                nWidth,
                nHeight);

            IntPtr hOld = SelectObject(compatibleDC, hBitmap);
            //	blit bits from screen to target buffer
            BitBlt(compatibleDC, 0, 0, nWidth, nHeight, windowDC, 0, 0, TernaryRasterOperations.SRCCOPY);
            //	de-select bitmap	
            SelectObject(compatibleDC, hOld);

            //	free DCs	
            DeleteDC(compatibleDC);
            hwndRenderTarget.GdiInteropRenderTarget.ReleaseDC();

            hwndRenderTarget.EndDraw();
           
            System.Windows.Media.Imaging.BitmapSource bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                 hBitmap,
                 IntPtr.Zero,
                 Int32Rect.Empty,
                 System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()
             );

            return bmpSource;
        }
        #endregion

        DWriteFactory                dwriteFactory;
        D2DFactory                   d2dFactory;
        HwndRenderTarget             hwndRenderTarget;
        SolidColorBrush              defaultBackgroundBrush;
        SolidColorBrush              defaultForegroundBrush;
        SolidColorBrush              defaultSelectionBrush;
        TextFormat                   defaultTextFormat;
        readonly RenderHost          renderHost;
        readonly Document            document;
        TextLayoutBuilder            textLayoutBuilder;
        SelectionManager             selectionManager;
        Caret                        caret;

        int                          pageBeginOrdinal;
        int                          pageEndOrdinal;
        ArrayList                    visualLines;
    }
}
