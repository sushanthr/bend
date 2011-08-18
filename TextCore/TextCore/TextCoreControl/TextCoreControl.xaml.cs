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
            this.copyPasteManager = null;
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
                case System.Windows.Input.Key.X:
                    if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.Cut(this);
                        }
                        e.Handled = true;
                    }
                    break;
                case System.Windows.Input.Key.C:
                    if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.Copy(this);
                        }
                        e.Handled = true;
                    }
                    break;
                case System.Windows.Input.Key.V:
                    if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.Paste(this);
                        }
                        e.Handled = true;
                    }
                    else if (e.KeyboardDevice.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.PasteNextRingItem(this);
                        }
                        e.Handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Insert:
                    if (e.KeyboardDevice.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.PasteNextRingItem(this);
                        }
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

        public CopyPasteManager CopyPasteManager
        {
            get { return this.copyPasteManager; }
            set { this.copyPasteManager = value; }
        }

        internal Document Document { get { return this.document; } }
        internal DisplayManager DisplayManager { get { return this.displayManager; } }

        private Document document;
        private DisplayManager displayManager;
        private UndoRedoManager undoRedoManager;
        private CopyPasteManager copyPasteManager;
    }
}
