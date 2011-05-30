using System;
using System.Windows;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using System.Runtime.InteropServices;

namespace TextCoreControl
{
    public class Caret
    {
        [DllImport("gdi32.dll")]
        public static extern int SetROP2(IntPtr hdc, int ops);

        [DllImport("gdi32.dll")]
        static extern bool Rectangle(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public extern static uint GetCaretBlinkTime();

        public Caret(HwndRenderTarget renderTarget, float defaultHeight)
        {
            this.renderTarget = renderTarget;
            this.caretRect = new RectF(0, 0, 2, defaultHeight);
            caretTimer = new System.Timers.Timer(GetCaretBlinkTime());
            caretTimer.Elapsed += new System.Timers.ElapsedEventHandler(caretTimer_Elapsed);
            this.isInvertedState = false;
            this.ordinal = Document.UNDEFINED_ORDINAL;
            caretTimer.Start();
        }

        public void MoveCaret(VisualLine visualLine, Document document, int ordinal)
        {
            lock (this.renderTarget)
            {
                if (isInvertedState)
                {
                    this.InvertCaretRect();
                }

                float x = visualLine.CharPosition(document, ordinal);
                this.caretRect = new RectF(visualLine.Position.X + x, visualLine.Position.Y, visualLine.Position.X + x + 2, visualLine.Position.Y + visualLine.Height);
                this.ordinal = ordinal;
            }
        }

        private void InvertCaretRect()
        {
            this.isInvertedState = !this.isInvertedState;

            renderTarget.BeginDraw();

            IntPtr dc = renderTarget.GdiInteropRenderTarget.GetDC(DCInitializeMode.Copy);

            SetROP2(dc, /*R2_NOT*/ 6);

            Rectangle(dc, (int)caretRect.Left, (int)caretRect.Top, (int)caretRect.Right, (int)caretRect.Bottom);

            renderTarget.GdiInteropRenderTarget.ReleaseDC();

            renderTarget.EndDraw();
        }

        void caretTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // do the rendering
            lock (this.renderTarget)
            {
                this.InvertCaretRect();
            }
        }

        public int Ordinal
        {
            get { return this.ordinal; }
        }

        RectF caretRect;
        HwndRenderTarget renderTarget;
        System.Timers.Timer caretTimer;
        int ordinal;
        bool isInvertedState;
    }
}
