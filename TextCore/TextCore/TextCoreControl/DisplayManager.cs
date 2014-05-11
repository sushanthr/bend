using System;
using System.Windows;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TextCoreControl.SyntaxHighlighting;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.WindowsImagingComponent;

namespace TextCoreControl
{
    public class DisplayManager
    {
        const int MOUSEWHEEL_WINDOWS_STEP_QUANTUM = 120;

        internal DisplayManager(RenderHost renderHost, 
            Document document, 
            ScrollBar vScrollBar, 
            ScrollBar hScrollBar,
            UndoRedoManager undoRedoManager,
            FlightRecorder flightRecorder)
        {
            this.renderHost = renderHost;
            renderHost.Loaded += new RoutedEventHandler(RenderHost_Loaded);
            renderHost.SizeChanged += new SizeChangedEventHandler(RenderHost_SizeChanged);
            renderHost.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(RenderHost_PreviewKeyDown);
            renderHost.IsVisibleChanged += new DependencyPropertyChangedEventHandler(RenderHost_IsVisibleChanged);

            this.document = document;

            scrollOffset = new SizeF();
            this.d2dFactory = D2DFactory.CreateFactory();
            this.textLayoutBuilder = new TextLayoutBuilder();

            this.scrollBoundsManager = new ScrollBoundsManager(vScrollBar, hScrollBar, this, renderHost, this.document);
            vScrollBar.Scroll += new ScrollEventHandler(vScrollBar_Scroll);
            hScrollBar.Scroll += new ScrollEventHandler(hScrollBar_Scroll);

            this.lastMouseWheelTime = System.DateTime.Now.Ticks;
            this.leftMargin = 0;

            this.syntaxHighlightingService = null;
            this.document.LanguageDetector.LanguageChange += new SyntaxHighlighting.LanguageDetector.LanguageChangeEventHandler(LanguageDetector_LanguageChange);

            DebugHUD.DisplayManager = this;

            this.undoRedoManager = undoRedoManager;
            this.flightRecorder = flightRecorder;
        }

        void CreateDeviceResources()
        {
            // Only calls if resources have not been initialize before
            if (hwndRenderTarget == null)
            {
                // Remove Previous Registrations
                if (this.caret != null)
                { 
                    this.document.OrdinalShift -= this.caret.Document_OrdinalShift;
                    this.caret.CaretPositionChanged -= this.caret_CaretPositionChanged;
                }
                if (this.selectionManager != null)
                    this.selectionManager.SelectionChange -= selectionManager_SelectionChange;
                document.ContentChange -= this.Document_ContentChanged;
                document.OrdinalShift -= this.Document_OrdinalShift;
                document.PreContentChange -= document_PreContentChange;
                if (this.contentLineManager != null)
                    this.contentLineManager.Dispose(document);

                // Create the render target
                float dpiX = (float)(this.d2dFactory.DesktopDpi.X / 96.0);
                float dpiY = (float)(this.d2dFactory.DesktopDpi.Y / 96.0);
                SizeU size = new SizeU((uint)(Math.Ceiling(renderHost.ActualWidth * dpiX)), (uint)(Math.Ceiling(renderHost.ActualHeight * dpiY)));
                RenderTargetProperties props = new RenderTargetProperties(
                    RenderTargetType.Hardware,
                    new PixelFormat(),
                    this.d2dFactory.DesktopDpi.X,
                    this.d2dFactory.DesktopDpi.Y,
                    RenderTargetUsages.GdiCompatible,
                    Microsoft.WindowsAPICodePack.DirectX.Direct3D.FeatureLevel.Default);

                HwndRenderTargetProperties hwndProps = new HwndRenderTargetProperties(renderHost.Handle, size, PresentOptions.None);
                // Create the D2D Factory
                hwndRenderTarget = this.d2dFactory.CreateHwndRenderTarget(props, hwndProps);

                // Default rendering options
                defaultForegroundBrush = hwndRenderTarget.CreateSolidColorBrush(Settings.DefaultForegroundColor);
                defaultBackgroundBrush = hwndRenderTarget.CreateSolidColorBrush(Settings.DefaultBackgroundColor);

                // defaultSelectionBrush has to be solid color and not alpha
                defaultSelectionBrush = hwndRenderTarget.CreateSolidColorBrush(Settings.DefaultSelectionColor);

                if (this.syntaxHighlightingService != null)
                    this.syntaxHighlightingService.InitDisplayResources(this.hwndRenderTarget);

                this.textLayoutBuilder.InitDisplayResources(this.hwndRenderTarget);

                // Force create an empty line
                double maxVisualLineWidth;
                int changeStart, changeEnd;
                this.UpdateVisualLines(/*visualLineStartIndex*/ 0, /*forceRelayout*/ true, out maxVisualLineWidth, out changeStart, out changeEnd);
                System.Diagnostics.Debug.Assert(this.VisualLineCount != 0);

                this.caret = new Caret(this.hwndRenderTarget, (int)this.visualLines[0].Height, dpiX, dpiY);                
                this.document.OrdinalShift += this.caret.Document_OrdinalShift;
                this.caret.CaretPositionChanged += this.caret_CaretPositionChanged;

                this.selectionManager = new SelectionManager(hwndRenderTarget, this.d2dFactory);
                this.selectionManager.SelectionChange += selectionManager_SelectionChange;

                this.contentLineManager = new ContentLineManager(this.document, hwndRenderTarget, this.d2dFactory);
                this.LeftMargin = this.contentLineManager.LayoutWidth(this.textLayoutBuilder.AverageDigitWidth());
                
                // Register for document content change in the end, so that the other subsystems get to handle the change first.
                document.ContentChange += this.Document_ContentChanged;
                document.OrdinalShift += this.Document_OrdinalShift;
                document.PreContentChange += document_PreContentChange;

                // Restore the transform back on the new hwndRenderTarget
                if (this.scrollOffset.Height != 0 || this.scrollOffset.Width != 0)
                {
                    hwndRenderTarget.Transform = Matrix3x2F.Translation(new SizeF(-scrollOffset.Width, -scrollOffset.Height));
                }
                else
                {
                    hwndRenderTarget.Transform = Matrix3x2F.Identity;
                }
            }
        }

        #region WIN32 API references

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        #endregion

        #region Render host load / size / focus handling

        void RenderHost_Loaded(object sender, RoutedEventArgs e)
        {
            // Start rendering now
            this.renderHost.Render       = Render;
            this.renderHost.MouseHandler = MouseHandler;
            this.renderHost.KeyHandler = KeyHandler;
            this.renderHost.OtherHandler = OtherHandler;
            this.pageBeginOrdinal   = 0;
            this.visualLines        = new List<VisualLine>(50);
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.SizeChangeFlightEvent(renderHost.ActualWidth, renderHost.ActualHeight));
            }
        }

        void RenderHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (hwndRenderTarget != null)
            {
                if (this.flightRecorder.IsRecording)
                {
                    this.flightRecorder.AddFlightEvent(new FlightRecorder.SizeChangeFlightEvent(renderHost.ActualWidth, renderHost.ActualHeight));
                }

                // Resize the render target to the actual host size
                float dpiX = (float)(this.d2dFactory.DesktopDpi.X / 96.0);
                float dpiY = (float)(this.d2dFactory.DesktopDpi.Y / 96.0);
                hwndRenderTarget.Resize(new SizeU((uint)(Math.Ceiling(renderHost.ActualWidth * dpiX)), (uint)(Math.Ceiling(renderHost.ActualHeight * dpiY))));

                double maxVisualLineWidth;
                int changeStart, changeEnd;
                this.UpdateVisualLines(/*visualLineStartIndex*/ 0, /*forceRelayout*/ true, out maxVisualLineWidth, out changeStart, out changeEnd);
                this.UpdateCaret(this.caret.Ordinal);

                this.scrollBoundsManager.InitializeVerticalScrollBounds(this.AvailbleWidth);
            }
        }

        private void OtherHandler(int type, int wparam, int lparam)
        {
            switch (type)
            {
                /*WM_SETFOCUS*/
                case 0x0007:
                    if (this.renderHost.Visibility == Visibility.Visible)
                    {
                        // Work around from some machines that fail to paint when the renderhost gets focus.
                        this.renderHost.InvalidateVisual();
                        if (this.caret != null) this.caret.OnGetFocus();
                    }                    
                    break;
                /*WM_KILLFOCUS*/
                case 0x0008:
                    if (this.caret != null && this.renderHost.Visibility == Visibility.Visible) this.caret.OnLostFocus();
                    break;
            }
        }

        void RenderHost_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.caret != null)
            {
                if (this.renderHost.Visibility == Visibility.Visible)
                    this.caret.OnGetFocus();
                else
                    this.caret.OnLostFocus();
            }
        }

        #endregion

        #region Keyboard / Mouse Input handling

        public delegate void ShowContextMenuEventHandler();
        public event ShowContextMenuEventHandler ContextMenu;

        internal void MouseHandler(int unscaledX, int unscaledY, int type, int flags)
        {
            int x = (int)(unscaledX * (96.0 / this.d2dFactory.DesktopDpi.X));
            int y = (int)(unscaledY * (96.0 / this.d2dFactory.DesktopDpi.Y));
            bool significantEvent = false;

            switch (type)
            {
                case 0x0201:
                    // WM_LBUTTONDOWN
                    {
                        significantEvent = true;
                        SetFocus(renderHost.Handle);

                        int selectionBeginOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionBeginOrdinal, out iLine))
                        {
                            VisualLine vl = this.visualLines[iLine];

                            try
                            {
                                this.caret.PrepareBeforeRender();
                                this.caret.MoveCaretToLine(vl, this.document, scrollOffset, selectionBeginOrdinal);

                                this.hwndRenderTarget.BeginDraw();
                                this.selectionManager.ShouldUseHighlightColors = false;
                                this.selectionManager.ResetSelection(selectionBeginOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                                this.hwndRenderTarget.EndDraw();
                                this.caret.UnprepareAfterRender();
                            }
                            catch
                            {
                                this.RecoverFromRenderException();
                            }

                            this.caret.OnGetFocus();
                        }
                    }
                    break;
                case 0x0202:
                    // WM_LBUTTONUP

                    break;
                case 0x0203:
                    // WM_LBUTTONDBLCLK                0x0203
                    {
                        significantEvent = true;
                        int selectionBeginOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionBeginOrdinal, out iLine))
                        {
                            int beginOrdinal, endOrdinal;
                            this.document.GetWordBoundary(selectionBeginOrdinal, out beginOrdinal, out endOrdinal);

                            VisualLine vl = this.visualLines[iLine];

                            try
                            {
                                this.caret.PrepareBeforeRender();
                                this.caret.MoveCaretToLine(vl, this.document, scrollOffset, endOrdinal);

                                this.hwndRenderTarget.BeginDraw();
                                this.selectionManager.ShouldUseHighlightColors = false;
                                this.selectionManager.ResetSelection(beginOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                                this.selectionManager.ExpandSelection(endOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                                this.hwndRenderTarget.EndDraw();
                                this.caret.UnprepareAfterRender();
                            }
                            catch
                            {                
                                this.RecoverFromRenderException();
                            }            
                            
                            this.caret.OnGetFocus();
                        }
                    }
                    break;
                case 0x0205:
                    // WM_RBUTTONUP
                    {
                        significantEvent = true;
                        if (this.ContextMenu != null)
                        {
                            this.ContextMenu();
                        }
                    }
                    break;
                case 0X0204:
                    // WM_RBUTTONDOWN
                    break;
                case 0x0200:
                    // WM_MOUSEMOVE
                    if (flags == 1)
                    {
                        significantEvent = true;
                        // Left mouse is down.
                        int selectionEndOrdinal;
                        int iLine;
                        if (this.HitTest(new Point2F(x, y), out selectionEndOrdinal, out iLine))
                        {
                            VisualLine vl = this.visualLines[iLine];

                            try
                            {
                                this.caret.PrepareBeforeRender();
                                this.caret.MoveCaretToLine(vl, this.document, scrollOffset, selectionEndOrdinal);

                                this.hwndRenderTarget.BeginDraw();
                                this.selectionManager.ShouldUseHighlightColors = false;
                                this.selectionManager.ExpandSelection(selectionEndOrdinal, visualLines, document, this.scrollOffset, this.hwndRenderTarget);
                                this.hwndRenderTarget.EndDraw();
                                this.caret.UnprepareAfterRender();
                            }
                            catch
                            {                
                                this.RecoverFromRenderException();
                            }            
                        }
                    }
                    break;
                case 0x020A:
                    {
                        significantEvent = true;
                        // WM_MOUSEWHEEL
                        // wparam is passed in as flags
                        int highWord = flags >> 16;
                        int lowWord = flags & 0xFF;

                        // Read http://www.codeproject.com/KB/system/HiResScrollSupp.aspx?display=Mobile for info about
                        // highWord and how it corresponds to mouse type and speed.
                        long timeStamp = System.DateTime.Now.Ticks;
                        long deltaMS = (timeStamp - lastMouseWheelTime) / 10000;
                        lastMouseWheelTime = timeStamp;

                        // Emphrical constants that define the acceleration factor for mouse wheel scrolling.
                        int acceleration = Settings.MouseWheel_Normal_Step_LineCount;
                        if (deltaMS < Settings.MouseWheel_Fast1_Threshold_MS) acceleration = Settings.MouseWheel_Fast1_Step_LineCount;

                        int deltaAmount = (int)Math.Ceiling( acceleration * (double)highWord / DisplayManager.MOUSEWHEEL_WINDOWS_STEP_QUANTUM);
                        // Flip coordinates between windows and text core control.
                        deltaAmount = (0 - deltaAmount);

                        // When lowWord has MK_CONTROL 0x0008, the control key is down.
                        if ((0x0008 & lowWord) == 0)
                        {
                            this.ScrollBy(deltaAmount);
                        }
                        else
                        {
                            if (deltaAmount < 0)
                            {
                                Settings.IncreaseFontSize();
                            }
                            else
                            {
                                Settings.DecreaseFontSize();
                            }
                            this.NotifyOfSettingsChange(/*recreateRenderTarget*/false);
                        }
                    }
                    break;
            }

            if (significantEvent)
            {
                if (this.flightRecorder.IsRecording)
                {
                    this.flightRecorder.AddFlightEvent(new FlightRecorder.MouseHandlerFlightEvent(unscaledX, unscaledY, type, flags));
                }
            }
        }

        /// <summary>
        ///     Handle all non special key strokes by adding it to the document.
        ///     Special key strokes are handled in renderHost_PreviewKeyDown.
        ///     Other keys are handled here, in order to give windows the oppertunity
        ///     to translate and provide correct wParam.
        /// </summary>
        /// <param name="wparam"></param>
        /// <param name="lparam"></param>
        internal void KeyHandler(int wparam, int lparam)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.KeyHandlerFlightEvent(wparam, lparam));
            }

            char key = (char)wparam;
            if (this.SelectionBegin != this.SelectionEnd)
            {
                // Active selection needs to be deleted
                int selectionBeginOrdinal;
                string cutString = this.GetSelectedText(out selectionBeginOrdinal);
                if (cutString.Length > 0)
                {
                    this.document.DeleteAt(selectionBeginOrdinal, cutString.Length);
                }
            }

            int insertOrdinal = this.caret.Ordinal;
            string content;
            if (key == '\r')
            {
                if (Settings.ReturnKeyInsertsNewLineCharacter)
                {
                    content = "\r\n";
                }
                else
                {
                    content = "\r";
                }
                document.InsertAt(insertOrdinal, content);
            }
            else if (!char.IsControl(key))
            {
                content = key.ToString();
                document.InsertAt(insertOrdinal, content);
            }
        }

        private void RenderHost_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool handled;
            this.RenderHost_PreviewKeyDown(e.Key, e.KeyboardDevice.Modifiers, out handled);
            e.Handled = handled;
        }

        internal void RenderHost_PreviewKeyDown(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifier, out bool handled)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.DisplayManagerPreviewKeyDownFlightEvent(key, modifier));
            }

            bool adjustSelection = false;
            bool isShiftKeyDown = (modifier == System.Windows.Input.ModifierKeys.Shift);
            handled = false;

            switch (key)
            {
                case System.Windows.Input.Key.Left:
                    if (this.caret.Ordinal > this.document.FirstOrdinal())
                    {
                        int newCaretPosition = this.document.PreviousOrdinal(this.caret.Ordinal);
                        if (newCaretPosition != Document.UNDEFINED_ORDINAL && document.CharacterAt(newCaretPosition) == '\n')
                        {
                            int previousOrdinal = document.PreviousOrdinal(newCaretPosition);
                            if (previousOrdinal != Document.BEFOREBEGIN_ORDINAL && document.CharacterAt(previousOrdinal) == '\r')
                            {
                                newCaretPosition = previousOrdinal;
                            }
                        }

                        if (this.VisualLineCount > 0 && this.visualLines[0].BeginOrdinal > newCaretPosition && this.pageBeginOrdinal != document.FirstOrdinal())
                        {
                            this.ScrollBy(/*numberOfLines*/ -1);
                        }

                        this.UpdateCaret(newCaretPosition);
                        adjustSelection = true;
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.Right:
                    if (this.document.NextOrdinal(this.caret.Ordinal) != Document.UNDEFINED_ORDINAL)
                    {
                        int newCaretPosition = this.document.NextOrdinal(this.caret.Ordinal);
                        char charAt = this.document.CharacterAt(this.caret.Ordinal);
                        if (charAt == '\r' && newCaretPosition != Document.UNDEFINED_ORDINAL && document.CharacterAt(newCaretPosition) == '\n')
                        {
                            newCaretPosition = this.document.NextOrdinal(newCaretPosition);
                        }

                        if (this.VisualLineCount > 0 && this.visualLines[this.VisualLineCount - 1].NextOrdinal <= newCaretPosition)
                        {
                            this.ScrollBy(/*numberOfLines*/ +1);
                        }

                        this.UpdateCaret(newCaretPosition);
                        adjustSelection = true;
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.Up:
                    {
                        if (this.VisualLineCount > 0)
                        {
                            int firstVisibleLine = this.FirstVisibleLine();
                            if (firstVisibleLine >= 0 &&
                                this.visualLines[firstVisibleLine].BeginOrdinal <= this.caret.Ordinal &&
                                this.visualLines[firstVisibleLine].NextOrdinal > this.caret.Ordinal)
                            {
                                // Moving up from the first line, we need to scroll up - if possible.
                                this.ScrollBy(/*numberOfLines*/ -1);
                            }
                        }
                        this.caret.MoveCaretVertical(this.visualLines, document, scrollOffset, Caret.CaretStep.LineUp);
                        adjustSelection = true;
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.Down:
                    {
                        if (this.VisualLineCount > 0)
                        {
                            int lastVisibleLine = this.LastVisibleLine();
                            if (lastVisibleLine >= 0 &&
                                this.visualLines[lastVisibleLine].BeginOrdinal <= this.caret.Ordinal &&
                                this.visualLines[lastVisibleLine].NextOrdinal > this.caret.Ordinal)
                            {
                                // Moving down from the last visible line, we need to scroll down - if possible.
                                this.ScrollBy(/*numberOfLines*/ +1);
                            }
                        }
                        this.caret.MoveCaretVertical(this.visualLines, document, scrollOffset, Caret.CaretStep.LineDown);
                        adjustSelection = true;
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.End:
                    {
                        int lineIndex;
                        int ordinal;
                        Point2F caretPosition = this.caret.PositionInScreenCoOrdinates();
                        if (this.HitTest(caretPosition, out ordinal, out lineIndex))
                        {
                            VisualLine vl = this.visualLines[lineIndex];
                            int newCaretOrdinal = this.document.PreviousOrdinal(vl.NextOrdinal);
                            if (newCaretOrdinal == Document.UNDEFINED_ORDINAL)
                            {
                                int tempOrdinal = vl.BeginOrdinal;
                                while (tempOrdinal != Document.UNDEFINED_ORDINAL)
                                {
                                    newCaretOrdinal = tempOrdinal;
                                    tempOrdinal = document.NextOrdinal(tempOrdinal);
                                }
                            }

                            if (newCaretOrdinal >= vl.BeginOrdinal)
                            {
                                if (document.CharacterAt(newCaretOrdinal) == '\n')
                                {
                                    int previousOrdinal = document.PreviousOrdinal(newCaretOrdinal);
                                    if (previousOrdinal != Document.BEFOREBEGIN_ORDINAL && document.CharacterAt(previousOrdinal) == '\r')
                                        newCaretOrdinal = previousOrdinal;
                                }

                                this.caret.MoveCaretToLine(vl, this.document, this.scrollOffset, newCaretOrdinal);
                                adjustSelection = true;
                            }
                        }
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.Home:
                    {
                        int lineIndex;
                        int ordinal;
                        Point2F caretPosition = this.caret.PositionInScreenCoOrdinates();
                        if (this.HitTest(caretPosition, out ordinal, out lineIndex))
                        {
                            VisualLine vl = this.visualLines[lineIndex];
                            int newCaretOrdinal = vl.BeginOrdinal;
                            this.caret.MoveCaretToLine(vl, this.document, this.scrollOffset, newCaretOrdinal);
                            adjustSelection = true;
                        }
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.PageUp:
                    if (this.VisualLineCount > 1)
                    {
                        int lineIndex;
                        Point2F caretPosition = this.caret.PositionInScreenCoOrdinates();
                        if (this.pageBeginOrdinal > document.FirstOrdinal())
                        {
                            this.scrollBoundsManager.ScrollBy(-this.AvailableHeight);
                        }
                        else
                        {
                            // Already at the first page
                            // Set to 0 to move to the top of the page.
                            caretPosition.Y = 0;
                        }
                        int ordinal;
                        // Change caret if we are not in selection mode.
                        if ((isShiftKeyDown || this.SelectionBegin == this.SelectionEnd) && this.HitTest(caretPosition, out ordinal, out lineIndex))
                        {
                            this.caret.MoveCaretToLine(this.visualLines[lineIndex], this.document, this.scrollOffset, ordinal);
                            adjustSelection = true;
                        }
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.PageDown:
                    if (this.VisualLineCount > 1)
                    {
                        Point2F caretPosition = this.caret.PositionInScreenCoOrdinates();
                        int lastVisibleLine = this.LastVisibleLine();
                        if (lastVisibleLine == -1 || this.visualLines[lastVisibleLine].NextOrdinal != Document.UNDEFINED_ORDINAL)
                        {
                            // If we find no visible lines, it is better to try scroll to the end and fail.
                            this.scrollBoundsManager.ScrollBy(this.AvailableHeight);
                        }
                        else
                        {
                            // already at the last page
                            caretPosition.Y = this.visualLines[this.VisualLineCount - 1].Position.Y - scrollOffset.Height;
                        }

                        int ordinal;
                        int lineIndex;
                        // Change caret if we are not in selection mode.
                        if ((isShiftKeyDown || this.SelectionBegin == this.SelectionEnd) && this.HitTest(caretPosition, out ordinal, out lineIndex))
                        {
                            this.caret.MoveCaretToLine(this.visualLines[lineIndex], this.document, this.scrollOffset, ordinal);
                            adjustSelection = true;
                        }
                    }
                    handled = true;
                    break;
                case System.Windows.Input.Key.Back:
                    {
                        int selectionBeginTemp;
                        string selectedText = this.GetSelectedText(out selectionBeginTemp);
                        if (selectedText != null && selectedText.Length != 0)
                        {
                            // We have some text selected. Need to delete that instead
                            document.DeleteAt(selectionBeginTemp, selectedText.Length);
                        }
                        else if (this.caret.Ordinal > document.FirstOrdinal())
                        {
                            this.DeleteSingleCharRespectingLineBreak(document.PreviousOrdinal(this.caret.Ordinal));
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Delete:
                    {
                        int selectionBeginTemp;
                        string selectedText = this.GetSelectedText(out selectionBeginTemp);
                        if (selectedText != null && selectedText.Length != 0)
                        {
                            // We have some text selected. Need to delete that instead
                            document.DeleteAt(selectionBeginTemp, selectedText.Length);
                        }
                        else if (this.caret.Ordinal != Document.UNDEFINED_ORDINAL)
                        {
                            this.DeleteSingleCharRespectingLineBreak(this.caret.Ordinal);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.A:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        //  Control A was pressed - select all
                        try
                        {
                            this.caret.PrepareBeforeRender();
                            this.hwndRenderTarget.BeginDraw();
                            this.selectionManager.ShouldUseHighlightColors = false;
                            this.selectionManager.ResetSelection(this.document.FirstOrdinal(), this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                            this.selectionManager.ExpandSelection(this.document.LastOrdinal(), this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                            this.hwndRenderTarget.EndDraw();
                            this.caret.UnprepareAfterRender();
                        }
                        catch
                        {
                            this.RecoverFromRenderException();
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Space:
                    {
                        if (this.SelectionBegin != this.SelectionEnd)
                        {
                            if (modifier == System.Windows.Input.ModifierKeys.Shift)
                            {
                                // Need to remove single space from every line of selection
                                handled = this.AdjustLeadingInSelection(/*fRemoveSpace*/true, /*leadingString*/ " ");
                            }
                            else
                            {
                                // Need to and single space to every line of selection.
                                handled = this.AdjustLeadingInSelection(/*fRemoveSpace*/false, /*leadingString*/ " ");
                            }
                        }
                    }
                    break;
                case System.Windows.Input.Key.Tab:
                    {
                        string tabString;
                        if (Settings.UseStringForTab)
                            tabString = Settings.TabString;
                        else
                            tabString = "\t";

                        if (this.SelectionBegin != this.SelectionEnd)
                        {
                            bool isLeadingAdjusted = false;
                            if (modifier == System.Windows.Input.ModifierKeys.Shift)
                            {
                                // Need to un-tabify selection                                
                                isLeadingAdjusted = this.AdjustLeadingInSelection(/*fRemove*/true, /*leadingString*/ tabString);
                            }
                            else
                            {
                                // Need to tabify selection.
                                isLeadingAdjusted = this.AdjustLeadingInSelection(/*fRemove*/false, /*leadingString*/ tabString);
                            }
                            if (!isLeadingAdjusted)
                            {
                                int selectionBeginOrdial;
                                string selectedText = this.GetSelectedText(out selectionBeginOrdial);
                                document.DeleteAt(selectionBeginOrdial, selectedText.Length);
                                document.InsertAt(selectionBeginOrdial, tabString);
                            }
                        }
                        else
                        {
                            document.InsertAt(this.caret.Ordinal, tabString);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.D0:
                case System.Windows.Input.Key.NumPad0:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        Settings.ResetFontSize();
                        this.NotifyOfSettingsChange(/*recreateRenderTarget*/false);
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.OemPlus:
                case System.Windows.Input.Key.Add:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        Settings.IncreaseFontSize();
                        this.NotifyOfSettingsChange(/*recreateRenderTarget*/false);
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.OemMinus:
                case System.Windows.Input.Key.Subtract:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        Settings.DecreaseFontSize();
                        this.NotifyOfSettingsChange(/*recreateRenderTarget*/false);
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Pause:
                    Settings.ShowDebugHUD = !Settings.ShowDebugHUD;
                    this.renderHost.InvalidateVisual();
                    break;
                case System.Windows.Input.Key.Return:
                    if (Settings.PreserveIndentLevel)
                    {
                        string insertString = "\r\n";

                        // Find the current indent level
                        int firstOrdinal = document.FirstOrdinal();
                        int ordinal = document.PreviousOrdinal(this.caret.Ordinal);
                        while (ordinal > firstOrdinal)
                        {
                            char ch = document.CharacterAt(ordinal);
                            if (TextLayoutBuilder.IsHardBreakChar(ch))
                            {
                                int caretOrdinal = this.caret.Ordinal;
                                int ordinalAddIndent = document.NextOrdinal(ordinal);
                                while (ordinalAddIndent < caretOrdinal)
                                {
                                    char chForward = document.CharacterAt(ordinalAddIndent);
                                    if (char.IsWhiteSpace(chForward))
                                    {
                                        insertString += chForward;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                    ordinalAddIndent = document.NextOrdinal(ordinalAddIndent);
                                }
                                break;
                            }
                            ordinal = document.PreviousOrdinal(ordinal);
                        }

                        document.InsertAt(this.caret.Ordinal, insertString);
                        handled = true;
                    }
                    break;
            }

            if (adjustSelection)
            {
                try
                {
                    this.caret.PrepareBeforeRender();
                    this.hwndRenderTarget.BeginDraw();
                    this.selectionManager.ShouldUseHighlightColors = false;
                    if (isShiftKeyDown)
                    {
                        this.selectionManager.ExpandSelection(this.CaretOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                    }
                    else
                    {
                        this.selectionManager.ResetSelection(this.CaretOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                    }
                    this.hwndRenderTarget.EndDraw();
                    this.caret.UnprepareAfterRender();
                }
                catch
                {
                    this.RecoverFromRenderException();
                }
            }
        }

        /// <summary>
        ///     Used by backspace and delete, removes a single character while handling 
        ///     '\r\n' '\r' '\n' dualities.
        /// </summary>
        /// <param name="ordinalToBeginDelete">Valid ordinal to delete</param>
        private void DeleteSingleCharRespectingLineBreak(int ordinalToBeginDelete)
        {
            System.Diagnostics.Debug.Assert(Document.BEFOREBEGIN_ORDINAL != ordinalToBeginDelete);
            System.Diagnostics.Debug.Assert(Document.UNDEFINED_ORDINAL != ordinalToBeginDelete);

            int length = 1;
            if (Settings.ReturnKeyInsertsNewLineCharacter)
            {
                char ch = document.CharacterAt(ordinalToBeginDelete);
                if (ch == '\n')
                {
                    int previousOrdinal = document.PreviousOrdinal(ordinalToBeginDelete);
                    if (previousOrdinal != Document.BEFOREBEGIN_ORDINAL)
                    {
                        char chBefore = document.CharacterAt(previousOrdinal);
                        if (chBefore == '\r')
                        {
                            ordinalToBeginDelete = previousOrdinal;
                            length = 2;
                        }
                    }
                }
                else if (ch == '\r')
                {
                    int nextOrdinal = document.NextOrdinal(ordinalToBeginDelete);
                    if (nextOrdinal != Document.UNDEFINED_ORDINAL)
                    {
                        char chAfter = document.CharacterAt(nextOrdinal);
                        if (chAfter == '\n')
                        {
                            length = 2;
                        }
                    }
                }
            }
            document.DeleteAt(ordinalToBeginDelete, length);
        }
        #endregion

        #region Scrolling

        internal void vScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.VScrollBarFlightEvent(e.NewValue));
            }

            int initialLineCount = this.VisualLineCount;
            this.scrollOffset.Height = (float)e.NewValue;

            this.AddLinesAbove(/*minLinesToAdd*/0);
            this.AddLinesBelow(/*minLinesToAdd*/0);

            // Remove lines going offscreen
            int indexLastLineAboveScreenWithHardBreak = -1;
            int indexFirstLineBelowScreenWithHardBreak = int.MaxValue;
            int indexFirstVisibleLine = int.MaxValue;
            for (int j = 0; j < visualLines.Count; j++)
            {
                VisualLine vl = visualLines[j];
                float lineTop = vl.Position.Y - scrollOffset.Height;
                float lineBottom = vl.Position.Y + vl.Height - scrollOffset.Height;

                if (lineTop > this.renderHost.ActualHeight)
                {
                    // Line is below screen
                    if (vl.HasHardBreak)
                    {
                        indexFirstLineBelowScreenWithHardBreak = Math.Min(indexFirstLineBelowScreenWithHardBreak, j);
                    }
                }
                else if (lineBottom <= 0)
                {
                    // Line is above screen
                    if (vl.HasHardBreak)
                    {
                        indexLastLineAboveScreenWithHardBreak = Math.Max(indexLastLineAboveScreenWithHardBreak, j);
                    }
                }
                else if (indexFirstVisibleLine == int.MaxValue)
                {
                    indexFirstVisibleLine = j;
                }
            }
            if (indexFirstLineBelowScreenWithHardBreak != int.MaxValue)
            {
                indexFirstLineBelowScreenWithHardBreak++;
                if (indexFirstLineBelowScreenWithHardBreak < this.visualLines.Count)
                {
                    this.visualLines.RemoveRange(indexFirstLineBelowScreenWithHardBreak, this.visualLines.Count - indexFirstLineBelowScreenWithHardBreak);
                }
            }
            if (indexLastLineAboveScreenWithHardBreak > 0)
            {
                this.visualLines.RemoveRange(0, indexLastLineAboveScreenWithHardBreak + 1);
                if (indexFirstVisibleLine != int.MaxValue)
                {
                    indexFirstVisibleLine -= (indexLastLineAboveScreenWithHardBreak + 1);
                }
            }

            // Update page begin ordinal
            if (this.VisualLineCount > 0)
            {
                if (indexFirstVisibleLine == int.MaxValue || indexFirstVisibleLine >= this.VisualLineCount)
                    indexFirstVisibleLine = 0;

                this.pageBeginOrdinal = this.visualLines[indexFirstVisibleLine].BeginOrdinal;
                this.pageTop = this.visualLines[indexFirstVisibleLine].Position.Y;
            }

            // Update caret
            this.UpdateCaret(this.caret.Ordinal);

            if (this.scrollOffset.Height != 0 || this.scrollOffset.Width != 0)
            {
                hwndRenderTarget.Transform = Matrix3x2F.Translation(new SizeF(-scrollOffset.Width, -scrollOffset.Height));
            }
            else
            {
                hwndRenderTarget.Transform = Matrix3x2F.Identity;
            }

            this.caret.PrepareBeforeRender();
            this.Render();
            this.caret.UnprepareAfterRender();
        }
        
        internal void hScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.HScrollBarFlightEvent(e.NewValue));
            }

            this.scrollOffset.Width = (float)e.NewValue * Settings.DefaultTextFormat.FontSize;
            if (this.scrollOffset.Height != 0 || this.scrollOffset.Width != 0)
            {
                hwndRenderTarget.Transform = Matrix3x2F.Translation(new SizeF(-scrollOffset.Width, -scrollOffset.Height));
            }
            else
            {
                hwndRenderTarget.Transform = Matrix3x2F.Identity;
            }

            this.caret.PrepareBeforeRender();
            this.Render();
            this.caret.UnprepareAfterRender();
        }

        private void AddLinesAbove(int minLinesToAdd)
        {
            // add lines coming in at the top.
            int nextOrdinalBack = this.pageBeginOrdinal;
            double yBottom = this.pageTop;
            if (this.VisualLineCount > 0)
            {
                nextOrdinalBack = this.visualLines[0].BeginOrdinal;
                yBottom = this.visualLines[0].Position.Y;
            }

            while ((yBottom > scrollOffset.Height || minLinesToAdd > 0) && nextOrdinalBack > Document.BEFOREBEGIN_ORDINAL)
            {
                List<VisualLine> previousVisualLines = this.textLayoutBuilder.GetPreviousLines(this.document, nextOrdinalBack, this.AvailbleWidth, out nextOrdinalBack);
                if (previousVisualLines.Count == 0)
                    break;

                for (int i = previousVisualLines.Count - 1; i >= 0; i--)
                {
                    VisualLine vl = previousVisualLines[i];
                    if (this.syntaxHighlightingService != null) this.syntaxHighlightingService.HighlightLine(ref vl);

                    yBottom -= vl.Height;
                    vl.Position = new Point2F(this.LeftMargin, (float)yBottom);
                    this.visualLines.Insert(0, vl);
                    minLinesToAdd--;
                }
            }
        }

        private void AddLinesBelow(int minLinesToAdd)
        {
            // add lines coming in at the bottom.
            int nextOrdinalFwd = this.pageBeginOrdinal;
            double yTop = this.pageTop;
            if (this.VisualLineCount > 0)
            {
                int lastLine = this.VisualLineCount - 1;
                nextOrdinalFwd = this.visualLines[lastLine].NextOrdinal;
                yTop = this.visualLines[lastLine].Position.Y + this.visualLines[lastLine].Height;
            }

            float pageBottom = this.scrollOffset.Height + (float)renderHost.ActualHeight;
            // add lines making sure, we end with a line that has a hard break.
            while ((yTop < pageBottom || this.VisualLineCount == 0 || !this.visualLines[this.VisualLineCount - 1].HasHardBreak || minLinesToAdd > 0) &&
                nextOrdinalFwd != Document.UNDEFINED_ORDINAL)
            {
                VisualLine vl = this.textLayoutBuilder.GetNextLine(this.document, nextOrdinalFwd, this.AvailbleWidth, out nextOrdinalFwd);
                if (this.syntaxHighlightingService != null) this.syntaxHighlightingService.HighlightLine(ref vl);

                vl.Position = new Point2F(this.LeftMargin, (float)yTop);
                if (yTop >= scrollOffset.Height)
                {
                    this.visualLines.Add(vl);
                    minLinesToAdd--;
                }
                yTop += vl.Height;
            }
        }

        /// <summary>
        ///     Adjusts for new lines being added / removed above the current first visual line
        /// </summary>
        /// <param name="delta">Number of lines added</param>
        /// <param name="newScrollOffset">Resulting new scroll offset</param>
        internal void AdjustVScrollPositionForResize(int newPageBeginOrdinal, double newPageTop)
        {
            if (this.scrollOffset.Height != newPageTop)
            {
                // Scrolloffset actually changed.
                this.visualLines.RemoveRange(0, this.VisualLineCount);
                this.pageTop = newPageTop;
                this.pageBeginOrdinal = newPageBeginOrdinal;
                this.vScrollBar_Scroll(this, new ScrollEventArgs(ScrollEventType.EndScroll, newPageTop));
            }
        }

        internal void GenerateLinesForScroll(int numberOfLines, out double heightToScrollBy)
        {
            heightToScrollBy = 0;

            if (numberOfLines > 0)
            {
                int lastVisibleLine = this.LastVisibleLine();
                if (lastVisibleLine < 0)
                {
                    this.AddLinesBelow(numberOfLines);
                    lastVisibleLine = 0;
                }
                else if (this.VisualLineCount - lastVisibleLine - 1 < numberOfLines)
                {
                    this.AddLinesBelow(numberOfLines - (this.VisualLineCount - lastVisibleLine - 1));
                }

                for (int i = lastVisibleLine; i < this.VisualLineCount && numberOfLines > 0; i++)
                {
                    numberOfLines--;
                    heightToScrollBy += this.visualLines[i].Height;
                }
            }
            else if (numberOfLines < 0)
            {
                int absNumberOfLines = Math.Abs(numberOfLines);
                int firstVisibleLine = this.FirstVisibleLine();
                if (firstVisibleLine < 0)
                {
                    this.AddLinesAbove(-numberOfLines);
                    firstVisibleLine = this.VisualLineCount - 1;
                }
                else if (firstVisibleLine < absNumberOfLines)
                {
                    int originalLineCount = this.VisualLineCount;
                    this.AddLinesAbove(absNumberOfLines - firstVisibleLine);
                    firstVisibleLine += (this.VisualLineCount - originalLineCount);
                }

                for (int i = firstVisibleLine - 1; i >= 0 && absNumberOfLines > 0; i--)
                {
                    absNumberOfLines--;
                    heightToScrollBy -= this.visualLines[i].Height;
                }
            }

            if (heightToScrollBy == 0)
            {
                if (numberOfLines < 0)
                {
                    heightToScrollBy = -int.MaxValue;
                }
                else if (numberOfLines > 0)
                {
                    heightToScrollBy = int.MaxValue;
                }
            }
        }

        public void ScrollBy(int numberOfLines)
        {
            double offset;
            this.GenerateLinesForScroll(numberOfLines, out offset);
            this.scrollBoundsManager.ScrollBy(offset);
        }

        /// <summary>
        ///     Performs smooth scroll if settings allow it, otherwise performs a regular scroll.
        /// </summary>
        /// <param name="numberOfLines">Number of lines to scroll by</param>
        public void SmoothScrollBy(int numberOfLines)
        {
            if (numberOfLines != 0)
            {
                if (Settings.AllowSmoothScrollBy)
                {
                    int finalScroll = (numberOfLines > 0) ? 1 : -1;
                    numberOfLines = numberOfLines - finalScroll;
                    double heightToScrollBy;
                    this.GenerateLinesForScroll(numberOfLines, out heightToScrollBy);

                    int sign = (heightToScrollBy > 0 ? +1 : -1);
                    double value = Math.Abs(heightToScrollBy);
                    double[] offsets = new double[10];

                    offsets[0] = 0;
                    if (value > 5) { offsets[0] = 5; value -= 5; }
                    offsets[9] = 0;
                    if (value > 5) { offsets[9] = 5; value -= 5; }
                    offsets[1] = 0;
                    if (value > 10) { offsets[1] = 10; value -= 10; }
                    offsets[8] = 0;
                    if (value > 10) { offsets[8] = 10; value -= 10; }

                    offsets[2] = (value / 6);
                    offsets[3] = offsets[2];
                    offsets[4] = offsets[2];
                    offsets[6] = offsets[2];
                    offsets[7] = offsets[2];
                    value -= (offsets[2] + offsets[3] + offsets[4] + offsets[6] + offsets[7]);
                    offsets[5] = value;

                    Debug.Assert(Math.Abs(heightToScrollBy) == offsets[0] + offsets[1] + offsets[2] + offsets[3] + offsets[4] + offsets[5] + offsets[6] + offsets[7] + offsets[8] + offsets[9]);

                    this.caret.PrepareBeforeRender();
                    for (int i = 0; i <= 9; i++)
                    {
                        this.scrollOffset.Height = (float)this.scrollBoundsManager.AdjustVScrollOffset(sign * offsets[i]);
                        if (this.scrollOffset.Height != 0 || this.scrollOffset.Width != 0)
                        {
                            hwndRenderTarget.Transform = Matrix3x2F.Translation(new SizeF(-scrollOffset.Width, -scrollOffset.Height));
                        }
                        else
                        {
                            hwndRenderTarget.Transform = Matrix3x2F.Identity;
                        }

                        this.Render();
                    }
                    this.caret.UnprepareAfterRender();

                    this.ScrollBy(finalScroll);
                }
                else
                {
                    this.ScrollBy(numberOfLines);
                }
            }
        }

        public void ScrollToContentLineNumber(int contentLineNumber, bool moveCaret)
        {
            if (this.contentLineManager != null)
            {             
                // Internally we are zero based, so subtract one.
                contentLineNumber--;
                int targetOrdinal;

                if (contentLineNumber < 0)
                    contentLineNumber = 0;
                
                if (contentLineManager.MaxContentLines <= contentLineNumber)
                    targetOrdinal = document.LastOrdinal();
                else
                    targetOrdinal = this.contentLineManager.GetBeginOrdinal(this.document, contentLineNumber);

                if (targetOrdinal != Document.UNDEFINED_ORDINAL && targetOrdinal != Document.BEFOREBEGIN_ORDINAL)
                {
                    this.ScrollOrdinalIntoView(targetOrdinal);                    
                    this.UpdateCaret(targetOrdinal);
                }
            }
        }

        public bool ScrollOrdinalIntoView(int ordinal, bool ignoreScrollBounds = false)
        {
            Debug.WriteLine("Scrolling to ordinal " + ordinal);
            bool didScroll = false;

            int firstVisibleLine = this.FirstVisibleLine();
            if (this.VisualLineCount > 0 && firstVisibleLine == -1)
            {
                // Line collection is completely out of view, bring couple of lines into view.
                this.scrollBoundsManager.ScrollBy(this.visualLines[0].Position.Y - scrollOffset.Height);
                firstVisibleLine = this.FirstVisibleLine();
            }

            if (this.VisualLineCount > 0 && firstVisibleLine != -1)
            {
                // We have a visible line check to see if jumping to start is more optimal.
                if (Math.Abs(ordinal - this.document.FirstOrdinal()) < Math.Abs(this.visualLines[firstVisibleLine].BeginOrdinal - ordinal))
                {
                    // Jump to start.
                    this.pageBeginOrdinal = this.document.FirstOrdinal();
                    this.pageTop = 0;
                    this.visualLines.Clear();
                    this.scrollBoundsManager.ScrollBy(-int.MaxValue);
                    firstVisibleLine = this.FirstVisibleLine();
                }
            }

            int lastVisibleLine  = this.LastVisibleLine();
            if (this.VisualLineCount > 0 && firstVisibleLine != -1 && lastVisibleLine != -1)
            {
                // Scroll Down
                if (this.visualLines[lastVisibleLine].NextOrdinal <= ordinal)
                {
                    int lineToVerify = lastVisibleLine;
                    int startLineToVerify = lineToVerify;
                    double startBottom = this.visualLines[startLineToVerify].Position.Y + this.visualLines[startLineToVerify].Height;
                    while (this.visualLines[lineToVerify].NextOrdinal <= ordinal)
                    {
                        lineToVerify++;
                        if (lineToVerify == this.VisualLineCount)
                        {
                            this.AddLinesBelow(10);
                            // We failed to add a line.
                            if (lineToVerify == this.VisualLineCount)
                            {
                                lineToVerify = this.VisualLineCount - 1;
                                break;
                            }
                        }
                    }
                    if (startLineToVerify != lineToVerify)
                    {
                        if (ignoreScrollBounds)
                        {
                            double delta = this.visualLines[lineToVerify].Position.Y + this.visualLines[lineToVerify].Height - startBottom;
                            this.scrollBoundsManager.UpdateVerticalScrollBoundsDueToContentChange(delta);
                        }

                        // scroll to make the lineToVerify be somewhere in the middle.
                        int deltaAdded = lineToVerify - startLineToVerify;
                        int extraDelta = 3 * (lastVisibleLine - firstVisibleLine) / 4;
                        this.SmoothScrollBy(deltaAdded + extraDelta);
                        didScroll = true;
                    }
                }
                else if (this.visualLines[firstVisibleLine].BeginOrdinal >= ordinal)
                {
                    // Scroll Up
                    int numberOfLinesToScrollBy = 0;
                    int lineToVerify = firstVisibleLine;
                    while (this.visualLines[lineToVerify].BeginOrdinal >= ordinal)
                    {
                        numberOfLinesToScrollBy++;
                        lineToVerify--;
                        if (lineToVerify < 0)
                        {
                            int oldLineCount = this.VisualLineCount;
                            this.AddLinesAbove(+10);
                            int numberOfLinesAdded = this.VisualLineCount - oldLineCount;
                            if (numberOfLinesAdded <= 0)
                            {
                                // Could not add lines. Bail.
                                break;
                            }
                            else
                            {
                                lineToVerify += numberOfLinesAdded;
                            }
                        }
                    }

                    if (numberOfLinesToScrollBy != 0)
                    {
                        int extraDelta = 1 * (lastVisibleLine - firstVisibleLine) / 4;
                        this.SmoothScrollBy(-numberOfLinesToScrollBy - extraDelta);
                        didScroll = true;
                    }
                }
            }
            return didScroll;
        }

        #endregion

        #region Selection

        public void SetHighlightMode(bool shouldUseHighlightColors)
        {   
            if (this.selectionManager != null)
            { 
                this.selectionManager.ShouldUseHighlightColors = shouldUseHighlightColors;
            }
        }

        public void SelectRange(int beginAtOrdinal, int endBeforeOrdinal)
        {
            try
            {
                this.caret.PrepareBeforeRender();
                this.hwndRenderTarget.BeginDraw();
                this.selectionManager.ResetSelection(beginAtOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                if (beginAtOrdinal != endBeforeOrdinal)
                {
                    this.selectionManager.ExpandSelection(endBeforeOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                }
                this.hwndRenderTarget.EndDraw();
                this.UpdateCaret(endBeforeOrdinal);
                this.caret.UnprepareAfterRender();
            }
            catch
            {
                this.RecoverFromRenderException();
            }
        }

        public string GetSelectedText(out int selectionBeginOrdinal)
        {
            StringBuilder selectedString = new System.Text.StringBuilder();
            
            selectionBeginOrdinal = this.SelectionBegin;
            int selectionEndOrdinal = this.SelectionEnd;

            if (selectionBeginOrdinal < selectionEndOrdinal &&
                selectionBeginOrdinal != Document.BEFOREBEGIN_ORDINAL &&
                selectionBeginOrdinal != Document.UNDEFINED_ORDINAL &&
                selectionEndOrdinal != Document.BEFOREBEGIN_ORDINAL &&
                selectionEndOrdinal != Document.UNDEFINED_ORDINAL)
            {
                // Valid selection range exists
                int tempOrdinal = selectionBeginOrdinal;
                while (tempOrdinal != selectionEndOrdinal)
                {
                    selectedString.Append(document.CharacterAt(tempOrdinal));
                    tempOrdinal = document.NextOrdinal(tempOrdinal);
                }
            }

            return selectedString.ToString();
        }               
        
        /// <summary>
        ///     Removes/Adds space or tabs after line breaks in the selected region.
        /// </summary>        
        /// <returns>
        ///     Returns true if any changes were made. Actually returns true if we believe
        ///     leading can be changed regardless of whether actual changes are made.
        /// </returns>
        private bool AdjustLeadingInSelection(bool fRemove, string leading)
        {
            bool selectionIsAcrossLines = false;
            if (this.SelectionBegin != this.SelectionEnd)
            {                
                for (int i = this.SelectionBegin; i < this.SelectionEnd; i++)
                {
                    if (TextLayoutBuilder.IsHardBreakChar(document.CharacterAt(i)))
                    {
                        selectionIsAcrossLines = true;
                        break;
                    }
                }
                if (!selectionIsAcrossLines)
                {
                    bool beginIsAfterHardBreak = this.SelectionBegin == document.FirstOrdinal() || TextLayoutBuilder.IsHardBreakChar(document.CharacterAt(document.PreviousOrdinal(this.SelectionBegin)));
                    bool endIsAfterHardBreak = this.SelectionEnd == document.LastOrdinal() || TextLayoutBuilder.IsHardBreakChar(document.CharacterAt(document.NextOrdinal(this.SelectionEnd)));
                    selectionIsAcrossLines = beginIsAfterHardBreak && endIsAfterHardBreak;
                }

                if (selectionIsAcrossLines)
                {
                    int beginOrdinal = this.SelectionBegin;
                    // Roll back until after the previous line break.
                    // Since we are looking backwards, it is okay to call IsHardBreakChar with a single character.
                    while (!TextLayoutBuilder.IsHardBreakChar(document.CharacterAt(beginOrdinal)))
                    {
                        int temp = document.PreviousOrdinal(beginOrdinal);
                        if (temp == Document.BEFOREBEGIN_ORDINAL)
                            break;
                        beginOrdinal = temp;
                    }
                    int selectionBeginOrdinal = beginOrdinal;
                    if (beginOrdinal != this.SelectionBegin)
                        selectionBeginOrdinal = document.NextOrdinal(selectionBeginOrdinal);

                    StringBuilder stringBuilder = new StringBuilder();
                    int ordinal = beginOrdinal;
                    while (ordinal < this.SelectionEnd && ordinal != Document.UNDEFINED_ORDINAL)
                    {
                        char ch = document.CharacterAt(ordinal);
                        stringBuilder.Append(ch);
                        ordinal = document.NextOrdinal(ordinal);
                    }
                    int length = stringBuilder.Length;
                    if (fRemove)
                    {
                        stringBuilder.Replace("\n" + leading, "\n");
                        if (beginOrdinal == document.FirstOrdinal())
                        {
                            bool fStartsWithLeading = true;
                            for (int i = 0; i < leading.Length; i++)
                            {
                                if (stringBuilder[i] != leading[i])
                                    fStartsWithLeading = false;
                            }
                            if (fStartsWithLeading)
                                stringBuilder.Remove(0, 1);
                        }
                    }
                    else
                    {
                        stringBuilder.Replace("\n", "\n" + leading);
                        if (beginOrdinal == document.FirstOrdinal())
                        {
                            stringBuilder.Insert(0, leading);
                        }
                    }
                    undoRedoManager.BeginTransaction();
                    document.DeleteAt(beginOrdinal, length);
                    document.InsertAt(beginOrdinal, stringBuilder.ToString());
                    undoRedoManager.EndTransaction();
                    try
                    {
                        this.caret.PrepareBeforeRender();
                        this.hwndRenderTarget.BeginDraw();
                        this.selectionManager.ShouldUseHighlightColors = false;
                        this.selectionManager.ResetSelection(selectionBeginOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                        this.selectionManager.ExpandSelection(document.NextOrdinal(beginOrdinal, (uint)stringBuilder.Length), this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                        this.hwndRenderTarget.EndDraw();
                        this.caret.UnprepareAfterRender();
                    }
                    catch
                    {
                        this.RecoverFromRenderException();
                    }
                }
            }
            return selectionIsAcrossLines;
        }

        internal void SetBackgroundHighlight(int beginOrdinal, int endOrdinal)
        {
            this.selectionManager.BackgroundHighlight.SetBackgroundHighlight(beginOrdinal, endOrdinal);
        }

        internal void ResetBackgroundHighlight()
        {
            this.selectionManager.BackgroundHighlight.ResetBackgroundHighlight();
        }

        internal bool IsInBackgroundHightlight(int ordinal)
        {
            return this.selectionManager.BackgroundHighlight.IsInBackgroundHighlight(ordinal);
        }

        internal void GetBackgroundHighlightRange(out int beginOrdinal, out int endOrdinal)
        {
            this.selectionManager.BackgroundHighlight.GetBackgroundHightlightRange(out beginOrdinal, out endOrdinal);
        }

        #endregion

        #region Content change handling

        void document_PreContentChange(int beginOrdinal, int endOrdinal)
        {
            if (beginOrdinal != Document.UNDEFINED_ORDINAL)
            {
                // Bring the begin ordinal into view.
                this.ScrollOrdinalIntoView(beginOrdinal, /*ignoreScrollBounds*/true);       
            }
        }               

        void Document_ContentChanged(int beginOrdinal, int endOrdinal, string content)
        {
            if (this.syntaxHighlightingService != null)
                this.syntaxHighlightingService.NotifyOfContentChange(beginOrdinal, endOrdinal, content);

            if (beginOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // Full reset, most likely a new file was loaded.
                this.pageBeginOrdinal = document.FirstOrdinal();
                this.pageTop = 0;
                this.SelectRange(this.pageBeginOrdinal, this.pageBeginOrdinal);

                double maxVisualLineWidth;
                int changeStart, changeEnd;
                this.UpdateVisualLines(/*visualLineStartIndex*/ 0, /*forceRelayout*/ true, out maxVisualLineWidth, out changeStart, out changeEnd);
                this.UpdateCaret(endOrdinal);

                this.scrollBoundsManager.InitializeVerticalScrollBounds(this.AvailbleWidth);
            }
            else
            {
                // ScrollBounds Prep for estimate scroll bounds delta due to change.
                int lastVisualLineNextOrdinal = Document.UNDEFINED_ORDINAL;
                float trackedVisualLineTop = 0f;
                bool trackableLineFound = false;
                if (this.VisualLineCount > 0)
                {
                    Debug.Assert(this.visualLines[this.VisualLineCount - 1].HasHardBreak);
                    lastVisualLineNextOrdinal = this.visualLines[this.VisualLineCount - 1].NextOrdinal;
                    if (lastVisualLineNextOrdinal > endOrdinal)
                    {
                        trackedVisualLineTop = this.visualLines[this.VisualLineCount - 1].Position.Y;
                        trackableLineFound = true;
                    }
                }
                                
                // Ensure that pageBegin is outside the change region
                if (this.pageBeginOrdinal == Document.BEFOREBEGIN_ORDINAL || 
                    this.pageBeginOrdinal >= beginOrdinal && this.pageBeginOrdinal <= endOrdinal)
                {
                    this.pageBeginOrdinal = this.document.FirstOrdinal();
                    this.pageTop = 0;
                }
                
                int visualLineStartIndex = -1;
                for (int i = 0; i < visualLines.Count; i++)
                {
                    VisualLine vl = visualLines[i];

                    // Null out all lines that intersect with the change region
                    bool lineIsOutsideChange = vl.NextOrdinal < beginOrdinal || vl.BeginOrdinal > endOrdinal;
                    if (!lineIsOutsideChange)
                    {
                        visualLines[i] = null;
                        if (visualLineStartIndex == -1)
                        {
                            visualLineStartIndex = i;
                        }
                    }
                }
                                
                visualLineStartIndex = (visualLineStartIndex > 0) ? visualLineStartIndex - 1 : 0;
                double maxVisualLineWidth;
                int changeStart, changeEnd;
                this.UpdateVisualLines(visualLineStartIndex, /*forceRelayout*/ false, out maxVisualLineWidth, out changeStart, out changeEnd);

                // Scrollbounds: Estimate delta due to change (only works when change is above the last ordinal on page).
                //               forces full document scroll bounds computation otherwise.
                float scrollBoundsDelta = 0f;
                bool forceDocumentBoundsMeasure = true;
                int lastLineIndex = this.VisualLineCount - 1;
                if (trackableLineFound && this.VisualLineCount > 0)
                {
                    Debug.Assert(this.visualLines[lastLineIndex].HasHardBreak);
                    int newLastVisualLineNextOrdinal = this.visualLines[lastLineIndex].NextOrdinal;

                    if (newLastVisualLineNextOrdinal == lastVisualLineNextOrdinal)
                    {
                        // Matching lines, the scroll bounds delta can be calculated.
                        scrollBoundsDelta = this.visualLines[lastLineIndex].Position.Y - trackedVisualLineTop;
                        forceDocumentBoundsMeasure = false;
                    }
                    else if (newLastVisualLineNextOrdinal < lastVisualLineNextOrdinal)
                    {
                        // try generating 5 more lines to see if lastVisualLineNextOrdinal can be found
                        float tempLinesDelta = 0;
                        for (int i = 0; i < 5 && newLastVisualLineNextOrdinal != Document.UNDEFINED_ORDINAL; i++)
                        {
                            VisualLine visualLine = textLayoutBuilder.GetNextLine(this.document, newLastVisualLineNextOrdinal, this.AvailbleWidth, out newLastVisualLineNextOrdinal);
                            if (this.syntaxHighlightingService != null) this.syntaxHighlightingService.HighlightLine(ref visualLine);

                            tempLinesDelta += visualLine.Height;
                            if (visualLine.NextOrdinal == lastVisualLineNextOrdinal)
                            {
                                scrollBoundsDelta = this.visualLines[lastLineIndex].Position.Y + tempLinesDelta - trackedVisualLineTop;
                                forceDocumentBoundsMeasure = false;
                                break;
                            }
                        }
                    }
                    else if (newLastVisualLineNextOrdinal > lastVisualLineNextOrdinal)
                    {
                        // Look up the array to find the line again.
                        for (int i = this.VisualLineCount - 1; i >= 0; i--)
                        {
                            if (this.visualLines[i].NextOrdinal == lastVisualLineNextOrdinal)
                            {
                                // We can safely assert this, since the change is completely before lastVisualLineNextOrdinal
                                // and this invariant should not have changed.
                                Debug.Assert(this.visualLines[i].HasHardBreak);
                                scrollBoundsDelta = this.visualLines[i].Position.Y - trackedVisualLineTop;
                                forceDocumentBoundsMeasure = false;
                            }
                        }
                    }
                }

                if (!forceDocumentBoundsMeasure)
                {
                    // scrollBoundsDelta is accurate and the scrollBoundsManager has to be updated with it.
                    if (scrollBoundsDelta != 0)
                    {
                        this.scrollBoundsManager.UpdateVerticalScrollBoundsDueToContentChange(scrollBoundsDelta, maxVisualLineWidth);
                    }
                }

                // Scroll the endOrdinal into view
                // Since the scroll bounds are not correct at this point, simply increment it so that scrollby can
                // scroll to the next line. The async scrollbounds estimator will fix the scroll bounds up later.
                bool contentRendered = this.ScrollOrdinalIntoView(endOrdinal, /*ignoreScrollBounds*/true);

                // Render
                try
                {
                    this.caret.PrepareBeforeRender();
                    this.UpdateCaret(endOrdinal);
                    hwndRenderTarget.BeginDraw();
                    this.selectionManager.ShouldUseHighlightColors = false;
                    this.selectionManager.ResetSelection(endOrdinal, this.visualLines, this.document, this.scrollOffset, this.hwndRenderTarget);
                    if (!contentRendered)
                    {
                        // Nothing to render if scrolling already rendered content on screen.
                        this.RenderToRenderTarget(changeStart, changeEnd, hwndRenderTarget);
                    }
                    hwndRenderTarget.EndDraw();
                    this.caret.UnprepareAfterRender();
                }
                catch
                {                
                    this.RecoverFromRenderException();
                }            

                if (forceDocumentBoundsMeasure)
                {
                    Debug.WriteLine("Initiating full document scroll bounds measure due to change.");
                    this.scrollBoundsManager.InitializeVerticalScrollBounds(this.AvailbleWidth);
                }
            }
        }
        
        void Document_OrdinalShift(Document document, int beginOrdinal, int shift)
        {
            for (int i = 0; i < visualLines.Count; i++)
            {
                VisualLine vl = visualLines[i];
                vl.OrdinalShift(beginOrdinal, shift);
            }

            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.pageBeginOrdinal);

            if (this.selectionManager != null)            
                this.selectionManager.NotifyOfOrdinalShift(beginOrdinal, shift);

            if (this.syntaxHighlightingService != null)
                this.syntaxHighlightingService.NotifyOfOrdinalShift(document, beginOrdinal, shift);
        }

        internal void NotifyOfSettingsChange(bool recreateRenderTarget)
        {
            if (this.hwndRenderTarget != null)
            {
                // Release the render target so that it can be recreated.
                if (recreateRenderTarget)
                {
                    this.hwndRenderTarget = null;
                }
                this.CreateDeviceResources();

                this.textLayoutBuilder.NotifyOfSettingsChange();

                if (this.syntaxHighlightingService != null)
                {
                    this.syntaxHighlightingService.InitDisplayResources(this.hwndRenderTarget);
                }

                if (this.syntaxHighlightingService != null)
                    this.syntaxHighlightingService.NotifyOfSettingsChange();

                if (this.contentLineManager != null)
                {
                    this.contentLineManager.NotifyOfSettingsChange();
                    this.LeftMargin = this.contentLineManager.LayoutWidth(this.textLayoutBuilder.AverageDigitWidth());
                }

                this.scrollBoundsManager.NotifyOfSettingsChange();
                this.scrollBoundsManager.InitializeVerticalScrollBounds(this.AvailbleWidth);
                
                double maxVisualLineWidth;
                int changeStart, changeEnd;
                this.UpdateVisualLines(/*visualLineStartIndex*/ 0, /*forceRelayout*/ true, out maxVisualLineWidth, out changeStart, out changeEnd);
                this.UpdateCaret(this.caret.Ordinal);

                this.caret.PrepareBeforeRender();
                this.Render();
                this.caret.UnprepareAfterRender();
            }
        }

        private void UpdateVisualLines(
            int visualLineStartIndex, 
            bool forceRelayout,
            out double maxVisualLineWidth, 
            out int changeStartIndex,
            out int changeEndIndex)
        {
            int ordinal = this.pageBeginOrdinal;
            double y = this.pageTop;
            maxVisualLineWidth = 0;
            if (forceRelayout)
            {
                this.visualLines.Clear();
                visualLineStartIndex = 0;
            }
            else
            {
                if (this.visualLines.Count > visualLineStartIndex && this.visualLines[visualLineStartIndex] != null)
                {
                    ordinal = this.visualLines[visualLineStartIndex].BeginOrdinal;
                    y = this.visualLines[visualLineStartIndex].Position.Y;
                }
                else
                {
                    visualLineStartIndex = 0;
                }
            }

            changeStartIndex = -1;
            changeEndIndex = -1;
            bool previousLineHasHardBreak = true;
            while (ordinal != Document.UNDEFINED_ORDINAL && (y < (renderHost.ActualHeight + scrollOffset.Height) || !previousLineHasHardBreak))
            {
                VisualLine visualLine = textLayoutBuilder.GetNextLine(this.document, ordinal, this.AvailbleWidth, out ordinal);
                if (this.syntaxHighlightingService != null) this.syntaxHighlightingService.HighlightLine(ref visualLine);

                visualLine.Position = new Point2F(this.LeftMargin, (float)y);
                y += visualLine.Height;
                previousLineHasHardBreak = visualLine.HasHardBreak;
                maxVisualLineWidth = Math.Max(maxVisualLineWidth, visualLine.Width);

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
                        if (visualLine.NextOrdinal == this.visualLines[visualLineStartIndex].BeginOrdinal &&
                            (this.syntaxHighlightingService == null || this.syntaxHighlightingService.CanReuseLine(this.visualLines[visualLineStartIndex])))
                        {
                            // We have reflowed enough, things are the same from here on.
                           
                            // Update position
                            for (int p = visualLineStartIndex; p < visualLines.Count; p++)
                            {
                                Point2F position = this.visualLines[p].Position;
                                position.Y = (float)y;
                                y += this.visualLines[p].Height;
                                this.visualLines[p].Position = position;
                                changeEndIndex = p;
                            }
                            
                            // Continue from the last ordinal.
                            ordinal = this.visualLines[this.visualLines.Count - 1].NextOrdinal;
                            visualLineStartIndex = this.visualLines.Count;
                        }
                    }
                }
            }

            if (changeEndIndex >= 0)
            {
                if (!(ordinal != Document.UNDEFINED_ORDINAL && (y < (renderHost.ActualHeight + scrollOffset.Height) || !previousLineHasHardBreak)))
                {
                    // Ran out of content delete everything after changeEndIndex
                    if (changeEndIndex + 1 < this.visualLines.Count)
                    {
                        this.visualLines.RemoveRange(changeEndIndex + 1, (this.visualLines.Count - changeEndIndex) - 1);
                    }
                }
                else
                {
                    // Remove any trailing null lines.
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

#if DEBUG
            // Verify the invariant that there are no null lines after UpdateVisualLines call.
            for (int i = 0; i < this.visualLines.Count; i++)
                Debug.Assert(this.visualLines[i] != null);

            // Verify that last line has a hard break
            Debug.Assert(this.visualLines.Count > 0);
            Debug.Assert(this.visualLines[this.visualLines.Count - 1].HasHardBreak);
#endif
        }

        private void UpdateCaret(int newCaretOrdinal)
        {
            // Update caret
            if (this.caret != null && newCaretOrdinal != Document.UNDEFINED_ORDINAL)
            {
                for (int i = 0; i < this.visualLines.Count; i++)
                {
                    VisualLine vl = (VisualLine)this.visualLines[i];
                    if (vl.BeginOrdinal <= newCaretOrdinal && vl.NextOrdinal > newCaretOrdinal)
                    {
                        this.caret.MoveCaretToLine(vl, this.document, scrollOffset, newCaretOrdinal);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Render and hit testing
        internal void Render()
        {
            CreateDeviceResources();
            if (hwndRenderTarget.IsOccluded)
                return;
                        
            try
            {
                this.caret.PrepareBeforeRender();
                hwndRenderTarget.BeginDraw();
                this.RenderToRenderTarget(/*redrawBegin*/ 0, /*redrawEnd*/ this.visualLines.Count - 1, hwndRenderTarget);
                hwndRenderTarget.EndDraw();
                this.caret.UnprepareAfterRender();
            }
            catch
            {                
                this.RecoverFromRenderException();
            }            
        }

        private void RenderToRenderTarget(int redrawBegin, int redrawEnd, RenderTarget renderTarget)
        {
            RectF wipeBounds;
            if ((redrawBegin == 0 && redrawEnd == visualLines.Count - 1) || redrawBegin < 0 || redrawEnd < 0 || redrawBegin >= this.VisualLineCount || redrawEnd >= this.VisualLineCount)
            {
                renderTarget.Clear(defaultBackgroundBrush.Color);
                redrawBegin = this.FirstVisibleLine();
                redrawEnd = this.LastVisibleLine();
            }
            else
            {
                VisualLine beginLine = this.visualLines[redrawBegin];
                VisualLine endLine = this.visualLines[redrawEnd];
                wipeBounds = new RectF(0.0f, beginLine.Position.Y, renderTarget.Size.Width, endLine.Position.Y + endLine.Height);
                if (redrawEnd == this.VisualLineCount - 1 && this.VisualLineCount > 0 && this.visualLines[redrawEnd].NextOrdinal == Document.UNDEFINED_ORDINAL)
                {
                    // There are no more lines after the last visual line, erase the area. Since this
                    // redraw could be because of content deletion that moved lines up.
                    wipeBounds.Bottom = this.scrollOffset.Height + renderTarget.Size.Height;
                }
                renderTarget.FillRectangle(wipeBounds, defaultBackgroundBrush);
            }

            this.selectionManager.BackgroundHighlight.Draw(this.visualLines,
                redrawBegin,
                redrawEnd,
                this.document,
                this.scrollOffset,
                renderTarget);

            for (int i = redrawBegin; i <= redrawEnd && i >= 0 && i < this.visualLines.Count; i++)
            {
                VisualLine visualLine = this.visualLines[i];
                visualLine.Draw(defaultForegroundBrush, renderTarget);
            }

            this.selectionManager.DrawSelection(
                selectionManager.GetSelectionBeginOrdinal(),
                selectionManager.GetSelectionEndOrdinal(), 
                this.visualLines,
                redrawBegin,
                redrawEnd,
                this.document, 
                this.scrollOffset, 
                renderTarget);

            this.contentLineManager.DrawLineNumbers(
                redrawBegin,
                redrawEnd,
                this.visualLines, 
                this.document,
                this.scrollOffset,
                this.LeftMargin, 
                renderTarget);

            DebugHUD.Draw(renderTarget, this.scrollOffset);

#if DEBUG
            // Verify that last line has a hard break
            Debug.Assert(this.visualLines.Count == 0 || this.visualLines[this.visualLines.Count - 1].HasHardBreak);
#endif
        }

        private bool HitTest(Point2F point, out int ordinal, out int lineIndex)
        {
            point = new Point2F(point.X + scrollOffset.Width, point.Y + scrollOffset.Height);
            for (int i = 0; i < visualLines.Count; i++)
            {
                VisualLine visualLine = visualLines[i];
                if (visualLine.Position.Y <= point.Y && visualLine.Position.Y + visualLine.Height > point.Y)
                {
                    point.Y -= visualLine.Position.Y;
                    point.X -= visualLine.Position.X;
                    uint offset;
                    visualLine.HitTest(point, out offset);
                    ordinal = document.NextOrdinal(visualLine.BeginOrdinal, (uint)offset);
                    if (ordinal == Document.UNDEFINED_ORDINAL)
                    {
                        // If we cannot snap to the right, try the character to the left.
                        ordinal = document.NextOrdinal(visualLine.BeginOrdinal, (uint)offset - 1);
                    }
                    lineIndex = i;

                    return true;
                }
            }

            ordinal = Document.UNDEFINED_ORDINAL;
            lineIndex = -1;
            return false;
        }

        private void RecoverFromRenderException()
        {
            this.NotifyOfSettingsChange(/*recreateRenderTarget*/true);
            this.renderHost.InvalidateVisual();            
        }

        #endregion

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

        internal System.Windows.Media.Imaging.BitmapSource Rasterize()
        {
            IntPtr hBitmap = IntPtr.Zero;

            try
            {
                this.caret.PrepareBeforeRender();
                hwndRenderTarget.BeginDraw();

                this.RenderToRenderTarget(0, this.visualLines.Count - 1, hwndRenderTarget);

                IntPtr windowDC = hwndRenderTarget.GdiInteropRenderTarget.GetDC(DCInitializeMode.Copy);
                IntPtr compatibleDC = CreateCompatibleDC(windowDC);

                int nWidth = (int)Math.Ceiling(hwndRenderTarget.Size.Width * d2dFactory.DesktopDpi.X / 96.0);
                int nHeight = (int)Math.Ceiling(hwndRenderTarget.Size.Height * d2dFactory.DesktopDpi.Y / 96.0);
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
                this.caret.UnprepareAfterRender();
            }
            catch
            {                
                this.RecoverFromRenderException();
            }

            System.Windows.Media.Imaging.BitmapSource bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                 hBitmap,
                 IntPtr.Zero,
                 Int32Rect.Empty,
                 System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()
             );

            return bmpSource;
        }
        #endregion

        #region Accessors

        internal int VisualLineCount
        {
            get { return this.visualLines == null ? 0 : this.visualLines.Count; }
        }
        
        internal int FirstVisibleOrdinal() 
        {
            int firstVisibleLine = this.FirstVisibleLine();
            if (firstVisibleLine >= 0)
            {
                // We have a cached line that is visible on screen
                return visualLines[firstVisibleLine].BeginOrdinal;
            }

            return this.pageBeginOrdinal;
        }

        internal int FirstVisibleLine()
        {
            double screenBottom = this.scrollOffset.Height + this.renderHost.ActualHeight;
            for (int i = 0; i < this.VisualLineCount; i++)
            {
                if (visualLines[i].Position.Y + this.visualLines[i].Height > this.scrollOffset.Height && visualLines[i].Position.Y < screenBottom)
                {
                    // We have a cached line that is visible on screen
                    return i;
                }
            }

            return -1;
        }

        internal int LastVisibleLine()
        {
            double screenBottom = this.scrollOffset.Height + this.renderHost.ActualHeight;
            for (int i = this.VisualLineCount - 1; i >= 0; i--)
            {
                if (visualLines[i].Position.Y < screenBottom && (visualLines[i].Position.Y + this.visualLines[i].Height) > this.scrollOffset.Height)
                {
                    // We have a cached line that is visible on screen
                    return i;
                }
            }

            return -1;
        }

        internal int MaxContentLines 
        { 
            set 
            { 
                if (this.contentLineManager != null) 
                {
                    this.contentLineManager.MaxContentLines = value;
                    this.LeftMargin = this.contentLineManager.LayoutWidth(this.textLayoutBuilder.AverageDigitWidth());
                }
            } 
        }

        internal int LeftMargin
        {
            get { return this.leftMargin; }
            set
            {
                if (this.leftMargin != value)
                {
                    this.leftMargin = value;

                    // Need to update all lines and recompute scroll bounds.
                    double maxVisualLineWidth;
                    int changeStart, changeEnd;
                    this.UpdateVisualLines(/*visualLineStartIndex*/ 0, /*forceRelayout*/ true, out maxVisualLineWidth, out changeStart, out changeEnd);
                    this.UpdateCaret(this.caret.Ordinal);
                    this.scrollBoundsManager.InitializeVerticalScrollBounds(this.AvailbleWidth);

                    // Render the newly shifted lines.
                    this.renderHost.InvalidateVisual();
                }
            }
        }

        internal float AvailbleWidth
        {
            get { return (float)renderHost.ActualWidth - this.LeftMargin; }
        }

        internal float AvailableHeight
        {
            get { return (float)renderHost.ActualHeight; }
        }

        internal int CaretOrdinal { get { return this.caret != null ? this.caret.Ordinal : Document.UNDEFINED_ORDINAL; } }
        public int SelectionBegin   { get { return this.selectionManager.GetSelectionBeginOrdinal(); } }
        public int SelectionEnd     { get { return this.selectionManager.GetSelectionEndOrdinal(); } }

        #endregion

        #region Exposed Events

        void selectionManager_SelectionChange()
        {
            if (this.SelectionChange != null)
                this.SelectionChange();
        }

        public event SelectionChangeEventHandler SelectionChange;

        public delegate void Caret_PositionChanged(int lineNumber, int columnNumber);

        public event Caret_PositionChanged CaretPositionChanged;
 
        void caret_CaretPositionChanged()
        {
            if (this.CaretPositionChanged != null && this.contentLineManager != null)
            {
                int lineNumber = this.contentLineManager.GetLineNumber(this.document, this.CaretOrdinal);
                int lineBeginOrdinal = this.contentLineManager.GetBeginOrdinal(this.document, lineNumber);
                int columnNumber = this.document.GetOrdinalCharacterDelta(lineBeginOrdinal, this.CaretOrdinal);
                this.CaretPositionChanged(lineNumber + 1, columnNumber);
            }
        }

        public bool HasSeenNonAsciiCharacters
        {
            get
            {
                return this.scrollBoundsManager.HasSeenNonAsciiCharacters || this.textLayoutBuilder.HasSeenNonAsciiCharacters;
            }
        }
        #endregion

        #region Syntax Highlighting

        void LanguageDetector_LanguageChange(SyntaxHighlighting.SyntaxHighlighterService syntaxHighlightingService)
        {
            this.syntaxHighlightingService = syntaxHighlightingService;
            if (this.hwndRenderTarget != null)
            {
                this.syntaxHighlightingService.InitDisplayResources(this.hwndRenderTarget);
                this.NotifyOfSettingsChange(/*recreateRenderTarget*/false);
            }
        }

        #endregion

        #region Member Data
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
        ScrollBoundsManager          scrollBoundsManager;
        ContentLineManager           contentLineManager;
        SyntaxHighlighterService     syntaxHighlightingService;

        int                          pageBeginOrdinal;
        double                       pageTop;

        // Cache of sequential visual lines such that the last line has a hard break and
        // that all the lines on screen are present in the collection.
        List<VisualLine>             visualLines;

        long                         lastMouseWheelTime;
        int                          leftMargin;

        UndoRedoManager              undoRedoManager;
        FlightRecorder               flightRecorder;
        #endregion
    }
}
