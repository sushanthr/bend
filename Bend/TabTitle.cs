using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Bend
{
    internal class TabTitle : WrapPanel
    {
        private static FontFamily fontFamilySegoeUI;
        private Point dragStartPoint;
        private bool dragStarted;
        private bool dragAccepted;
        private const double DragThreshold = 5;

        static TabTitle()
        {
            fontFamilySegoeUI = new FontFamily("Segoe UI");
        }

        internal TabTitle(bool mutedCloseButton = false)
        {
            this.Height = 34.7;
            this.Margin = new Thickness(0, 1, 0, 0);
            this.MinWidth = 150;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.SetResourceReference(Panel.BackgroundProperty, "TabBackgroundBrush");
            titleText = new TextBlock();
            titleText.Text = "New File";
            titleText.MinWidth = 120;
            titleText.MaxWidth = 220;
            titleText.Height = 34;
            titleText.Padding = new Thickness(14, 0, 8, 0);
            titleText.VerticalAlignment = VerticalAlignment.Center;
            titleText.RenderTransform = new TranslateTransform(0, 9.5);
            titleText.TextAlignment = TextAlignment.Left;
            titleText.TextTrimming = TextTrimming.CharacterEllipsis;
            titleText.FontFamily = fontFamilySegoeUI;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(titleText, /*isHitTestable*/true);
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
            this.Children.Add(titleText);

            Separator seperator = new Separator();
            seperator.Width = 5;
            seperator.Visibility = Visibility.Hidden;
            this.Children.Add(seperator);

            Path closeGlyph = new Path
            {
                Width = 9,
                Height = 9,
                Stroke = mutedCloseButton ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(240, 76, 76)),
                StrokeThickness = 1.4,
                Data = Geometry.Parse("M0,0 L9,9 M9,0 L0,9"),
                VerticalAlignment = VerticalAlignment.Center
            };
            closeButton = new Border
            {
                Width = 24,
                Height = 35.7,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = closeGlyph
            };
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(closeButton, /*isHitTestable*/true);
            this.Children.Add(closeButton);

            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(this, /*isHitTestable*/true);
            closeButton.MouseLeftButtonUp += closeButton_MouseLeftButtonUp;

            this.PreviewMouseMove += TabTitle_PreviewMouseMove;
            this.PreviewMouseLeftButtonDown += TabTitle_PreviewMouseLeftButtonDown;
            this.PreviewMouseLeftButtonUp += TabTitle_PreviewMouseLeftButtonUp;
            dragAccepted = true;
            dragStarted = false;
        }

        void TabTitle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragStarted = false;
            dragAccepted = true;
        }

        void TabTitle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStarted = true;
            dragStartPoint = e.GetPosition(this);
            dragAccepted = false;
        }

        void TabTitle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (dragStarted && !dragAccepted)
            {
                Vector delta = dragStartPoint - e.GetPosition(this);
                if (delta.Length > DragThreshold)
                {
                    dragAccepted = true;
                }
                else
                { 
                    // Suppress the drag
                    e.Handled = true;
                }
            }
        }

        void closeButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.CloseButtonClicked != null)
            {
                this.CloseButtonClicked(sender, e);
            }
        }

        internal string TitleText
        {
            get { return this.titleText.Text; }
            set { this.titleText.Text = value; }
        }

        internal FrameworkElement CloseButton
        {
            get { return this.closeButton; }
        }

        internal delegate void CloseButtonClickedEvent(object sender, MouseButtonEventArgs e);
        internal event CloseButtonClickedEvent CloseButtonClicked;

        readonly TextBlock titleText;
        readonly FrameworkElement closeButton;
    }
}
