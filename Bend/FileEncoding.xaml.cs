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
    /// Interaction logic for FileEncodingMessageBox.xaml
    /// </summary>
    public partial class FileEncodingMessageBox : Window
    {
        public FileEncodingMessageBox(TextCoreControl.TextEditor textEditor)
        {
            InitializeComponent();
            if (textEditor.DisplayManager.HasSeenNonAsciiCharacters)
            {
                ContainsAscii.Text = "The current file could contain non ASCII characters";
            }
            else
            {
                ContainsAscii.Text = "The current file contains only ASCII characters";
            }

            string currentEncoding = textEditor.Document.CurrentEncoding.ToString();

            switch (currentEncoding)
            {
                case "ASCII":
                    EncodingPicker.SelectedIndex = 0;
                    break;
                case "UTF-8":
                    EncodingPicker.SelectedIndex = 1;
                    break;
                case "Unicode":
                    EncodingPicker.SelectedIndex = 2;
                    break;
                case "UTF-7":
                    EncodingPicker.SelectedIndex = 3;
                    break;
                case "UTF-32":
                    EncodingPicker.SelectedIndex = 4;
                    break;
                case "BigEndianUnicode":
                    EncodingPicker.SelectedIndex = 5;
                    break;
                default:
                    EncodingPicker.SelectedIndex = 1;
                    break;
            }
        }

        public static void Show(TextCoreControl.TextEditor textEditor, bool warningMode)
        {
            FileEncodingMessageBox messageBox = new FileEncodingMessageBox(textEditor);
            messageBox.Owner = Application.Current.MainWindow;
            if (warningMode)
            {
                messageBox.SideBand.Background = Brushes.Orange;
                messageBox.Warning.Text = " and is about to be written to disk, please choose a suitable file encoding format";
            }
            messageBox.ShowDialog();
            string selectedEncoding = ((ComboBoxItem)messageBox.EncodingPicker.SelectedValue).Content.ToString();

            Encoding selectedEncodingObject;
            switch (selectedEncoding)
            {
                case "ASCII":
                    selectedEncodingObject = Encoding.ASCII;
                    break;
                case "UTF-8":
                    selectedEncodingObject = Encoding.UTF8;
                    break;
                case "Unicode":
                    selectedEncodingObject = Encoding.Unicode;
                    break;
                case "UTF-7":
                    selectedEncodingObject = Encoding.UTF7;
                    break;
                case "UTF-32":
                    selectedEncodingObject = Encoding.UTF32;
                    break;
                case "BigEndianUnicode":
                    selectedEncodingObject = Encoding.BigEndianUnicode;
                    break;
               default:
                    selectedEncodingObject = Encoding.UTF8;
                    break;
            }
            textEditor.Document.CurrentEncoding = selectedEncodingObject;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }        
    }
}
