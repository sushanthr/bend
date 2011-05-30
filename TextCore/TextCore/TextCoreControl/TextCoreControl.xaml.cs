using System;
using System.Windows;
using System.Windows.Controls;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class TextControlUserControl : UserControl
    {
        public TextControlUserControl()
        {
            InitializeComponent();
            this.document = new Document();
            this.displayManager = new DisplayManager(this.RenderHost, document);
        }
        
        public void LoadFile(string fullFilePath)
        {
            document.LoadFile(fullFilePath);
            RenderHost.InvalidateVisual();
        }

        private Document document;
        private DisplayManager displayManager;
    }
}
