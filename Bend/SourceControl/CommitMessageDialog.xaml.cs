using System;
using System.Windows;
using System.Windows.Controls;

namespace Bend.SourceControl
{
    public partial class CommitMessageDialog : Window
    {
        public CommitMessageDialog() { InitializeComponent(); Loaded += (s, e) => MessageBox.Focus(); }
        public string CommitMessage { get { return MessageBox.Text.Trim(); } }
        private void MessageBox_TextChanged(object sender, TextChangedEventArgs e) { CommitButton.IsEnabled = !String.IsNullOrWhiteSpace(MessageBox.Text); }
        private void Commit_Click(object sender, RoutedEventArgs e) { DialogResult = true; }
    }
}
