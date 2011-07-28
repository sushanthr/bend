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
    
    
    public partial class UIMap
    {
        public const bool ShouldGeneratedBaseline = false;
        public const string TestDataDirectory = "D:\\assembla\\trunk\\TextCore\\TextCore\\Test\\Data\\";

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
    }
}
