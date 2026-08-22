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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, WM msg, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeout, out IntPtr result);

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
            handled = false;
            if (lParam == IntPtr.Zero)
                return IntPtr.Zero;

            COPYDATASTRUCT copyDataStruct = (COPYDATASTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));

            if (copyDataStruct.dwData == MAGIC_NUMBER && copyDataStruct.lpData != IntPtr.Zero &&
                copyDataStruct.cbData >= 0 && copyDataStruct.cbData % 2 == 0)
            {
                int characterCount = copyDataStruct.cbData / 2;
                string file = System.Runtime.InteropServices.Marshal.PtrToStringUni(copyDataStruct.lpData, characterCount);

                if (!string.IsNullOrWhiteSpace(file))
                {
                    NotifyOfFileNameRecieved(file);
                    handled = true;
                }
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
                if (otherBends[i].Id == System.Diagnostics.Process.GetCurrentProcess().Id)
                    continue;
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
        internal static bool SendFileNameToHwnd(IntPtr hWnd, string file)
        {
            IntPtr lpData = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(file);
            IntPtr lpStruct = IntPtr.Zero;
            try
            {
                COPYDATASTRUCT copyDataStruct = new COPYDATASTRUCT();
                copyDataStruct.dwData = MAGIC_NUMBER;
                copyDataStruct.cbData = file.Length * 2;
                copyDataStruct.lpData = lpData;
                lpStruct = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf(copyDataStruct));
                System.Runtime.InteropServices.Marshal.StructureToPtr(copyDataStruct, lpStruct, false);
                IntPtr result;
                bool sent = SendMessageTimeout(hWnd, WM.COPYDATA, IntPtr.Zero, lpStruct, 0x0002, 3000, out result) != IntPtr.Zero;
                if (sent)
                    SetForegroundWindow(hWnd);
                return sent;
            }
            finally
            {
                if (lpStruct != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(lpStruct);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(lpData);
            }
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
