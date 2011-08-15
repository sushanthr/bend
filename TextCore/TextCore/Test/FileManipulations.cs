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
    /// Summary description for CodedUITest1
    /// </summary>
    [CodedUITest]
    public class FileManipulations
    {
        public FileManipulations()
        {
        }

        [TestMethod]
        public void FileManipulation()
        {
            // To generate code for this test, select "Generate Code for Coded UI Test" from the shortcut menu and select one of the menu items.
            // For more information on generated code, see http://go.microsoft.com/fwlink/?LinkId=179463
            this.UIMap.OpenSampleText();
            this.UIMap.OpenSample2XML();


            System.IO.File.Delete(UIMap.TestDataDirectory + "Current\\SampleTextSave.txt");
            System.Diagnostics.Debug.Assert(!System.IO.File.Exists(UIMap.TestDataDirectory + "Current\\SampleTextSave.txt"));
            this.UIMap.EditSampleTextAndSave();
            System.Diagnostics.Debug.Assert(System.IO.File.Exists(UIMap.TestDataDirectory + "Current\\SampleTextSave.txt"));

            string savedFileContent = System.IO.File.ReadAllText(UIMap.TestDataDirectory + "Current\\SampleTextSave.txt");
            if (UIMap.ShouldGeneratedBaseline)
            {
                System.IO.File.WriteAllText(UIMap.TestDataDirectory + "Baseline\\SampleTextSave.txt", savedFileContent);
            }
            else
            {
                string baselineFileContent = System.IO.File.ReadAllText(UIMap.TestDataDirectory + "Baseline\\SampleTextSave.txt");
                System.Diagnostics.Debug.Assert(savedFileContent == baselineFileContent);
            }

            this.UIMap.Close();
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
