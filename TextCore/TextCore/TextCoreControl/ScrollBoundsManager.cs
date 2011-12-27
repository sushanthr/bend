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
            this.vScrollBar = vScrollBar;
            this.hScrollBar = hScrollBar;
            this.document = document;
            this.displayManager = displayManager;
            this.textLayoutBuilder = new TextLayoutBuilder();
            this.currentWidth = 0;
            this.currentFirstVisibleOrdinal = Document.UNDEFINED_ORDINAL;
            this.verticalScrollBound = 0;
            this.horizontalScrollBound = 0;

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
            vScrollBar.SmallChange = this.textLayoutBuilder.AverageLineHeight();
            vScrollBar.LargeChange = this.displayManager.AvailableHeight;
        }

        internal void UpdateVerticalScrollBoundsDueToContentChange(double deltaVertical, double maxNewVisualLineWidth)
        {
            this.SetVerticalScrollBarLimits(this.verticalScrollBound + deltaVertical);
            if (maxNewVisualLineWidth > this.horizontalScrollBound)
                this.SetHorizontalScrollBarLimits(maxNewVisualLineWidth);

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
                double verticalScrollBound = 0;
                double horizontalScrollBound = 0;

                bool pageTopFound = false;
                int newPageBeginOrdinal = 0;
                double newPageTop = 0;
                int contentLines = 0;
                int progressCount = 0;

                while (ordinal != Document.UNDEFINED_ORDINAL)
                {
                    VisualLine vl = this.textLayoutBuilder.GetNextLine(document, ordinal, layoutWidth, out ordinal);

                    if (vl.HasHardBreak)
                    {
                        contentLines++;
                    }

                    if (!pageTopFound)
                    {
                        if (vl.NextOrdinal > firstVisibleOrdinal)
                        {
                            // page begin has been passed
                            newPageBeginOrdinal = vl.BeginOrdinal;
                            pageTopFound = true;
                        }
                        else
                        {
                            newPageTop += vl.Height;
                        }
                    }

                    verticalScrollBound += vl.Height;
                    horizontalScrollBound = Math.Max(horizontalScrollBound, vl.Width);

                    progressCount++;
                    if (progressCount % UPDATE_INTERVAL == 0)
                    {
                        worker.ReportProgress((int)verticalScrollBound);
                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                    }
                }

                if (!e.Cancel)
                {
                    object[] resultArray = { verticalScrollBound, horizontalScrollBound, newPageBeginOrdinal, newPageTop, contentLines };
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
                double verticalScrollBound = (double)resultArray[0];
                double horizontalScrollBound = (double)resultArray[1];
                int newPageBeginOrdinal = (int)resultArray[2];
                double newPageTop = (double)resultArray[3];
                int maxContentLines = (int)resultArray[4];

                this.SetVerticalScrollBarLimits(verticalScrollBound);
                this.SetHorizontalScrollBarLimits(horizontalScrollBound);
                if (this.vScrollBar.IsEnabled)
                {
                    this.vScrollBar.Value = newPageTop;
                    this.displayManager.AdjustVScrollPositionForResize(newPageBeginOrdinal, newPageTop);
                }

                this.displayManager.MaxContentLines = maxContentLines;
            }
        }

        void scrollLengthEstimator_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int verticalScrollBound = e.ProgressPercentage;
            // To prevent jittery scroll thumb updates, update the thumb only if totalLineCount increases.
            // On completion, we do update the thumb size to the final value correctly.
            if (verticalScrollBound > this.verticalScrollBound)
            {
                this.SetVerticalScrollBarLimits(verticalScrollBound);
            }
        }

        private void SetVerticalScrollBarLimits(double verticalScrollBound)
        {
            this.verticalScrollBound = verticalScrollBound;
            if (displayManager.AvailableHeight < this.verticalScrollBound && displayManager.AvailableHeight != 0)
            {
                // Need to show scrollbars
                this.vScrollBar.IsEnabled = true;
                this.vScrollBar.Minimum = 0;
                this.vScrollBar.Maximum = this.verticalScrollBound - displayManager.AvailableHeight;
                this.vScrollBar.Track.Thumb.Visibility = System.Windows.Visibility.Visible;

                // Guesstimate the thumb hieght
                this.vScrollBar.ViewportSize = this.verticalScrollBound * displayManager.AvailableHeight / (this.verticalScrollBound - displayManager.AvailableHeight);
            }
            else
            {
                // Vertical scrollbar not needed
                DisableVScrollbar();
            }
        }

        private void SetHorizontalScrollBarLimits(double horizontalScrollBound)
        {
            this.horizontalScrollBound = horizontalScrollBound;
            if (horizontalScrollBound > displayManager.AvailbleWidth)
            {
                // Need to show scrollbars
                this.vScrollBar.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action
                    (
                        delegate()
                        {
                            this.hScrollBar.IsEnabled = true;
                            this.hScrollBar.Minimum = 0;
                            this.hScrollBar.Maximum = horizontalScrollBound;
                            this.hScrollBar.Track.Thumb.Visibility = System.Windows.Visibility.Visible;

                            // Guesstimate the thumb hieght
                            if (displayManager.AvailbleWidth < horizontalScrollBound)
                            {
                                this.hScrollBar.ViewportSize = horizontalScrollBound * displayManager.AvailbleWidth / (horizontalScrollBound - displayManager.AvailbleWidth);
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

        internal void ScrollBy(double offset)
        {
            double newScrollValue = this.vScrollBar.Value + offset;
            if (newScrollValue < 0) newScrollValue = 0;
            if (newScrollValue > this.vScrollBar.Maximum) newScrollValue = this.vScrollBar.Maximum;

            this.vScrollBar.Value = newScrollValue;
            this.displayManager.vScrollBar_Scroll(this, new ScrollEventArgs(
                offset > 0 ? ScrollEventType.SmallIncrement : ScrollEventType.SmallDecrement, 
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
                        this.hScrollBar.Track.Thumb.Visibility = System.Windows.Visibility.Hidden;
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

        double currentWidth;
        int currentFirstVisibleOrdinal;

        double verticalScrollBound;
        double horizontalScrollBound;
        #endregion
    }
}