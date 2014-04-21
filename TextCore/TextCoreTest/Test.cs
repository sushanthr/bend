using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TextCoreTest
{
    [TestClass]
    public class Test
    {
        static string textCoreDemoExePath;
        static string flightRecordsPath;
        bool showLineNumber;
        bool showHUD;
        [AssemblyInitialize()]
        public static void AssemblyInit(TestContext context)
        {
            String filePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
            textCoreDemoExePath = filePath.Replace("TextCore\\TextCoreTest", "TextCore\\TextCore\\TextCore");
            textCoreDemoExePath = textCoreDemoExePath + "TextCore.exe";

            int index = filePath.LastIndexOf("bin\\");
            flightRecordsPath = filePath.Substring(0, index);
            flightRecordsPath = flightRecordsPath + "FlightRecords\\";
        }

        [TestMethod]
        public void BasicLoadFile()
        {
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord.xml");
        }
        
        [TestMethod]
        public void BasicTyping() 
        {
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord1.xml");
        }

        [TestMethod]
        public void CaretTest() 
        {
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord2.xml");
        }

        [TestMethod]
        public void AdvancedTypingTest() 
        {
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord3.xml");
        }

        [TestMethod]
        public void LineNumberTests() 
        {
            this.showHUD = true;
            this.showLineNumber = true;
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord4.xml");
            this.showHUD = false;
            this.showLineNumber = false;
        }

        [TestMethod]
        public void FileManipulations() 
        {
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord5.xml");
            string originalFile = System.IO.File.ReadAllText(flightRecordsPath + "Sample2.xml");
            string newFile = System.IO.File.ReadAllText(flightRecordsPath + "SampleTextSave.txt");
            Assert.AreEqual(originalFile, newFile, "Saved file should match original file.");
        }

        [TestMethod]
        public void ScrollingTest() 
        {
            PlaybackAndCompareFlightRecordSnapShots("FlightRecord6.xml");
        }

        private void PlaybackAndCompareFlightRecordSnapShots(string flightRecordName)
        {
            string arguments = GetArguments(flightRecordName);
            int exitCode = RunTextCoreDemo(arguments);
            Assert.IsTrue(exitCode == 0, "Image shows differences.");
        }

        private string GetArguments(string flightRecordName)
        {
            string otherArguments = "";
            if (showLineNumber)
            {
                otherArguments += "/linenumber ";
            }
            if (showHUD)
            {
                otherArguments += "/hud ";
            }
            return otherArguments + "/exit /hide /playback=\"" + flightRecordsPath + flightRecordName + "\"";
        }

        private int RunTextCoreDemo(string arguments)
        {
            System.Diagnostics.Process textCoreProcess = new System.Diagnostics.Process();
            textCoreProcess.StartInfo.FileName = textCoreDemoExePath;
            textCoreProcess.StartInfo.Arguments = arguments;
            textCoreProcess.Start();
            textCoreProcess.WaitForExit();
            return textCoreProcess.ExitCode;
        }
    }
}
