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
    public sealed class EditorCapabilities
    {
        public bool CanEdit { get; set; } = true;
        public bool CanCopy { get; set; } = true;
        public bool CanCut { get; set; } = true;
        public bool CanPaste { get; set; } = true;
    }
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class TextEditor : UserControl, IFindTarget
    {
        public TextEditor() : this(true) { }

        private TextEditor(bool ownsComparison)
        {
            InitializeComponent();
            this.ownsComparison = ownsComparison;
            this.document = new Document();
            this.undoRedoManager = new UndoRedoManager(this.document);
            this.flightRecorder = new FlightRecorder(this);
            this.displayManager = new DisplayManager(this.RenderHost, document, vScrollBar, hScrollBar, undoRedoManager, flightRecorder);
            this.activeCommandSurface = this;
            this.displayManager.ContextMenu += () => { if (!relayingBaseContextMenu) this.activeCommandSurface = this; };
            this.findSession = new TextFindSession(this);
            this.comparisonFindSession = new ComparisonFindSession(this, () => this.baseEditor);
            this.document.ContentChange += DiffDocument_ContentChange;
            this.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(TextControl_PreviewKeyDown);
            this.PreviewTextInput += TextControl_PreviewTextInput;
            this.displayManager.VerticalScrollChanged += DisplayManager_VerticalScrollChanged;
            this.copyPasteManager = null;
            SetControlBackground();
        }

        private void DisplayManager_VerticalScrollChanged(object sender, EventArgs e)
        {
            if (VerticalScrollChanged != null) VerticalScrollChanged(this, EventArgs.Empty);
            if (!synchronizingScroll && DiffMode == DiffViewMode.SideBySide && baseEditor != null)
            {
                if (baseEditor.DisplayManager.IsReady) SynchronizeBaseToCurrent();
                else Dispatcher.BeginInvoke(new Action(SynchronizeBaseToCurrent), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void SynchronizeBaseToCurrent()
        {
            if (synchronizingScroll || DiffMode != DiffViewMode.SideBySide || baseEditor == null || !baseEditor.DisplayManager.IsReady) return;
            synchronizingScroll = true;
            try
            {
                baseEditor.SetVerticalOffset(this.VerticalOffset);
            }
            finally { synchronizingScroll = false; }
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
            if (this.IsReadOnly && IsMutationKey(e.Key, e.KeyboardDevice.Modifiers)) { e.Handled = true; return; }
            bool handled;
            this.TextControl_PreviewKeyDown(e.Key, e.KeyboardDevice.Modifiers, out handled);
            e.Handled = handled;
        }

        private void TextControl_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (this.IsReadOnly) e.Handled = true;
        }

        private static bool IsMutationKey(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            if (key == System.Windows.Input.Key.Back || key == System.Windows.Input.Key.Delete || key == System.Windows.Input.Key.Enter || key == System.Windows.Input.Key.Tab) return true;
            return (key == System.Windows.Input.Key.V || key == System.Windows.Input.Key.X) && (modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
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
                        if (this.copyPasteManager != null && this.Capabilities.CanCut)
                        {
                            this.copyPasteManager.Cut(this);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.C:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null && this.Capabilities.CanCopy)
                        {
                            this.copyPasteManager.Copy(this);
                        }
                        handled = true;
                    }
                    break;
                case System.Windows.Input.Key.V:
                    if (modifier == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (this.copyPasteManager != null && this.Capabilities.CanPaste)
                        {
                            this.copyPasteManager.Paste(this);
                        }
                        handled = true;
                    }
                    else if (modifier == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                    {
                        if (this.copyPasteManager != null && this.Capabilities.CanPaste)
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

        public void LoadFile(string currentPath, string basePath)
        {
            LoadFile(currentPath);
            EnsureBaseEditor();
            baseEditor.LoadFile(basePath);
            comparisonFileName = currentPath;
            OnDiffBaseChanged();
        }

        public void LoadText(string text, string fileName = null)
        {
            document.LoadText(text, fileName);
            RenderHost.InvalidateVisual();
        }

        public void LoadText(string currentText, string currentFileName, string baseText)
        {
            LoadText(currentText, currentFileName);
            SetDiffBase(baseText, currentFileName);
        }

        public void SetDiffBase(string baseText, string fileName = null)
        {
            EnsureBaseEditor();
            comparisonFileName = fileName;
            comparisonBaseDocument = new Document();
            comparisonBaseDocument.LoadText(baseText ?? String.Empty, fileName);
            baseEditor.LoadText(baseText ?? String.Empty, fileName);
            OnDiffBaseChanged();
        }

        public void ClearDiffBase()
        {
            if (baseEditor != null) baseEditor.LoadText(String.Empty, comparisonFileName);
            hasDiffBase = false;
            ShowDiff(DiffViewMode.None);
            UpdateDiffToolbar();
        }

        private void EnsureBaseEditor()
        {
            if (!ownsComparison || baseEditor != null) return;
            baseEditor = new TextEditor(false) { IsReadOnly = true, WordWrapOverride = false };
            baseEditor.CopyPasteManager = this.CopyPasteManager;
            baseEditor.ConfigureAsSecondaryDiffSurface();
            baseEditor.DisplayManager.ContextMenu += BaseEditor_ContextMenu;
            baseEditor.PreviewMouseWheel += BaseEditor_PreviewMouseWheel;
            baseEditor.VerticalScrollChanged += BaseEditor_VerticalScrollChanged;
            BaseEditorHost.Content = baseEditor;
        }

        private void BaseEditor_ContextMenu()
        {
            activeCommandSurface = baseEditor;
            relayingBaseContextMenu = true;
            try { displayManager.RaiseContextMenu(); }
            finally { relayingBaseContextMenu = false; }
        }

        public void CopySelection()
        {
            TextEditor surface = activeCommandSurface ?? this;
            if (surface.SelectedText.Length == 0 && this.SelectedText.Length != 0) surface = this;
            if (surface.CopyPasteManager != null && surface.Capabilities.CanCopy)
                surface.CopyPasteManager.Copy(surface);
        }

        public void GoToLine(int lineNumber)
        {
            TextEditor surface = activeCommandSurface ?? this;
            surface.DisplayManager.ScrollToContentLineNumber(lineNumber, /*moveCaret*/ true);
        }

        private void BaseEditor_VerticalScrollChanged(object sender, EventArgs e)
        {
            if (synchronizingScroll || DiffMode != DiffViewMode.SideBySide || !displayManager.IsReady) return;
            synchronizingScroll = true;
            try
            {
                SetVerticalOffset(baseEditor.VerticalOffset);
            }
            finally { synchronizingScroll = false; }
        }

        private void ConfigureAsSecondaryDiffSurface()
        {
            vScrollBar.Visibility = Visibility.Collapsed;
            RenderHost.Margin = new Thickness(0, 0, 0, 16);
            RasterHost.Margin = new Thickness(0, 0, 0, 16);
            hScrollBar.Margin = new Thickness(0);
            BottomRightPatch.Visibility = Visibility.Collapsed;
        }

        private void BaseEditor_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (DiffMode != DiffViewMode.SideBySide || !displayManager.IsReady) return;
            displayManager.ScrollBy(e.Delta > 0 ? -Settings.MouseWheel_Normal_Step_LineCount : Settings.MouseWheel_Normal_Step_LineCount);
            e.Handled = true;
        }

        private void OnDiffBaseChanged()
        {
            hasDiffBase = true;
            WordWrapOverride = false;
            UpdateDiffToolbar();
            RefreshDiff();
            ShowDiff(diffMode == DiffViewMode.None ? DiffViewMode.Inline : diffMode);
        }

        public bool AllowEdit { get { return !IsReadOnly; } set { IsReadOnly = !value; } }
        public bool HasDiffBase { get { return hasDiffBase; } }
        public Document BaseDocument { get { return comparisonBaseDocument; } }
        public DiffAlignment CurrentDiffAlignment { get { return currentAlignment; } }
        public DiffViewMode DiffMode { get { return diffMode; } set { ShowDiff(value); } }
        // Kept for source compatibility. Diff presentation is selected by the host;
        // TextCore no longer renders a second, per-editor toolbar.
        public bool ShowDiffControls { get { return false; } set { } }

        public void ShowDiff(DiffViewMode mode)
        {
            if (!ownsComparison) return;
            if (mode != DiffViewMode.None && !hasDiffBase) mode = DiffViewMode.None;
            diffMode = mode;
            RefreshDiff();
            bool side = mode == DiffViewMode.SideBySide;
            BaseColumn.Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            ComparisonSplitterColumn.Width = side ? new GridLength(5) : new GridLength(0);
            ComparisonSplitter.Visibility = side ? Visibility.Visible : Visibility.Collapsed;
            BaseEditorHost.Visibility = side ? Visibility.Visible : Visibility.Collapsed;
            if (side) Dispatcher.BeginInvoke(new Action(SynchronizeBaseToCurrent), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateDiffToolbar()
        {
            // Diff controls belong to the host application's status bar.
        }

        private void DiffDocument_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (!hasDiffBase) return;
            if (diffRefreshTimer == null)
            {
                diffRefreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                diffRefreshTimer.Tick += (sender, args) => { diffRefreshTimer.Stop(); RefreshDiff(); };
            }
            diffRefreshTimer.Stop(); diffRefreshTimer.Start();
        }

        private void RefreshDiff()
        {
            if (!hasDiffBase || baseEditor == null || diffMode == DiffViewMode.None)
            {
                displayManager.SetDiffLineKinds(null);
                displayManager.SetDiffLineNumbers(null);
                if (baseEditor != null) baseEditor.DisplayManager.SetDiffLineKinds(null);
                return;
            }
            string currentText = document.Text;
            DiffAlignment alignment = DiffEngine.Compare(comparisonBaseDocument == null ? String.Empty : comparisonBaseDocument.Text, currentText);
            currentAlignment = alignment;
            displayManager.SetDiffLineKinds(alignment.CurrentLineKinds);
            displayManager.SetDiffLineNumbers(null);
            baseEditor.LoadText(alignment.BaseDisplayText, comparisonFileName);
            baseEditor.DisplayManager.SetDiffLineKinds(alignment.BaseDisplayLineKinds);
            baseEditor.DisplayManager.SetDiffLineNumbers(alignment.BaseDisplayLineNumbers);
            if (diffMode == DiffViewMode.SideBySide)
                Dispatcher.BeginInvoke(new Action(SynchronizeBaseToCurrent), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public EditorCapabilities Capabilities { get; private set; } = new EditorCapabilities();
        public bool IsReadOnly
        {
            get { return !Capabilities.CanEdit; }
            set
            {
                Capabilities.CanEdit = !value;
                Capabilities.CanCut = !value;
                Capabilities.CanPaste = !value;
                displayManager.CanEdit = !value;
            }
        }
        public bool? WordWrapOverride { set { displayManager.SetWordWrapOverride(value); } }

        public event EventHandler VerticalScrollChanged;
        public double VerticalOffset { get { return vScrollBar.Value; } }
        public void SetVerticalOffset(double value)
        {
            if (!displayManager.IsReady) return;
            double bounded = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum, value));
            vScrollBar.Value = bounded;
            displayManager.vScrollBar_Scroll(vScrollBar, new System.Windows.Controls.Primitives.ScrollEventArgs(System.Windows.Controls.Primitives.ScrollEventType.ThumbPosition, bounded));
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
            set { this.copyPasteManager = value; if (this.baseEditor != null) this.baseEditor.CopyPasteManager = value; }
        }

        public void NotifyOfSettingsChange()
        {
            SetControlBackground();
            this.displayManager.NotifyOfSettingsChange(/*recreateRenderTarget*/true);
            if (baseEditor != null) baseEditor.NotifyOfSettingsChange();
        }

        public void ReplaceText(int index, int length, string newText)
        {
            if (this.IsReadOnly) return;
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ReplaceTextFlightEvent(index, length, newText));
            }

            this.undoRedoManager.BeginTransaction();
            try
            {
                this.document.DeleteAt(index, length);
                this.document.InsertAt(index, newText);
            }
            finally
            {
                this.undoRedoManager.EndTransaction();
            }
        }

        public int ReplaceAllText(string findText, string replaceText, bool matchCase, bool useRegEx, bool replaceInBackgroundHighlightRange)
        {
            if (this.IsReadOnly) return 0;
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ReplaceAllTextFlightEvent(findText, replaceText, matchCase, useRegEx, replaceInBackgroundHighlightRange));
            }
            this.undoRedoManager.BeginTransaction();
            try
            {
                int beginOrdinal = Document.UNDEFINED_ORDINAL;
                int endOrdinal = Document.UNDEFINED_ORDINAL;
                if (replaceInBackgroundHighlightRange)
                    this.displayManager.GetBackgroundHighlightRange(out beginOrdinal, out endOrdinal);
                return this.document.ReplaceAllText(findText, replaceText, matchCase, useRegEx, beginOrdinal, endOrdinal);
            }
            finally
            {
                this.undoRedoManager.EndTransaction();
            }
        }

        public void ReplaceWithRegexAtOrdinal(string findText, string replaceText, bool matchCase, int beginOrdinal)
        {
            if (this.IsReadOnly) return;
            if (this.flightRecorder.IsRecording)
            {
                this.flightRecorder.AddFlightEvent(new FlightRecorder.ReplaceWithRegexAtOrdinalFlightEvent(findText, replaceText, matchCase, beginOrdinal));
            }

            this.undoRedoManager.BeginTransaction();
            try
            {
                this.document.ReplaceWithRegexAtOrdinal(findText, replaceText, matchCase, beginOrdinal);
            }
            finally
            {
                this.undoRedoManager.EndTransaction();
            }
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
                if (this.IsReadOnly) return;
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

        public void StartFlightRecord()
        {
            this.flightRecorder.StartRecording();
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
        private readonly TextFindSession findSession;
        private readonly ComparisonFindSession comparisonFindSession;
        private readonly bool ownsComparison;
        private TextEditor activeCommandSurface;
        private bool relayingBaseContextMenu;
        private TextEditor baseEditor;
        private bool synchronizingScroll;
        private bool hasDiffBase;
        private string comparisonFileName;
        private DiffViewMode diffMode;
        private System.Windows.Threading.DispatcherTimer diffRefreshTimer;
        private DiffAlignment currentAlignment;
        private Document comparisonBaseDocument;

        public FindNavigationResult StartFind(FindQuery query) { return hasDiffBase ? comparisonFindSession.Start(query) : findSession.Start(query); }
        public FindNavigationResult FindNext() { return hasDiffBase ? comparisonFindSession.Next() : findSession.Next(); }
        public FindNavigationResult FindPrevious() { return hasDiffBase ? comparisonFindSession.Previous() : findSession.Previous(); }
        public void ClearFind() { findSession.Clear(); comparisonFindSession.Clear(); }
        internal int ActiveComparisonFindIndex { get { return comparisonFindSession.ActiveMatchIndex; } }
    }
}
