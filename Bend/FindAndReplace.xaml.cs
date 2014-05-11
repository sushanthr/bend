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
    internal struct FindOptions
    {
        private string findText;
        private bool findUseRegex;
        private bool findMatchCase;
        private int selectionBegin;
        private int selectionEnd;

        internal FindOptions(string findText)
        {
            this.findText = findText;
            this.findUseRegex = false;
            this.findMatchCase = false;
            this.selectionEnd = TextCoreControl.Document.UNDEFINED_ORDINAL;
            this.selectionBegin = TextCoreControl.Document.UNDEFINED_ORDINAL;
        }

        public static bool operator ==(FindOptions a, FindOptions b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            // Return true if the fields match:
            return a.findText == b.findText && a.findMatchCase == b.findMatchCase && a.findUseRegex == b.findUseRegex && a.selectionBegin == b.selectionBegin && a.selectionEnd == b.selectionEnd;
        }

        public static bool operator !=(FindOptions a, FindOptions b)
        {
            return !(a == b);
        }

        public override bool Equals(Object obj)
        {
            return obj is FindOptions && this == (FindOptions)obj;
        }
        public override int GetHashCode()
        {
            return findText.GetHashCode() ^ findUseRegex.GetHashCode() ^ findMatchCase.GetHashCode() ^ selectionBegin.GetHashCode() ^ selectionEnd.GetHashCode();
        }

        internal void SetSelection(int selectionBegin, int selectionEnd)
        {
            this.selectionBegin = selectionBegin;
            this.selectionEnd = selectionEnd;
        }

        internal void GetSelection(out int selectionBegin, out int selectionEnd)
        {
            selectionBegin = this.selectionBegin;
            selectionEnd = this.selectionEnd;
        }

        internal string FindText
        {
            get { return this.findText; }
            set { this.findText = value; }
        }

        internal bool FindUseRegex
        {
            get { return this.findUseRegex; }
            set { this.findUseRegex = value; }
        }

        internal bool FindMatchCase
        {
            get { return this.findMatchCase;  }
            set { this.findMatchCase = value; }
        }

        internal bool IsFindAndReplaceInSelection
        {
            get { return this.selectionEnd != TextCoreControl.Document.UNDEFINED_ORDINAL && this.selectionBegin != TextCoreControl.Document.UNDEFINED_ORDINAL; }
        }
    }

    /// <summary>
    /// Interaction logic for FindAndReplace.xaml
    /// </summary>
    public partial class FindAndReplace : Window
    {
        #region Member Data
        MainWindow mainWindow;
        FindOptions findOptions;
        #endregion  

        public FindAndReplace(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            findOptions = new FindOptions(String.Empty);
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
                this.UpdateFindOptions();
                mainWindow.CurrentTab.StartFindOnPage(mainWindow, this.findOptions);
            }
        }

        private void UpdateFindOptions()
        {
            this.findOptions.FindText = FindText.Text;
            this.findOptions.FindMatchCase = this.MatchCase.IsChecked ?? true;
            this.findOptions.FindUseRegex = this.RegexFind.IsChecked ?? true;
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
                this.UpdateFindOptions();
                mainWindow.CurrentTab.StartFindOnPage(mainWindow, this.findOptions);
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
            this.mainWindow.SetStatusText("", MainWindow.StatusType.STATUS_CLEAR);
            if (this.Visibility == System.Windows.Visibility.Visible)
            {
                this.SetUserSelectionAsFindRange();
                // window is becoming visible.
                if (FindText.Text != null && FindText.Text.Length != 0 && mainWindow.CurrentTab != null)
                {
                    this.UpdateFindOptions();
                    mainWindow.CurrentTab.StartFindOnPage(mainWindow, this.findOptions);
                }
            }
            else
            {
                if (findOptions.IsFindAndReplaceInSelection)
                {
                    mainWindow.CurrentTab.TextEditor.ResetBackgroundHighlight();
                }
            }
        }

        private void SetUserSelectionAsFindRange()
        {
            int selectionBegin = mainWindow.CurrentTab.TextEditor.DisplayManager.SelectionBegin;
            int selectionEnd = mainWindow.CurrentTab.TextEditor.DisplayManager.SelectionEnd;
            if (selectionBegin != selectionEnd && selectionBegin != TextCoreControl.Document.UNDEFINED_ORDINAL)
            {
                this.Selection.IsChecked = true;
                this.findOptions.SetSelection(selectionBegin, selectionEnd);
                mainWindow.CurrentTab.TextEditor.SetBackgroundHighlight(selectionBegin, selectionEnd);
            }
            else
            {
                this.Selection.IsChecked = false;
                this.findOptions.SetSelection(TextCoreControl.Document.UNDEFINED_ORDINAL, TextCoreControl.Document.UNDEFINED_ORDINAL);
            }
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow.CurrentTab != null)
            {
                if (FindText.Text.Length > 0 && FindText.Items != null && (FindText.Items.Count == 0 || (string)FindText.Items[0] != FindText.Text))
                {
                    FindText.Items.Insert(0, FindText.Text);
                }
                if (mainWindow.CurrentTab.FindOptions == this.findOptions)
                {
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    {
                        mainWindow.SetStatusText(mainWindow.CurrentTab.HighlightPreviousMatch(), MainWindow.StatusType.STATUS_FINDONPAGE);
                    }
                    else
                    {
                        mainWindow.SetStatusText(mainWindow.CurrentTab.HighlightNextMatch(), MainWindow.StatusType.STATUS_FINDONPAGE);
                    }
                }
                else
                {
                    this.UpdateFindOptions();
                    mainWindow.CurrentTab.StartFindOnPage(mainWindow, this.findOptions);
                }
            }
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow.CurrentTab != null)
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
                if (this.mainWindow.CurrentTab.FindOptions == this.findOptions)
                {
                    if (this.RegexFind.IsChecked ?? true)
                    {
                        TextCoreControl.TextEditor textEditor = mainWindow.CurrentTab.TextEditor;
                        mainWindow.CurrentTab.TextEditor.ReplaceWithRegexAtOrdinal(FindText.Text, replaceText, this.MatchCase.IsChecked ?? true, textEditor.DisplayManager.SelectionBegin);
                    }
                    else
                    {
                        this.mainWindow.CurrentTab.TextEditor.SelectedText = replaceText;
                    }
                    mainWindow.CurrentTab.HighlightNextMatch();
                }
                else
                {
                    this.UpdateFindOptions();
                    mainWindow.CurrentTab.StartFindOnPage(mainWindow, this.findOptions);
                }
            }
        }

        private void FindOptionsChanged(object sender, RoutedEventArgs e)
        {
            this.UpdateFindOptions();
            mainWindow.CurrentTab.StartFindOnPage(mainWindow,this.findOptions);
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow.CurrentTab != null)
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

                this.UpdateFindOptions();
                int count = mainWindow.CurrentTab.TextEditor.ReplaceAllText(FindText.Text, replaceText, this.findOptions.FindMatchCase, this.findOptions.FindUseRegex, this.findOptions.IsFindAndReplaceInSelection);
                if (count == 0)
                {
                    mainWindow.SetStatusText("NO MATCHES FOUND", MainWindow.StatusType.STATUS_FINDONPAGE);
                }
                else
                {
                    mainWindow.SetStatusText(count + " MATCHES REPLACED", MainWindow.StatusType.STATUS_FINDONPAGE);
                    mainWindow.CurrentTab.ClearFindOnPage();
                }
            }
        }

        private void InSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (Selection.IsChecked ?? true)
            { 
                SetUserSelectionAsFindRange();
            }
            else
            {
                this.findOptions.SetSelection(TextCoreControl.Document.UNDEFINED_ORDINAL, TextCoreControl.Document.UNDEFINED_ORDINAL);
                mainWindow.CurrentTab.TextEditor.ResetBackgroundHighlight();
            }
        }
    }
}
