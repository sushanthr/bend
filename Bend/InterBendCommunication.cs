using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Bend
{
    internal class InterBendCommunication
    {
        internal InterBendCommunication(HwndSource hwndSource)
        {
            hwndSource.AddHook(HandleMessages);
        }

        #region Windows API

        /// <summary>
        /// Window message values, WM_*
        /// </summary>
        internal enum WM
        {
            NULL = 0x0000,
            COPYDATA = 0x004A
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public UInt32 dwData;
            public int cbData;
            public IntPtr lpData;
        }
        // Depending on the message, callers may want to call GetLastError based on the return value.
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, WM Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        #endregion

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            WM message = (WM)msg;
            switch (message)
            {
                case WM.COPYDATA:
                    return HandleCopyData(message, wParam, lParam, out handled);
                default:
                    return IntPtr.Zero;
            }
        }

        private IntPtr HandleCopyData(WM uMsg, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            COPYDATASTRUCT copyDataStruct = (COPYDATASTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));

            if (copyDataStruct.dwData == MAGIC_NUMBER)
            {
                string file = System.Runtime.InteropServices.Marshal.PtrToStringUni(copyDataStruct.lpData);
                int enforceLength = (int)(copyDataStruct.cbData / 2);
                file = file.Substring(0, enforceLength);

                NotifyOfFileNameRecieved(file);
                handled = true;
            }
            else
            {
                handled = false;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        ///     Finds other instances of the same application
        /// </summary>
        /// <param name="hWnd">Window handle for the other application</param>
        /// <returns>True if another instance exists</returns>
        internal static bool FindOtherApplicationInstance(out IntPtr hWnd)
        {
            string appName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            System.Diagnostics.Process[] otherBends = System.Diagnostics.Process.GetProcessesByName(appName);

            for (int i = 0; i < otherBends.Length; i++)
            {
                hWnd = otherBends[i].MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                    return true;
            }

#if DEBUG
            appName = appName + ".vshost";
            otherBends = System.Diagnostics.Process.GetProcessesByName(appName);

            for (int i = 0; i < otherBends.Length; i++)
            {
                hWnd = otherBends[i].MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                    return true;
            }
#endif

            hWnd = IntPtr.Zero;
            return false;
        }               

        internal const int MAGIC_NUMBER = 202020;
        internal static void SendFileNameToHwnd(IntPtr hWnd, string file)
        {
            IntPtr lpData = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(file);
            COPYDATASTRUCT copyDataStruct = new COPYDATASTRUCT();
            copyDataStruct.dwData = MAGIC_NUMBER;
            copyDataStruct.cbData = file.Length * 2;
            copyDataStruct.lpData = lpData;
            IntPtr lpStruct = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(copyDataStruct));
            System.Runtime.InteropServices.Marshal.StructureToPtr(copyDataStruct, lpStruct, false);
            SendMessage(hWnd, WM.COPYDATA, IntPtr.Zero, lpStruct);
            SetForegroundWindow(hWnd);
        }

        internal void NotifyOfFileNameRecieved(string fileName)
        {
            if (this.RecivedFileNameEvent != null)
                this.RecivedFileNameEvent(fileName);
        }

        internal delegate void RecivedFileNameEventHandler(string fileName);
        internal event RecivedFileNameEventHandler RecivedFileNameEvent;
    }
}
