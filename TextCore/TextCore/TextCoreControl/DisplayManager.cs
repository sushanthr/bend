using System;
using System.Windows;
using System.Collections;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

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

            this.d2dFactory = D2DFactory.CreateFactory();
            defaultBackgroundColor = new ColorF(1, 1, 1, 1);
        }

        #region Event Handling

        void RenderHost_Loaded(object sender, RoutedEventArgs e)
        {
            // Start rendering now
            renderHost.Render       = Render;
            renderHost.MouseHandler = MouseHandler;
            this.pageBeginOrdinal   = 0;
            this.pageEndOrdinal     = Document.UNDEFINED_ORDINAL;
            this.visualLines        = new ArrayList(50);
            this.EnsureVisualLinesAndUpdateCaret();
        }

        void RenderHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (renderTarget != null)
            {
                lock (renderTarget)
                {
                    // Resize the render target to the actual host size
                    renderTarget.Resize(new SizeU((uint)(renderHost.ActualWidth), (uint)(renderHost.ActualHeight)));
                    this.EnsureVisualLinesAndUpdateCaret();
                }
            }
        }

        void Document_ContentChanged(int beginOrdinal, int endOrdinal)
        {
            if (beginOrdinal == Document.UNDEFINED_ORDINAL)
                this.pageBeginOrdinal = document.FirstOrdinal();
            
            this.pageEndOrdinal = Document.UNDEFINED_ORDINAL;
            this.EnsureVisualLinesAndUpdateCaret();
        }

        #endregion

        void CreateDeviceResources()
        {
            // Only calls if resources have not been initialize before
            if (renderTarget == null)
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
                renderTarget = this.d2dFactory.CreateHwndRenderTarget(props, hwndProps);

                // Default rendering options
                defaultForegroundBrush = renderTarget.CreateSolidColorBrush(new ColorF(0, 0, 0, 1));
                defaultTextFormat = dwriteFactory.CreateTextFormat("Consolas", 14);
                // defaultSelectionBrush has to be solid color and not alpha
                defaultSelectionBrush = renderTarget.CreateSolidColorBrush(new ColorF(0.414f, 0.484f, 0.625f, 1.0f));
   
                this.textLayoutBuilder = new TextLayoutBuilder(defaultTextFormat, /*autoWrap*/true);
                this.selectionManager = new SelectionManager(renderTarget, this.d2dFactory);
                this.caret = new Caret(this.renderTarget, defaultTextFormat.FontSize * 1.3f);
            }
        }

        private void EnsureVisualLinesAndUpdateCaret()
        {
            int ordinal = this.pageBeginOrdinal;
            this.visualLines.Clear();

            float y = 0;
            for (int i = 0; ordinal != Document.UNDEFINED_ORDINAL && y < renderHost.ActualHeight; i++)
            {
                VisualLine visualLine = textLayoutBuilder.GetNextLine(this.document, ordinal, (float)renderHost.ActualWidth, out ordinal);
                this.visualLines.Add(visualLine);
                visualLine.Position = new Point2F(0, y);
                y += visualLine.Height;
            }

            // Update caret
            if (this.caret.Ordinal != Document.UNDEFINED_ORDINAL)
            {
                for (int i = 0; i < this.visualLines.Count; i++)
                {
                    VisualLine vl = (VisualLine)this.visualLines[i];
                    if (vl.BeginOrdinal < this.caret.Ordinal && vl.NextOrdinal > this.caret.Ordinal)
                    {
                        this.caret.MoveCaret(vl, this.document, this.caret.Ordinal);
                        break;
                    }
                }
            }
        }

        private void Render()
        {
            CreateDeviceResources();
            if (renderTarget.IsOccluded)
                return;

            lock (renderTarget)
            { 
                renderTarget.BeginDraw();
                renderTarget.Clear(defaultBackgroundColor);

                for (int i = 0; i < this.visualLines.Count; i++)
                {
                    VisualLine visualLine = (VisualLine)this.visualLines[i];
                    visualLine.Draw(renderTarget, defaultForegroundBrush);
                }

                renderTarget.EndDraw();
            }

            selectionManager.DrawSelection(this.visualLines, this.document);
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
                    ordinal = document.NextOrdinal(visualLine.BeginOrdinal, (int)offset);
                    lineIndex = i;

                    return true;
                }
            }

            ordinal = Document.UNDEFINED_ORDINAL;
            lineIndex = -1;
            return false;
        }

        private void MouseHandler(int x, int y, int type, int flags)
        {
            switch (type)
            {
                case 0x0201:
                    // WM_LBUTTONDOWN
                    {
                        int selectionBeginOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionBeginOrdinal, out iLine))
                        {
                            VisualLine vl = (VisualLine)this.visualLines[iLine];
                            this.caret.MoveCaret(vl, this.document, selectionBeginOrdinal);
                      
                            this.selectionManager.ResetSelection(selectionBeginOrdinal, this.visualLines, this.document);
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
                            this.caret.MoveCaret(vl, this.document, selectionEndOrdinal);
                         
                            this.selectionManager.ExpandSelection(selectionEndOrdinal, visualLines, document);
                        }
                    }
                    break;
              }
        }

        DWriteFactory                dwriteFactory;
        D2DFactory                   d2dFactory;
        HwndRenderTarget             renderTarget;
        ColorF                       defaultBackgroundColor;
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
