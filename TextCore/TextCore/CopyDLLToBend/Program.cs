using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CopyDLLToBend
{
    class Program
    {
        static void Main(string[] args)
        {
            File.SetAttributes("..\\..\\..\\..\\..\\Bend\\Dll\\TextCoreControl.dll", FileAttributes.Normal);
            File.Copy("TextCoreControl.dll", "..\\..\\..\\..\\..\\Bend\\Dll\\TextCoreControl.dll", true);
            File.SetAttributes("..\\..\\..\\..\\..\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.Controls.dll", FileAttributes.Normal);
            File.Copy("Microsoft.WindowsAPICodePack.DirectX.Controls.dll", "..\\..\\..\\..\\..\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.Controls.dll", true);
            File.SetAttributes("..\\..\\..\\..\\..\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.dll", FileAttributes.Normal);
            File.Copy("Microsoft.WindowsAPICodePack.DirectX.dll", "..\\..\\..\\..\\..\\Bend\\Dll\\Microsoft.WindowsAPICodePack.DirectX.dll", true);
        }
    }
}
