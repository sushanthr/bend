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
            this.flightRecorder = new FlightRecorder(this);
            this.displayManager = new DisplayManager(this.RenderHost, document, vScrollBar, hScrollBar, undoRedoManager, flightRecorder);
            this.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(TextControl_PreviewKeyDown);
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

        private void TextControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool handled;
            this.TextControl_PreviewKeyDown(e.Key, e.KeyboardDevice.Modifiers, out handled);
            e.Handled = handled;
        }

        internal void TextControl_PreviewKeyDown(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifier, out bool handled)
        {
            handled = false;
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.TextEdtiorPreviewKeyDownFlightEvent(key, modifier));
            }

            switch (key)
            {
                case System.Windows.Input.Key.Z:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        this.Undo();
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Y:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        this.Redo();
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.X:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.Cut(this);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.C:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.Copy(this);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.V:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.Paste(this);
                        }
                        handled = true;
                    }
                    else if (modifier == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.PasteNextRingItem(this);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Insert:
                    if (modifier == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                    {
                        if (this.copyPasteManager != null)
                        {
                            this.copyPasteManager.PasteNextRingItem(this);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.F9:
                    this.flightRecorder.TakeSnapshot();                    
                    handled = true;
                    break;
            }
        }

        public void LoadFile(string fullFilePath)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.LoadFileFlightEvent(fullFilePath));
            }
            document.LoadFile(fullFilePath);
            RenderHost.InvalidateVisual();
        }

        public void SaveFile(string fullFilePath)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.SaveFileFlightEvent(fullFilePath));
            }
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
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ReplaceTextFlightEvent(index, length, newText));
            }

            this.undoRedoManager.BeginTransaction();
            this.document.DeleteAt(index, length);
            this.document.InsertAt(index, newText);
            this.undoRedoManager.EndTransaction();
        }

        public int ReplaceAllText(string findText, string replaceText, bool matchCase, bool useRegEx, bool replaceInBackgroundHighlightRange)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ReplaceAllTextFlightEvent(findText, replaceText, matchCase, useRegEx, replaceInBackgroundHighlightRange));
            }
            this.undoRedoManager.BeginTransaction();
            int beginOrdinal = Document.UNDEFINED_ORDINAL;
            int endOrdinal = Document.UNDEFINED_ORDINAL;
            if (replaceInBackgroundHighlightRange)
            { 
                this.displayManager.GetBackgroundHighlightRange(out beginOrdinal, out endOrdinal);
            }
            int count = this.document.ReplaceAllText(findText, replaceText, matchCase, useRegEx, beginOrdinal, endOrdinal);
            this.undoRedoManager.EndTransaction();
            return count;
        }

        public void ReplaceWithRegexAtOrdinal(string findText, string replaceText, bool matchCase, int beginOrdinal)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ReplaceWithRegexAtOrdinalFlightEvent(findText, replaceText, matchCase, beginOrdinal));
            }

            this.undoRedoManager.BeginTransaction();
            this.document.ReplaceWithRegexAtOrdinal(findText, replaceText, matchCase, beginOrdinal);
            this.undoRedoManager.EndTransaction();
        }

        public void Select(int index, uint length)
        {
            if (this.flightRecorder.IsRecording) this.flightRecorder.AddFlightEvent(new FlightRecorder.SelectFlightEvent(index, length));
            int beginOrdinal = this.document.GetOrdinalForTextIndex(index);            
            this.displayManager.ScrollOrdinalIntoView(beginOrdinal);
            this.displayManager.SetHighlightMode(/*shouldUseHighlightColors*/ true);
            this.displayManager.SelectRange(beginOrdinal, this.document.NextOrdinal(beginOrdinal, length));
        }

        public void CancelSelect()
        {
            if (this.flightRecorder.IsRecording) this.flightRecorder.AddFlightEvent(new FlightRecorder.CancelSelectFlightEvent());
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
                int length = text.Length;
                if (length > 0)
                {
                    this.ReplaceText(selectionBeginOrdinal, length, value);
                }
            }
        }

        public void SetBackgroundHighlight(int beginOrdinal, int endOrdinal)
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.SetBackgroundHighlightFlightEvent(beginOrdinal, endOrdinal));
            }
            this.displayManager.SetBackgroundHighlight(beginOrdinal, endOrdinal);
            RenderHost.InvalidateVisual();
        }

        public void ResetBackgroundHighlight()
        {
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ResetBackgroundHighlightFlightEvent());
            }
            this.displayManager.ResetBackgroundHighlight();
            RenderHost.InvalidateVisual();
        }

        public bool IsInBackgroundHighlight(int ordinal)
        {
            return this.displayManager.IsInBackgroundHightlight(ordinal);
        }

        #region WIN32 API references

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        #endregion

        public bool SetFocus()
        {
            if (RenderHost.Visibility == System.Windows.Visibility.Visible)
            {
                IntPtr rValue = SetFocus(RenderHost.Handle);
                int error = Marshal.GetLastWin32Error();
            }
            return false;
        }

        public void PlaybackFlightRecord(string fullFilePath)
        {
            this.playbackFlightRecordFullPath = fullFilePath;
            this.flightRecorder.Playback(fullFilePath);
        }

        internal string PlaybackFlightRecordFullPath
        {
            get
            {
                return this.playbackFlightRecordFullPath;
            }
        }

        public void ExitAfterPlayback()
        {
            this.flightRecorder.ExitAfterPlayback = true;
        }

        public Document Document { get { return this.document; } }
        public DisplayManager DisplayManager { get { return this.displayManager; } }

        private Document document;
        private DisplayManager displayManager;
        private UndoRedoManager undoRedoManager;
        private CopyPasteManager copyPasteManager;
        private FlightRecorder flightRecorder;
        private string playbackFlightRecordFullPath;
    }
}
