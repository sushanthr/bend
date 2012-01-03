using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TransferToBend
{
    class Program
    {
        static void Main(string[] args)
        {
            File.SetAttributes("D:\\Projects\\Bend\\Dll\\TextCoreControl.dll", FileAttributes.Normal);
            File.Copy("TextCoreControl.dll", "D:\\Projects\\Bend\\Dll\\TextCoreControl.dll", true);
            File.SetAttributes("D:\\Projects\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.Controls.dll", FileAttributes.Normal);
            File.Copy("Microsoft.WindowsAPICodePack.DirectX.Controls.dll", "D:\\Projects\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.Controls.dll", true);
            File.SetAttributes("D:\\Projects\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.dll", FileAttributes.Normal);
            File.Copy("Microsoft.WindowsAPICodePack.DirectX.dll", "D:\\Projects\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.dll", true);
        }
    }
}
