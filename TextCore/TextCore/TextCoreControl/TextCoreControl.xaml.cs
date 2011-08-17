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
            this.undoRedoManager = new UndoRedoManager(this.document);
            this.document.UndoRedoManager = this.undoRedoManager;
            this.displayManager = new DisplayManager(this.RenderHost, document, vScrollBar, hScrollBar);
            this.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(TextControlUserControl_PreviewKeyDown);
        }

        void TextControlUserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.Z:
                    if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        this.Undo();
                        e.Handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Y:
                    if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        this.Redo();
                        e.Handled = true;
                    }
                    break;
            }
        }

        public void LoadFile(string fullFilePath)
        {
            document.LoadFile(fullFilePath);
            RenderHost.InvalidateVisual();
        }

        public void SaveFile(string fullFilePath)
        {
            document.SaveFile(fullFilePath);
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

        public void Undo()
        {
            this.undoRedoManager.Undo();
        }

        public void Redo()
        {
            this.undoRedoManager.Redo();
        }

        private Document document;
        private DisplayManager displayManager;
        private UndoRedoManager undoRedoManager;
    }
}
