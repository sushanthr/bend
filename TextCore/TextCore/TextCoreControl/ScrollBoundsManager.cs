using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls.Primitives;
using System.Threading;

using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    internal class ScrollBoundsManager
    {
        public ScrollBoundsManager(ScrollBar vScrollBar, 
            ScrollBar hScrollBar, 
            DisplayManager displayManager, 
            RenderHost renderHost,
            Document document)
        {
            this.totalLineCount = 0;
            vScrollBar.Loaded += new System.Windows.RoutedEventHandler(vScrollBar_Loaded);
            this.vScrollBar = vScrollBar;
            this.hScrollBar = hScrollBar;
            this.asyncScrollLengthEstimater = null;
            this.document = document;
            renderHost.SizeChanged += new System.Windows.SizeChangedEventHandler(renderHost_SizeChanged);
            this.displayManager = displayManager;
        }

        #region Event handler (Renderhost size change / loaded)
        void renderHost_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            RenderHost renderHost = (RenderHost) sender;
            this.InitializeVerticalScrollBounds((float)renderHost.ActualWidth);
        }

        void vScrollBar_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            DisableVScrollbar();
            DisableHScrollbar();
        }
        #endregion

        #region Vertical scroll bounds estimation
        public void InitializeVerticalScrollBounds(float width)
        {
            TextLayoutBuilder textLayoutBuilder = new TextLayoutBuilder();
            object[] paramaterArray = { this.document, textLayoutBuilder, this, width, this.displayManager.FirstVisibleOrdinal()};

            if (asyncScrollLengthEstimater != null)
            {
                asyncScrollLengthEstimater.Abort();
            }
            asyncScrollLengthEstimater = new Thread(AsyncScrollLengthEstimater);
            asyncScrollLengthEstimater.Start(paramaterArray);
        }

        static void AsyncScrollLengthEstimater(object paramaterArrayIn)
        {
            object[] paramaterArray = (object[])paramaterArrayIn;
            Document document = (Document)paramaterArray[0];
            TextLayoutBuilder textLayoutBuilder = (TextLayoutBuilder)paramaterArray[1];
            ScrollBoundsManager scrollBoundsManager = (ScrollBoundsManager)paramaterArray[2];
            float layoutWidth = (float)paramaterArray[3];
            int pageBeginOrdinal = (int)paramaterArray[4];

            int ordinal = document.FirstOrdinal();
            int lineCount = 0;
            int firstLineIndex = -1;
            int newPageBeginOrdinal = 0;
            double newPageTop = 0;

            while (ordinal != Document.UNDEFINED_ORDINAL)
            {
                VisualLine vl = textLayoutBuilder.GetNextLine(document, ordinal, layoutWidth, out ordinal);
                if (firstLineIndex == -1)
                {
                    if (vl.NextOrdinal > pageBeginOrdinal)
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
            }

            scrollBoundsManager.InitializeVerticalScrollBounds(lineCount, newPageBeginOrdinal, newPageTop, firstLineIndex);
        }

        private void InitializeVerticalScrollBounds(int totalLineCount, int pageBeginOrdinal, double pageTop, double scrollOffset)
        {
            this.vScrollBar.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                new Action
                (
                    delegate()
                    {
                        this.SetVerticalScrollBarLimits(totalLineCount);
                        if (this.vScrollBar.IsEnabled)
                        {
                            this.vScrollBar.Value = scrollOffset;
                            this.displayManager.AdjustVScrollPositionForResize(pageBeginOrdinal, pageTop, scrollOffset);
                        }
                    }
                )
            );
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

        internal void UpdateVerticalScrollBoundsDueToContentChange(int delta)
        {
            this.SetVerticalScrollBarLimits(this.totalLineCount + delta);
        }

        #endregion

        #region Horizonatal scroll bounds setter API
        public void InitializeHorizontalScrollBounds(double maxLineWidth, double renderHostWidth)
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

        public void ScrollBy(int numberOfLines)
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
        Thread asyncScrollLengthEstimater;
        Document document;
        DisplayManager displayManager;
        int totalLineCount;
        #endregion
    }
}