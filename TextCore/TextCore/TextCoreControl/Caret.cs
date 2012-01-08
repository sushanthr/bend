using System;
using System.Windows;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace TextCoreControl
{
    internal class Caret
    {
        internal static volatile bool DBG_CARET_IS_PREPARED_FOR_RENDER;

        public Caret(HwndRenderTarget renderTarget, int defaultHeight)
        {
            this.caretHeight = defaultHeight;
            windowHandle = renderTarget.WindowHandle;
            isCaretHidden = true;
            DBG_CARET_IS_PREPARED_FOR_RENDER = true;

            xPos = 0;
            yPos = 0;
        }

        ~Caret()
        {
            DestroyCaret();
        }

        #region WIN32 API references
        [DllImport("User32.dll")]
        static extern bool CreateCaret(IntPtr hWnd, int hBitmap, int nWidth, int nHeight);

        [DllImport("User32.dll")]
        static extern bool SetCaretPos(int x, int y);

        [DllImport("User32.dll")]
        static extern bool DestroyCaret();

        [DllImport("User32.dll")]
        static extern bool ShowCaret(IntPtr hWnd);

        [DllImport("User32.dll")]
        static extern bool HideCaret(IntPtr hWnd);
        #endregion

        #region Caret position manipulation

        public void MoveCaretToLine(VisualLine visualLine, Document document, SizeF scrollOffset, int ordinal)
        {
            System.Diagnostics.Debug.WriteLine("Moving caret to ordinal " + ordinal);
            float x = visualLine.CharPosition(document, ordinal);
            int lineHeight = (int)Math.Ceiling(visualLine.Height);
            if (lineHeight != caretHeight || ordinal == 0)
            {
                this.caretHeight = lineHeight;
                DestroyCaret();
                CreateCaret(windowHandle, 0, 0, caretHeight);

                if (!this.isCaretHidden)
                {
                    ShowCaret(windowHandle);
                }
            }

            xPos = (int)(visualLine.Position.X + x - scrollOffset.Width);
            yPos = (int) (visualLine.Position.Y - scrollOffset.Height);

            SetCaretPos(xPos, yPos);
 
            this.ordinal = ordinal;
            System.Diagnostics.Debug.Assert(this.ordinal >= 0 && this.ordinal < Document.UNDEFINED_ORDINAL);
        }

        public enum CaretStep
        {
            LineUp,
            LineDown
        }

        public void MoveCaretVertical(List<VisualLine> visualLines, Document document, SizeF scrollOffset, CaretStep caretStep) 
        {
            if (visualLines.Count > 1)
            {
                if (this.Ordinal != Document.UNDEFINED_ORDINAL)
                {
                    for (int i = 0; i < visualLines.Count; i++)
                    {
                        VisualLine vl = visualLines[i];
                        if (vl.BeginOrdinal <= this.Ordinal && vl.NextOrdinal > this.Ordinal)
                        {
                            // Caret is on index i, either move up or down.
                            if (caretStep == CaretStep.LineUp)
                            {
                                if (i > 0) i--;
                                else break;
                            }
                            else if (caretStep == CaretStep.LineDown)
                            {
                                if (i + 1 < visualLines.Count) i++;
                                else break;
                            }

                            VisualLine newVl = visualLines[i];
                            uint offset;
                            newVl.HitTest(new Point2F(xPos, 0), out offset);
                            int newOrdinal = document.NextOrdinal(newVl.BeginOrdinal, offset);
                            if (newOrdinal >= newVl.NextOrdinal)
                            {
                                if (newVl.NextOrdinal == Document.UNDEFINED_ORDINAL)
                                    newOrdinal = document.LastOrdinal();
                                else
                                    newOrdinal = document.PreviousOrdinal(newVl.NextOrdinal, 1);
                            }

                            if (newOrdinal != Document.UNDEFINED_ORDINAL)
                            {
                                this.MoveCaretToLine(newVl, document, scrollOffset, newOrdinal);
                            }
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Content change

        public void Document_OrdinalShift(Document document, int beginOrdinal, int shift)
        {
            if (this.Ordinal > beginOrdinal)
            {
                if (shift > 0)
                {
                    this.ordinal = document.NextOrdinal(this.ordinal, (uint)shift);
                }
                else
                {
                    this.ordinal = document.PreviousOrdinal(this.ordinal, (uint)(-1 * shift));
                }
            }
        }

        #endregion

        #region Caret focus events
        public void OnGetFocus()
        {
            // Create a solid black caret. 
            CreateCaret(windowHandle, 0, 0, this.caretHeight);

            // Adjust the caret position, in client coordinates. 
            SetCaretPos(xPos, yPos + 1);

            // Display the caret. 
            ShowCaret(windowHandle);
            isCaretHidden = false;
        }

        public void OnLostFocus()
        {
            HideCaret(windowHandle);
            isCaretHidden = true;
            DestroyCaret();
        }
        #endregion

        #region Show / Hide caret

        public void PrepareBeforeRender()
        {
            HideCaret(windowHandle);
            DBG_CARET_IS_PREPARED_FOR_RENDER = true;
        }

        public void UnprepareAfterRender()
        {
            if (!this.isCaretHidden)
            {
                ShowCaret(windowHandle);
            }
            DBG_CARET_IS_PREPARED_FOR_RENDER = false;
        }

        #endregion

        #region Accessors

        public int Ordinal
        {
            get { return this.ordinal; }
        }

        public Point2F PositionInScreenCoOrdinates()
        {
            return new Point2F(xPos, yPos + caretHeight / 2);
        }

        #endregion

        #region Member data

        int ordinal;
        int caretHeight;
        int xPos;
        int yPos;
        IntPtr windowHandle;
        bool isCaretHidden;

        #endregion
    }
}
