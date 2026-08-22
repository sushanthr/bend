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
            try
            {
                LogFilePath = Path.Combine(Path.GetTempPath(), "TextCore.log");
                tempFileStream = new StreamWriter(LogFilePath, true, Encoding.UTF8);
                tempFileStream.AutoFlush = true;
            }
            catch
            {
                tempFileStream = null;
            }
        }
        
        internal static void Write(string data)
        {
            string message = DateTime.Now.ToString("O") + " " + data;
            System.Diagnostics.Trace.WriteLine(message);
            try
            {
                if (tempFileStream != null)
                    tempFileStream.WriteLine(message);
            }
            catch
            {
                // Diagnostics must never crash the editor.
            }
        }

        internal static void Write(Exception exception)
        {
            Write(exception == null ? "Unknown exception" : exception.ToString());
        }

        internal static void Flush()
        {
            if (tempFileStream != null)
                tempFileStream.Flush();
        }

        internal static string LogFilePath { get; private set; }

        private static StreamWriter tempFileStream;
    }
}
