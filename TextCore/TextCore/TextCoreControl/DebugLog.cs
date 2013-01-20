using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TextCoreControl
{
    static class DebugLog
    {
        static DebugLog()
        {
            string tempFileName = Path.GetTempFileName();
            tempFileStream = File.CreateText(tempFileName);
            tempFileStream.AutoFlush = true;
        }
        
        internal static void Write(string data)
        {
            tempFileStream.WriteLine(DateTime.Now.ToString() + " " + data);
        }

        internal static void Flush()
        {
            tempFileStream.Close();
        }

        private static StreamWriter tempFileStream;
    }
}
