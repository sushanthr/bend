using System;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.WindowsImagingComponent;

namespace TextCoreControl
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class TextEditor : UserControl
    {
        public TextEditor()
        {
            InitializeComponent();
            this.document = new Document();
            this.undoRedoManager = new UndoRedoManager(this.document);
            this.displayManager = new DisplayManager(this.RenderHost, document, vScrollBar, hScrollBar, undoRedoManager);
            this.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(TextControlUserControl_PreviewKeyDown);
            this.copyPasteManager = null;
            SetControlBackground();
        }

        private void SetControlBackground()
        {
            this.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
                (Byte)(Settings.DefaultBackgroundColor.Red * 255),
                (Byte)(Settings.DefaultBackgroundColor.Green * 255), 
                (Byte)(Settings.DefaultBackgroundColor.Blue * 255)));
            this.BottomRightPatch.Background = this.Background;
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

        public System.Windows.Media.Imaging.BitmapSource Rasterize()
        {
            System.Windows.Media.Imaging.BitmapSource bitmap = displayManager.Rasterize();
            RasterHost.Source = bitmap;
            RasterHost.Visibility = System.Windows.Visibility.Visible;
            RenderHost.Visibility = System.Windows.Visibility.Hidden;
            return bitmap;
        }

        public void UnRasterize()
        {
            RenderHost.Visibility = System.Windows.Visibility.Visible;
            RasterHost.Visibility = System.Windows.Visibility.Hidden;
            this.SetFocus();
            this.InvalidateVisual();
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

        public void NotifyOfSettingsChange()
        {
            SetControlBackground();
            this.displayManager.NotifyOfSettingsChange(/*recreateRenderTarget*/true);
        }

        public void ReplaceText(int index, int length, string newText)
        {
            this.undoRedoManager.BeginTransaction();
            this.document.DeleteAt(index, length);
            this.document.InsertAt(index, newText);
            this.undoRedoManager.EndTransaction();
        }

        public int ReplaceAllText(string findText, string replaceText, bool matchCase, bool useRegEx)
        {
            this.undoRedoManager.BeginTransaction();
            int count = this.document.ReplaceAllText(findText, replaceText, matchCase, useRegEx);
            this.undoRedoManager.EndTransaction();
            return count;
        }

        public void ReplaceWithRegexAtOrdinal(string findText, string replaceText, bool matchCase, int beginOrdinal)
        {
            this.undoRedoManager.BeginTransaction();
            this.document.ReplaceWithRegexAtOrdinal(findText, replaceText, matchCase, beginOrdinal);
            this.undoRedoManager.EndTransaction();
        }

        public void Select(int index, uint length)
        {
            int beginOrdinal = this.document.GetOrdinalForTextIndex(index);            
            this.displayManager.ScrollOrdinalIntoView(beginOrdinal);
            this.displayManager.SetHighlightMode(/*shouldUseHighlightColors*/ true);
            this.displayManager.SelectRange(beginOrdinal, this.document.NextOrdinal(beginOrdinal, length));
        }

        public void CancelSelect()
        {
            this.displayManager.SetHighlightMode(/*shouldUseHighlightColors*/ false);
            int caretOrdinal = this.displayManager.CaretOrdinal;
            if (caretOrdinal != Document.UNDEFINED_ORDINAL)
            { 
                this.displayManager.ScrollOrdinalIntoView(caretOrdinal, /*allowSmoothScroll*/true);
                this.displayManager.SelectRange(caretOrdinal, caretOrdinal);
            }
        }

        public string SelectedText
        {
            get 
            {
                int selectionBeginOrdinal;
                return this.displayManager.GetSelectedText(out selectionBeginOrdinal);
            }
            set 
            {
                int selectionBeginOrdinal;
                string text = this.displayManager.GetSelectedText(out selectionBeginOrdinal);
                this.ReplaceText(selectionBeginOrdinal, text.Length, value);                
            }
        }

        public void SetBackgroundHighlight(int beginOrdinal, int endOrdinal)
        {
            this.displayManager.SetBackgroundHighlight(beginOrdinal, endOrdinal);
            RenderHost.InvalidateVisual();
        }

        public void ResetBackgroundHighlight()
        {
            this.displayManager.ResetBackgroundHighlight();
            RenderHost.InvalidateVisual();
        }

        #region WIN32 API references

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        #endregion

        public bool SetFocus()
        {
            if (RenderHost.Visibility == System.Windows.Visibility.Visible)
            {
                SetFocus(RenderHost.Handle);
            }
            return false;
        }

        public Document Document { get { return this.document; } }
        public DisplayManager DisplayManager { get { return this.displayManager; } }

        private Document document;
        private DisplayManager displayManager;
        private UndoRedoManager undoRedoManager;
        private CopyPasteManager copyPasteManager;       
    }
}
