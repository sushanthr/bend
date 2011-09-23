using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls.Primitives;
using System.ComponentModel;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    internal class ScrollBoundsManager
    {
        const int UPDATE_INTERVAL = 100;

        internal ScrollBoundsManager(ScrollBar vScrollBar, 
            ScrollBar hScrollBar, 
            DisplayManager displayManager, 
            RenderHost renderHost,
            Document document)
        {
            this.totalLineCount = 0;
            this.vScrollBar = vScrollBar;
            this.hScrollBar = hScrollBar;
            this.document = document;
            this.displayManager = displayManager;
            this.textLayoutBuilder = new TextLayoutBuilder();
            this.currentWidth = 0;
            this.currentFirstVisibleOrdinal = Document.UNDEFINED_ORDINAL;

            this.scrollLengthEstimator = new BackgroundWorker();
            this.scrollLengthEstimator.WorkerReportsProgress = true;
            this.scrollLengthEstimator.WorkerSupportsCancellation = true;
            this.scrollLengthEstimator.DoWork += new DoWorkEventHandler(scrollLengthEstimator_DoWork);
            this.scrollLengthEstimator.ProgressChanged += new ProgressChangedEventHandler(scrollLengthEstimator_ProgressChanged);
            this.scrollLengthEstimator.RunWorkerCompleted += new RunWorkerCompletedEventHandler(scrollLengthEstimator_RunWorkerCompleted);

            vScrollBar.Loaded += new System.Windows.RoutedEventHandler(vScrollBar_Loaded);
        }

        #region Event handler (Renderhost size change / loaded)

        void vScrollBar_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            DisableVScrollbar();
            DisableHScrollbar();
        }

        internal void UpdateVerticalScrollBoundsDueToContentChange(int delta)
        {
            this.SetVerticalScrollBarLimits(this.totalLineCount + delta);

            if (scrollLengthEstimator.IsBusy)
            {
                // Content changed, hence restart the scrollLengthEstimator if it has been running.
                this.InitializeVerticalScrollBounds(this.currentWidth);
            }
        }

        #endregion

        #region Vertical scroll bounds estimation

        internal void InitializeVerticalScrollBounds(double width)
        {
            lock (this)
            {
                this.currentWidth = width;
                this.currentFirstVisibleOrdinal = displayManager.FirstVisibleOrdinal();
            }

            if (scrollLengthEstimator.IsBusy)
            {
                // Request a restart
                scrollLengthEstimator.CancelAsync();
            }
            else
            {
                scrollLengthEstimator.RunWorkerAsync(this);
            }
        }

        void scrollLengthEstimator_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            ScrollBoundsManager scrollBoundsManager = (ScrollBoundsManager)e.Argument;
            e.Cancel = false;
            e.Result = null;

            Document document = scrollBoundsManager.document;
            float layoutWidth;
            int firstVisibleOrdinal;
            lock (scrollBoundsManager)
            {
                layoutWidth = (float)scrollBoundsManager.currentWidth;
                firstVisibleOrdinal = scrollBoundsManager.currentFirstVisibleOrdinal;
            }

            try
            {
                int ordinal = document.FirstOrdinal();
                int lineCount = 0;
                int firstLineIndex = -1;
                int newPageBeginOrdinal = 0;
                double newPageTop = 0;
                int contentLines = 0;

                while (ordinal != Document.UNDEFINED_ORDINAL)
                {
                    VisualLine vl = this.textLayoutBuilder.GetNextLine(document, ordinal, layoutWidth, out ordinal);

                    if (vl.HasHardBreak)
                    {
                        contentLines++;
                    }

                    if (firstLineIndex == -1)
                    {
                        if (vl.NextOrdinal > firstVisibleOrdinal)
                        {
                            // page begin has been passed
                            newPageBeginOrdinal = vl.BeginOrdinal;
                            firstLineIndex = lineCount;
                        }
                        else
                        {
                            newPageTop += vl.Height;
                        }
                    }

                    lineCount++;

                    if (lineCount % UPDATE_INTERVAL == 0)
                    {
                        worker.ReportProgress(lineCount);
                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                    }
                }

                if (!e.Cancel)
                {
                    object[] resultArray = { lineCount, newPageBeginOrdinal, newPageTop, firstLineIndex, contentLines };
                    e.Result = resultArray;
                }
            }
            catch
            {
                // Do nothing and let this thread rest in peace.
                // It is legit to fall into this catch because document could have changed underneath us and
                // we could try to access ordinals that dont exist. Simply ignore the failure as this
                // thread will be restarted soon by the change notification.
            }
        }

        void scrollLengthEstimator_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                // A restart was requested
                scrollLengthEstimator.RunWorkerAsync(this);
            }
            else if (e.Error == null)
            {
                // We have a valid result.
                object[] resultArray = (object []) e.Result;
                int lineCount = (int)resultArray[0];
                int newPageBeginOrdinal = (int)resultArray[1];
                double newPageTop = (double)resultArray[2];
                int firstLineIndex = (int)resultArray[3];
                int maxContentLines = (int)resultArray[4];

                this.SetVerticalScrollBarLimits(lineCount);
                if (this.vScrollBar.IsEnabled)
                {
                    this.vScrollBar.Value = firstLineIndex;
                    this.displayManager.AdjustVScrollPositionForResize(newPageBeginOrdinal, newPageTop, firstLineIndex);
                }

                this.displayManager.MaxContentLines = maxContentLines;
            }
        }

        void scrollLengthEstimator_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int totalLineCount = e.ProgressPercentage;
            // To prevent jittery scroll thumb updates, update the thumb only if totalLineCount increases.
            // On completion, we do update the thumb size to the final value correctly.
            if (totalLineCount > this.totalLineCount)
            {
                this.SetVerticalScrollBarLimits(totalLineCount);
            }
        }

        private void SetVerticalScrollBarLimits(int totalLineCount)
        {
            int maxLinesPerPage = displayManager.MaxLinesPerPage();
            this.totalLineCount = totalLineCount;
            if (maxLinesPerPage < totalLineCount && maxLinesPerPage != 0)
            {
                // Need to show scrollbars
                this.vScrollBar.IsEnabled = true;
                this.vScrollBar.Minimum = 0;
                this.vScrollBar.Maximum = totalLineCount - maxLinesPerPage;
                this.vScrollBar.Track.Thumb.Visibility = System.Windows.Visibility.Visible;

                // Guesstimate the thumb hieght
                if (maxLinesPerPage < totalLineCount)
                {
                    this.vScrollBar.ViewportSize = totalLineCount * maxLinesPerPage / (totalLineCount - maxLinesPerPage);
                }
                else
                {
                    this.vScrollBar.ViewportSize = double.MaxValue;
                }
            }
            else
            {
                // Vertical scrollbar not needed
                DisableVScrollbar();
            }
        }

        #endregion

        #region Horizonatal scroll bounds setter API
        internal void InitializeHorizontalScrollBounds(double maxLineWidth, double renderHostWidth)
        {
            if (maxLineWidth > renderHostWidth)
            {
                // Need to show scrollbars
                this.vScrollBar.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action
                    (
                        delegate()
                        {
                            this.hScrollBar.IsEnabled = true;
                            this.hScrollBar.Minimum = 0;
                            this.hScrollBar.Maximum = maxLineWidth;
                            this.hScrollBar.Track.Thumb.Visibility = System.Windows.Visibility.Visible;

                            // Guesstimate the thumb hieght
                            if (renderHostWidth < maxLineWidth)
                            {
                                this.hScrollBar.ViewportSize = maxLineWidth * renderHostWidth / (maxLineWidth - renderHostWidth);
                            }
                            else
                            {
                                this.hScrollBar.ViewportSize = double.MaxValue;
                            }
                        }
                    )
                );
            }
            else
            {
                DisableHScrollbar();
            }
        }
        #endregion

        #region Scroll bar offset computation / Hide - Show Scroll bar API

        internal void ScrollBy(int numberOfLines)
        {
            double newScrollValue = this.vScrollBar.Value + numberOfLines;
            if (newScrollValue < 0) newScrollValue = 0;
            if (newScrollValue > this.vScrollBar.Maximum) newScrollValue = this.vScrollBar.Maximum;

            this.vScrollBar.Value = newScrollValue;
            this.displayManager.vScrollBar_Scroll(this, new ScrollEventArgs(
                numberOfLines > 0 ? ScrollEventType.SmallIncrement : ScrollEventType.SmallDecrement, 
                this.vScrollBar.Value));
        }

        private void DisableVScrollbar()
        {
            this.vScrollBar.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                new Action
                (
                    delegate()
                    {
                        this.vScrollBar.IsEnabled = false;
                        this.vScrollBar.Track.Thumb.Visibility = System.Windows.Visibility.Hidden;
                    }
                )
            );
        }

        private void DisableHScrollbar()
        {
            this.hScrollBar.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                new Action
                (
                    delegate()
                    {
                        this.hScrollBar.IsEnabled = false;
                        this.hScrollBar.Track.Thumb.Height = 0;
                    }
                )
            );
        }

        #endregion

        #region Member data
        ScrollBar hScrollBar;
        ScrollBar vScrollBar;
        BackgroundWorker scrollLengthEstimator;
        Document document;
        DisplayManager displayManager;
        TextLayoutBuilder textLayoutBuilder;
        int totalLineCount;

        double currentWidth;
        int currentFirstVisibleOrdinal;

        #endregion
    }
}