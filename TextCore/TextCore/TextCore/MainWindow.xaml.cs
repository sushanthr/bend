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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using Microsoft.Win32;

namespace TextCore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.tilted = false;
            TextEditor.CopyPasteManager = new TextCoreControl.CopyPasteManager();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            if (openDialog.ShowDialog().Value)
            {
                FilePath.Text = openDialog.FileName;
                TextEditor.LoadFile(FilePath.Text);
            }
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            if (!this.tilted)
            {
                this.tilted = true;
                TextEditor.Rasterize();
                tiltPanel.RenderTransform = new RotateTransform(5, this.tiltPanel.RenderSize.Width / 2, this.tiltPanel.RenderSize.Height / 2);
            }
            else
            {
                this.tilted = false;
                TextEditor.UnRasterize();
                tiltPanel.RenderTransform = null;
            }
        }

        bool tilted;

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            if (saveDialog.ShowDialog().Value)
            {
                FilePath.Text = saveDialog.FileName;
                TextEditor.SaveFile(FilePath.Text);
            }
        }
    }
}
