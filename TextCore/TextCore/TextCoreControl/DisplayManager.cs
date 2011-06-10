using System;
using System.Windows;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;

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
            this.renderHost = renderHost;
            renderHost.Loaded += new RoutedEventHandler(RenderHost_Loaded);
            renderHost.SizeChanged += new SizeChangedEventHandler(RenderHost_SizeChanged);
            renderHost.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(renderHost_PreviewKeyDown);
   
            this.document = document;
            document.ContentChange += this.Document_ContentChanged;
            document.OrdinalShift += this.Document_OrdinalShift;

            scrollOffset = new SizeF();
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

                this.caret.HideCaret();
                hwndRenderTarget.BeginDraw();
                this.RenderToRenderTarget(hwndRenderTarget, changeStart, changeEnd);
                hwndRenderTarget.Flush();
                hwndRenderTarget.EndDraw();
                this.caret.ShowCaret();
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
        }

        /// <summary>
        ///    Handles all the special keys
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void renderHost_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.Left:
                    if (this.caret.Ordinal > this.document.FirstOrdinal())
                    {
                        this.caret.MoveCaretOrdinal(this.document, -1);
                        this.UpdateCaret();
                    }
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Right:
                    if (this.document.NextOrdinal(this.caret.Ordinal) != Document.UNDEFINED_ORDINAL)
                    {
                        this.caret.MoveCaretOrdinal(this.document, 1);
                        this.UpdateCaret();
                    }
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Up:
                    e.Handled = this.caret.MoveCaretVertical(this.visualLines, document, scrollOffset, /*moveUp*/true, /*moveDown*/false);
                    break;
                case System.Windows.Input.Key.Down:
                    e.Handled = this.caret.MoveCaretVertical(this.visualLines, document, scrollOffset, /*moveUp*/false, /*moveDown*/true);
                    break;
            }
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
                            this.caret.HideCaret();
                            this.caret.MoveCaretVisual(vl, this.document, scrollOffset, selectionBeginOrdinal);

                            this.hwndRenderTarget.BeginDraw();
                            this.selectionManager.ResetSelection(selectionBeginOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                            this.hwndRenderTarget.EndDraw();
                            this.caret.ShowCaret();
                        }
                    }
                    break;
                case 0x0202:
                    // WM_LBUTTONUP

                    break;
                case 0x0203:
                    // WM_LBUTTONDBLCLK                0x0203
                    {
                        int selectionBeginOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionBeginOrdinal, out iLine))
                        {
                            VisualLine vl = (VisualLine)this.visualLines[iLine];
                            this.caret.HideCaret();
                            this.caret.MoveCaretVisual(vl, this.document, scrollOffset, selectionBeginOrdinal);

                            int beginOrdinal, endOrdinal;
                            this.document.GetWordBoundary(selectionBeginOrdinal, out beginOrdinal, out endOrdinal);

                            this.hwndRenderTarget.BeginDraw();
                            this.selectionManager.ResetSelection(beginOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                            this.selectionManager.ExpandSelection(endOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                            this.hwndRenderTarget.EndDraw();
                            this.caret.ShowCaret();
                        }
                    }
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
                            this.caret.HideCaret();
                            this.caret.MoveCaretVisual(vl, this.document, scrollOffset, selectionEndOrdinal);

                            this.hwndRenderTarget.BeginDraw();
                            this.selectionManager.ExpandSelection(selectionEndOrdinal, visualLines, document, this.scrollOffset, this.hwndRenderTarget);
                            this.hwndRenderTarget.EndDraw();
                            this.caret.ShowCaret();
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

        public void vScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            scrollOffset.Height = (float)e.NewValue * ((VisualLine)visualLines[0]).Height;

            // Remove lines going offscreen
            for (int j = 0; j < visualLines.Count; j++)
            {
                VisualLine vl = ((VisualLine)visualLines[j]);
                float lineBottom = vl.Position.Y + vl.Height - scrollOffset.Height;
                if (lineBottom > 0)
                {
                    if (j > 0)
                    {
                        this.visualLines.RemoveRange(0, j);
                    }
                    break;
                }
            }

            // add lines coming in at the top.
            int nextOrdinal = ((VisualLine)this.visualLines[0]).BeginOrdinal;
            double yBottom = ((VisualLine)this.visualLines[0]).Position.Y;

            for (int j = 0; j < visualLines.Count; j++)
            {
                VisualLine vl = ((VisualLine)visualLines[j]);
                float lineTop = vl.Position.Y - scrollOffset.Height;
                if (lineTop > this.renderHost.ActualHeight)
                {
                    this.visualLines.RemoveRange(j, visualLines.Count - j);
                    break;
                }
            }

            if (nextOrdinal > document.FirstOrdinal())
            {
                while (yBottom > scrollOffset.Height)
                {
                    VisualLine vl = this.textLayoutBuilder.GetPreviousLine(this.document, nextOrdinal, (float)renderHost.ActualWidth, out nextOrdinal);
                    yBottom -= vl.Height;
                    vl.Position = new Point2F(0, (float)yBottom);
                    this.visualLines.Insert(0, vl);
                }
            }

            if (visualLines.Count > 0)
            {
                this.pageBeginOrdinal = ((VisualLine)this.visualLines[0]).BeginOrdinal;
            }
            else
            {
                this.pageBeginOrdinal = document.FirstOrdinal();
            }

            int changeStartIndex;
            int changeEndIndex;
            UpdateVisualLinesAndCaret(/*visualLineStartIndex*/0,/*forceRelayout*/false, out changeStartIndex, out changeEndIndex);


            if (this.scrollOffset.Height != 0 || this.scrollOffset.Width != 0)
            {
                hwndRenderTarget.Transform = Matrix3x2F.Translation(new SizeF(-scrollOffset.Width, -scrollOffset.Height));
            }
            else
            {
                hwndRenderTarget.Transform = Matrix3x2F.Identity;
            }

            this.caret.HideCaret();
            this.Render();
            this.caret.ShowCaret();
        }

        public void AdjustVScrollPositionForResize(double oldPosition, int newFirstLineIndex)
        {
            if ((int) oldPosition != newFirstLineIndex)
            {
                double yTop = (int)newFirstLineIndex * ((VisualLine)visualLines[0]).Height;
                for (int i = 0; i < this.visualLines.Count; i++)
                {
                    Point2F position = ((VisualLine)visualLines[i]).Position;
                    position.Y = (float)yTop;
                    ((VisualLine)visualLines[i]).Position = position;
                    yTop += ((VisualLine)visualLines[i]).Height;
                }
            }

            this.vScrollBar_Scroll(this, new ScrollEventArgs(ScrollEventType.EndScroll, newFirstLineIndex));
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

                // defaultSelectionBrush has to be solid color and not alpha
                defaultSelectionBrush = hwndRenderTarget.CreateSolidColorBrush(new ColorF(0.414f, 0.484f, 0.625f, 1.0f));
   
                this.textLayoutBuilder = new TextLayoutBuilder();
                this.selectionManager = new SelectionManager(hwndRenderTarget, this.d2dFactory);
         
                int changeStart, changeEnd;
                this.UpdateVisualLinesAndCaret(/*visualLineStartIndex*/ 0, /*forceRelayout*/ false, out changeStart, out changeEnd);
                if (this.visualLines.Count > 0)
                {
                    this.caret = new Caret(this.hwndRenderTarget, (int)((VisualLine)this.visualLines[0]).Height);
                }
                else
                {
                    this.caret = new Caret(this.hwndRenderTarget, (int)(Settings.defaultTextFormat.FontSize * 1.3f));
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
            double y;
            if (forceRelayout)
            {
                y = ((VisualLine)this.visualLines[0]).Position.Y;
                this.visualLines.Clear();
                visualLineStartIndex = 0;
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
            while (ordinal != Document.UNDEFINED_ORDINAL && y < (renderHost.ActualHeight + scrollOffset.Height))
            {
                VisualLine visualLine = textLayoutBuilder.GetNextLine(this.document, ordinal, (float)renderHost.ActualWidth, out ordinal);

                visualLine.Position = new Point2F(0, (float)y);
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
                           
                            // Update position
                            for (int p = visualLineStartIndex; p < visualLines.Count; p++)
                            {
                                Point2F position = ((VisualLine)this.visualLines[p]).Position;
                                position.Y = (float)y;
                                y += ((VisualLine)this.visualLines[p]).Height;
                                //((VisualLine)this.visualLines[p]).Position = position;
                            }
                            
                            // Continue from the last ordinal.
                            ordinal = ((VisualLine)this.visualLines[this.visualLines.Count - 1]).NextOrdinal;
                            visualLineStartIndex = this.visualLines.Count;
                        }
                    }
                }
            }

            if (changeEndIndex > 0)
            {

                if (ordinal == Document.UNDEFINED_ORDINAL)
                {
                    // Ran out of content delete everything after changeEndIndex
                    if (changeEndIndex + 1 < this.visualLines.Count)
                    {
                        this.visualLines.RemoveRange(changeEndIndex + 1, (this.visualLines.Count - changeEndIndex) - 1);
                    }
                }
                else
                {
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
                }
            }

            this.UpdateCaret();
        }

        private void UpdateCaret()
        {
            // Update caret
            if (this.caret != null && this.caret.Ordinal != Document.UNDEFINED_ORDINAL)
            {
                for (int i = 0; i < this.visualLines.Count; i++)
                {
                    VisualLine vl = (VisualLine)this.visualLines[i];
                    if (vl.BeginOrdinal <= this.caret.Ordinal && vl.NextOrdinal > this.caret.Ordinal)
                    {
                        this.caret.MoveCaretVisual(vl, this.document, scrollOffset, this.caret.Ordinal);
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
                this.scrollOffset, 
                renderTarget);
        }

        private bool HitTest(Point2F point, out int ordinal, out int lineIndex)
        {
            point = new Point2F(point.X + this.scrollOffset.Width, point.Y + this.scrollOffset.Height);
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

            this.caret.HideCaret();
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
            this.caret.ShowCaret();

            System.Windows.Media.Imaging.BitmapSource bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                 hBitmap,
                 IntPtr.Zero,
                 Int32Rect.Empty,
                 System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()
             );

            return bmpSource;
        }
        #endregion

        public int VisualLineCount
        {
            get { return this.visualLines == null ? 0 : this.visualLines.Count; }
        }

        public int PageBeginOrdinal
        {
            get
            {
                if (this.visualLines == null || this.visualLines.Count == 0)
                {
                    return this.document.FirstOrdinal();
                }
                else
                {
                    return ((VisualLine)this.visualLines[0]).BeginOrdinal;
                }
            }
        }

        D2DFactory                   d2dFactory;
        HwndRenderTarget             hwndRenderTarget;
        SolidColorBrush              defaultBackgroundBrush;
        SolidColorBrush              defaultForegroundBrush;
        SolidColorBrush              defaultSelectionBrush;
        readonly RenderHost          renderHost;
        readonly Document            document;
        TextLayoutBuilder            textLayoutBuilder;
        SelectionManager             selectionManager;
        Caret                        caret;
        SizeF                        scrollOffset;

        int                          pageBeginOrdinal;
        ArrayList                    visualLines;
    }
}
