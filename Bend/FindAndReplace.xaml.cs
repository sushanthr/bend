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
using System.Windows.Shapes;

namespace Bend
{
    /// <summary>
    /// Interaction logic for FindAndReplace.xaml
    /// </summary>
    public partial class FindAndReplace : Window
    {
        #region Member Data
        MainWindow mainWindow;        
        #endregion  

        public FindAndReplace(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
        }

        private void Close_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Hide();
                e.Handled = true;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.H)
                {
                    this.Hide();
                    e.Handled = true;
                }
            }
        }

        private void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Find_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0)
            {
                mainWindow.FindOnPage(FindText.Text, this.MatchCase.IsChecked ?? true, this.RegexFind.IsChecked ?? true);                
            }
        }

        private void Replace_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ComboBox comboBox = ((ComboBox)sender);
                if (comboBox.Items != null && (comboBox.Items.Count == 0 || (string)comboBox.Items[0] != comboBox.Text))
                {
                    comboBox.Items.Insert(0, comboBox.Text);
                }
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;
                if (keyboardFocus != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }
            }            
        }

        private void Find_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ComboBox comboBox = ((ComboBox)sender);
                if (comboBox.Text.Length > 0 && comboBox.Items != null && (comboBox.Items.Count == 0 || (string)comboBox.Items[0] != comboBox.Text))
                {
                    comboBox.Items.Insert(0, comboBox.Text);
                }
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;
                if (keyboardFocus != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }
            }
            else
            {
                mainWindow.FindOnPage(FindText.Text, this.MatchCase.IsChecked ?? true, this.RegexFind.IsChecked ?? true);
            }
        }

        private void MoveToNext_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {                
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;
                if (keyboardFocus != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.mainWindow.SetStatusText("");
            if (this.Visibility == System.Windows.Visibility.Visible)
            {
                // window is becoming visible.
                if (FindText.Text != null && FindText.Text.Length != 0)
                {
                    mainWindow.FindOnPage(FindText.Text, this.MatchCase.IsChecked ?? true, this.RegexFind.IsChecked ?? true);
                }
            }
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            if (FindText.Text.Length > 0 && FindText.Items != null && (FindText.Items.Count == 0 || (string)FindText.Items[0] != FindText.Text))
            {
                FindText.Items.Insert(0, FindText.Text);
            }
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                mainWindow.HighlightPreviousMatch();
            }
            else
            {
                mainWindow.HighlightNextMatch();
            }
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (FindText.Text.Length > 0 && FindText.Items != null && (FindText.Items.Count == 0 || (string)FindText.Items[0] != FindText.Text))
            {
                FindText.Items.Insert(0, FindText.Text);
            }
            string replaceText = ReplaceText.Text;
            if (this.RegexFind.IsChecked ?? true)
            {
                replaceText = System.Text.RegularExpressions.Regex.Unescape(replaceText);
            }
            if (replaceall.IsChecked ?? true)
            {
                mainWindow.ReplaceText(FindText.Text, replaceText, this.MatchCase.IsChecked ?? true, this.RegexFind.IsChecked ?? true);
            }
            else
            {
                this.mainWindow.ReplaceSelectedText(replaceText);
                mainWindow.HighlightNextMatch();
            }            
        }

        private void FindOptionsChanged(object sender, RoutedEventArgs e)
        {
            mainWindow.FindOnPage(FindText.Text, this.MatchCase.IsChecked ?? true, this.RegexFind.IsChecked ?? true);
        }
    }
}
