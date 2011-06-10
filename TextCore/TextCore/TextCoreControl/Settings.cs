using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    static class Settings
    {
        static Settings()
        {
            // Create the DWrite Factory
            Settings.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            Settings.defaultTextFormat = dwriteFactory.CreateTextFormat("Consolas", 14);
            autoWrap = true;
        }

        public static TextFormat defaultTextFormat;
        public static DWriteFactory dwriteFactory;
        public static bool autoWrap;
    }
}
