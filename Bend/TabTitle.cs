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
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Bend
{
    internal class TabTitle : WrapPanel
    {
        private static FontFamily fontFamilySegoeUI;

        static TabTitle()
        {
            fontFamilySegoeUI = new FontFamily("Segoe UI");
        }

        internal TabTitle()
        {
            titleText = new TextBlock();
            titleText.Text = "New File";
            titleText.MinWidth = 110;
            titleText.Height = 34;
            titleText.Padding = new Thickness(5, 2, 0, 0);
            titleText.VerticalAlignment = VerticalAlignment.Top;
            titleText.TextAlignment = TextAlignment.Center;
            titleText.FontFamily = fontFamilySegoeUI;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(titleText, /*isHitTestable*/true);
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
            this.Children.Add(titleText);

            Separator seperator = new Separator();
            seperator.Width = 5;
            seperator.Visibility = Visibility.Hidden;
            this.Children.Add(seperator);

            closeButton = new Image();
            closeButton.Width = 8;
            closeButton.Height = 8;
            BitmapImage closeImage = new BitmapImage();
            closeImage.BeginInit();
            closeImage.UriSource = new Uri("pack://application:,,,/Bend;component/Images/Close.png");
            closeImage.EndInit();
            closeButton.Source = closeImage;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(closeButton, /*isHitTestable*/true);
            this.Children.Add(closeButton);

            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(this, /*isHitTestable*/true);
            closeButton.MouseLeftButtonUp += closeButton_MouseLeftButtonUp;
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

        internal Image CloseButton
        {
            get { return this.closeButton; }
        }

        internal delegate void CloseButtonClickedEvent(object sender, MouseButtonEventArgs e);
        internal event CloseButtonClickedEvent CloseButtonClicked;

        readonly TextBlock titleText;
        readonly Image closeButton;
    }
}
