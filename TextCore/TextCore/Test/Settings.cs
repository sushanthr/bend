using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test
{
    class Settings
    {
        public const string TestDataDirectory = "D:\\assembla\\trunk\\TextCore\\TextCore\\Test\\Data\\";
        public const string TestAppFullPath = "D:\\assembla\\trunk\\TextCore\\TextCore\\TextCore\\bin\\Debug\\TextCore.exe";
        
        /// <summary>
        ///     Warning: Setting true here will cause all original test images to be overriden with
        ///     new images that are produced. It disables verification.
        /// </summary>
        public const bool GenerateBaseLine = false;
    }
}
