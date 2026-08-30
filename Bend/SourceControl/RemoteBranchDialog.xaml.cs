using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Bend.SourceControl
{
    public partial class RemoteBranchDialog : Window
    {
        public RemoteBranchDialog()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                BranchBox.Focus();
                BranchBox.CaretIndex = BranchBox.Text.Length;
            };
        }

        public string RemoteBranch { get { return BranchBox.Text.Trim(); } }

        private void BranchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CheckoutButton != null)
                CheckoutButton.IsEnabled = !String.IsNullOrWhiteSpace(BranchBox.Text);
        }

        private void Checkout_Click(object sender, RoutedEventArgs e) { DialogResult = true; }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
        }
    }
}
