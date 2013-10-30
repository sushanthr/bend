namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Input;
    using System.CodeDom.Compiler;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UITest.Extension;
    using Microsoft.VisualStudio.TestTools.UITesting;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.VisualStudio.TestTools.UITesting.WinControls;
    using Keyboard = Microsoft.VisualStudio.TestTools.UITesting.Keyboard;
    using Mouse = Microsoft.VisualStudio.TestTools.UITesting.Mouse;
    using MouseButtons = System.Windows.Forms.MouseButtons;
    using System.Runtime.InteropServices;
    
    public partial class UIMap
    {
        public const bool ShouldGeneratedBaseline = false;
        public const string TestDataDirectory = "D:\\Projects\\sushanth.assembla.com\\trunk\\TextCore\\TextCore\\Test\\Data\\";
        private uint caretBlinkTime;

        public void CaptureVerify(string testName)
        {
            WinClient uIRenderHostClient = this.UIMainWindowWindow1.UIRenderHostPane.UIRenderHostClient;
            Image currentJpegImage = uIRenderHostClient.CaptureImage();
            if (UIMap.ShouldGeneratedBaseline)
            {
                currentJpegImage.Save(UIMap.TestDataDirectory + "Baseline\\" + testName + ".jpg");
            }
            else
            {
                currentJpegImage.Save(UIMap.TestDataDirectory + "Current\\" + testName + ".jpg");
                Bitmap currentImage = new Bitmap(currentJpegImage);
                Bitmap originalImage = new Bitmap(Image.FromFile(UIMap.TestDataDirectory + "Baseline\\" + testName + ".jpg"));

                bool areImagesEqual = true;
                if (currentImage.Width == originalImage.Width && currentImage.Height == originalImage.Height)
                {
                    for (int h = 0; h < currentImage.Height; h++)
                    {
                        for (int w = 0; w < currentImage.Width; w++)
                        {
                            if (originalImage.GetPixel(w, h) != currentImage.GetPixel(w, h))
                            {
                                areImagesEqual = false;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    areImagesEqual = false;
                }

                System.Diagnostics.Debug.Assert(areImagesEqual, "Image comparision failure in test case " + testName);
            }
        }

        public void ClickVScrollBarDown()
        {
            for (int i = 0; i < 5; i++)
            {
                Mouse.Click(this.UIMainWindowWindow.UITextEditorCustom.UIVScrollBarScrollBar.UIPageDownButton, new Point(5, 150));
            }
        }

        public void DragVScrollBarThumb(int amount)
        {
            Mouse.StartDragging(this.UIMainWindowWindow.UITextEditorCustom.UIVScrollBarScrollBar.UIItemIndicator, new Point(0, 0));
            Mouse.StopDragging(this.UIMainWindowWindow.UITextEditorCustom.UIVScrollBarScrollBar.UIItemIndicator, 0, amount);
        }

        public void ResizeWindowX(int x)
        {
            // get the mouse to the end
            Mouse.Click(new Point(this.UIMainWindowWindow.BoundingRectangle.Right - 5, this.UIMainWindowWindow.BoundingRectangle.Bottom - 5));
            Mouse.StartDragging();
            Mouse.StopDragging(new Point(this.UIMainWindowWindow.BoundingRectangle.Right + x, this.UIMainWindowWindow.BoundingRectangle.Bottom - 5));
        }

        public void ResizeWindowY(int y)
        {
            // get the mouse to the end
            Mouse.Click(new Point(this.UIMainWindowWindow.BoundingRectangle.Right - 5, this.UIMainWindowWindow.BoundingRectangle.Bottom - 5));
            Mouse.StartDragging();
            Mouse.StopDragging(new Point(this.UIMainWindowWindow.BoundingRectangle.Right - 5, this.UIMainWindowWindow.BoundingRectangle.Bottom  + y));
        }

        [DllImport("user32.dll")]
        static extern bool SetCaretBlinkTime(uint uMSeconds);

        [DllImport("user32.dll")]
        static extern uint GetCaretBlinkTime();

        public void UnblinkCaret()
        {
            this.caretBlinkTime = GetCaretBlinkTime();
            SetCaretBlinkTime(9999);
        }

        public void BlinkCaret()
        {
            if (this.caretBlinkTime != 0)
            {
                SetCaretBlinkTime(this.caretBlinkTime);
            }
        }
    }
}
