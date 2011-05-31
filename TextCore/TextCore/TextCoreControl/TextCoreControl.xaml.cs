using System;
using System.Windows;
using System.Windows.Controls;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.WindowsImagingComponent;

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

        public void Rasterize()
        {
            System.Windows.Media.Imaging.BitmapSource bitmap = displayManager.Rasterize();
            RasterHost.Source = bitmap;
            RasterHost.Visibility = System.Windows.Visibility.Visible;
            RenderHost.Visibility = System.Windows.Visibility.Hidden;
        }


        public void UnRasterize()
        {
            RenderHost.Visibility = System.Windows.Visibility.Visible;
            RasterHost.Visibility = System.Windows.Visibility.Hidden;
        }

        private Document document;
        private DisplayManager displayManager;
    }
}
