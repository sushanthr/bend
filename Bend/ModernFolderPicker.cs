using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Bend
{
    internal static class ModernFolderPicker
    {
        private const uint FosPickFolders = 0x00000020;
        private const uint FosForceFileSystem = 0x00000040;
        private const uint FosPathMustExist = 0x00000800;
        private const uint SigDnFileSystemPath = 0x80058000;
        private const int ErrorCancelled = unchecked((int)0x800704C7);

        internal static bool TryShow(Window owner, string initialPath, out string selectedPath)
        {
            selectedPath = null;
            IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialogClass();
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FosPickFolders | FosForceFileSystem | FosPathMustExist);
                dialog.SetTitle("Open Folder");
                dialog.SetOkButtonLabel("Select Folder");

                IShellItem initialFolder = null;
                if (!string.IsNullOrWhiteSpace(initialPath))
                {
                    Guid shellItemId = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref shellItemId, out initialFolder) == 0)
                    {
                        dialog.SetFolder(initialFolder);
                        Marshal.FinalReleaseComObject(initialFolder);
                    }
                }

                int result = dialog.Show(new WindowInteropHelper(owner).Handle);
                if (result == ErrorCancelled)
                    return false;
                Marshal.ThrowExceptionForHR(result);

                IShellItem item;
                dialog.GetResult(out item);
                try
                {
                    IntPtr pathPointer;
                    item.GetDisplayName(SigDnFileSystemPath, out pathPointer);
                    try { selectedPath = Marshal.PtrToStringUni(pathPointer); }
                    finally { Marshal.FreeCoTaskMem(pathPointer); }
                }
                finally { Marshal.FinalReleaseComObject(item); }
                return !string.IsNullOrEmpty(selectedPath);
            }
            finally
            {
                Marshal.FinalReleaseComObject(dialog);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string path, IntPtr bindContext, ref Guid shellItemId, out IShellItem shellItem);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialogClass { }

        [ComImport]
        [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr owner);
            void SetFileTypes(uint count, IntPtr filterSpec);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, uint alignment);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int errorCode);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
            void GetResults(out IntPtr items);
            void GetSelectedItems(out IntPtr items);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr interfacePointer);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint displayNameType, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem item, uint hint, out int order);
        }
    }
}
