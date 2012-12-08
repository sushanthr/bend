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
    /// Interaction logic for SaveChangesMessageBox.xaml
    /// </summary>
    public partial class SaveChangesMessageBox : Window
    {
        public enum ButtonClicked {
            Save,
            Discard,
            Cancel
        };
        internal ButtonClicked buttonClicked;

        public SaveChangesMessageBox(string fileName)
        {
            InitializeComponent();
            if (fileName != null && fileName.Length != 0)
            {
                this.FileName.Text = fileName;
            }
            buttonClicked = ButtonClicked.Cancel;
        }

        public static ButtonClicked Show(string fileName)
        {
            SaveChangesMessageBox messageBox = new SaveChangesMessageBox(fileName);            
            messageBox.Owner = Application.Current.MainWindow;
            messageBox.ShowDialog();
            return messageBox.buttonClicked;
        }
        
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            cancelButton.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                buttonClicked = ButtonClicked.Cancel;
                this.Close();
            }
        }

        private void DiscardButtonClick(object sender, RoutedEventArgs e)
        {
            buttonClicked = ButtonClicked.Discard;
            this.Close();
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            buttonClicked = ButtonClicked.Cancel;
            this.Close();
        }

        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            buttonClicked = ButtonClicked.Save;
            this.Close();
        }
    }
}
