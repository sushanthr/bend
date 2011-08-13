using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UITesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UITest.Extension;
using Keyboard = Microsoft.VisualStudio.TestTools.UITesting.Keyboard;


namespace Test
{
    /// <summary>
    /// Summary description for CodedUITest2
    /// </summary>
    [CodedUITest]
    public class ScrollTest
    {
        public ScrollTest()
        {
        }

        [TestMethod]
        public void BasicScrollTest()
        {
            this.UIMap.UnblinkCaret();

            // To generate code for this test, select "Generate Code for Coded UI Test" from the shortcut menu and select one of the menu items.
            // For more information on generated code, see http://go.microsoft.com/fwlink/?LinkId=179463
            this.UIMap.LaunchTextCoreDemo();
            this.UIMap.OpenSample2XML();
            this.UIMap.AssertVScrollBarExists();
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("OpenSample2XML");

            this.UIMap.ClickVScrollBarDown();
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollLineDown");

            this.UIMap.DragVScrollBarThumb(250);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollEnd1");

            this.UIMap.DragVScrollBarThumb(-250);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollUp1");

            this.UIMap.ResizeWindowX(-200);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ResizeSample2XML1");

            this.UIMap.DragVScrollBarThumb(250);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollEnd2");

            this.UIMap.DragVScrollBarThumb(-250);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollUp2");

            this.UIMap.ResizeWindowX(-300);

            this.UIMap.DragVScrollBarThumb(250);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollEnd3");

            this.UIMap.DragVScrollBarThumb(-250);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollUp3");

            this.UIMap.ResizeWindowX(500);
            this.UIMap.DragVScrollBarThumb(250);
            this.UIMap.ResizeWindowY(50);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollEnd4");

            this.UIMap.DragVScrollBarThumb(-300);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollUp4");

            this.UIMap.DragVScrollBarThumb(300);
            this.UIMap.ResizeWindowY(50);
            this.UIMap.ResizeWindowX(-100);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollEnd5");

            this.UIMap.DragVScrollBarThumb(-350);
            System.Threading.Thread.Sleep(200);
            this.UIMap.CaptureVerify("ScrollUp5");

            this.UIMap.Close();

            this.UIMap.BlinkCaret();
        }

        #region Additional test attributes

        // You can use the following additional attributes as you write your tests:

        ////Use TestInitialize to run code before running each test 
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{        
        //    // To generate code for this test, select "Generate Code for Coded UI Test" from the shortcut menu and select one of the menu items.
        //    // For more information on generated code, see http://go.microsoft.com/fwlink/?LinkId=179463
        //}

        ////Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{        
        //    // To generate code for this test, select "Generate Code for Coded UI Test" from the shortcut menu and select one of the menu items.
        //    // For more information on generated code, see http://go.microsoft.com/fwlink/?LinkId=179463
        //}

        #endregion

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }
        private TestContext testContextInstance;

        public UIMap UIMap
        {
            get
            {
                if ((this.map == null))
                {
                    this.map = new UIMap();
                }

                return this.map;
            }
        }

        private UIMap map;
    }
}
