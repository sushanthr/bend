using System;
using System.Windows;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using System.Runtime.InteropServices;
using System.Collections;

namespace TextCoreControl
{
    public class Caret
    {
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

        public Caret(HwndRenderTarget renderTarget, int defaultHeight)
        {
            this.caretHeight = defaultHeight;
            windowHandle = renderTarget.WindowHandle;

            xPos = 0;
            yPos = 0;
        }

        ~Caret()
        {
            DestroyCaret();
        }

        public void OnGetFocus()
        {
            // Create a solid black caret. 
            CreateCaret(windowHandle, 0, 1, caretHeight);

            // Adjust the caret position, in client coordinates. 
            SetCaretPos(xPos, yPos);

            // Display the caret. 
            ShowCaret(); 
        }

        public void OnLostFocus()
        {
            DestroyCaret();
        }

        public void MoveCaretVisual(VisualLine visualLine, Document document, int ordinal)
        {
            float x = visualLine.CharPosition(document, ordinal);
            if ((int)visualLine.Height != caretHeight || ordinal == 0)
            {
                this.caretHeight = (int) visualLine.Height;
                DestroyCaret();
                CreateCaret(windowHandle, 0, 1, caretHeight);
                ShowCaret();
            }

            xPos = (int)(visualLine.Position.X + x);
            yPos = (int) visualLine.Position.Y;

            SetCaretPos(xPos, yPos);
 
            this.ordinal = ordinal;
        }

        public void MoveCaretOrdinal(Document document, int shift)
        {
            if (shift > 0)
            {
                ordinal = document.NextOrdinal(ordinal, (uint)shift);
            }
            else
            {
                ordinal = document.PreviousOrdinal(ordinal, (uint)(-1 * shift));
            }
        }

        public void MoveCaretVertical(ArrayList visualLines, Document document, bool moveUp, bool moveDown) 
        {
            if (this.Ordinal != Document.UNDEFINED_ORDINAL)
            {
                for (int i = 0; i < visualLines.Count; i++)
                {
                    VisualLine vl = (VisualLine)visualLines[i];
                    if (vl.BeginOrdinal <= this.Ordinal && vl.NextOrdinal > this.Ordinal)
                    {
                        // Caret is on index i, either move up or down.
                        if (moveUp)
                        {
                            if (i > 0) i--;
                            else return;
                        }
                        else if (moveDown)
                        {
                            if (i + 1 < visualLines.Count) i++;
                            else return;
                        }

                        VisualLine newVl = (VisualLine)visualLines[i];
                        uint offset;
                        newVl.HitTest(new Point2F(xPos, 0), out offset);
                        int newOrdinal = document.NextOrdinal(newVl.BeginOrdinal, offset);
                        if (newOrdinal >= newVl.NextOrdinal)
                        {
                            newOrdinal = document.PreviousOrdinal(newVl.NextOrdinal, 1);
                        }

                        this.MoveCaretVisual(newVl, document, newOrdinal);
                        break;
                    }
                }
            }
        }

        public int Ordinal
        {
            get { return this.ordinal; }
        }

        public void HideCaret()
        {
            HideCaret(windowHandle);
        }

        public void ShowCaret()
        {
            ShowCaret(windowHandle);
        }

        int ordinal;
        int caretHeight;
        int xPos;
        int yPos;
        IntPtr windowHandle;
    }
}
