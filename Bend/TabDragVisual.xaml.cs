using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TextCoreControl;

namespace Bend
{
    /// <summary>
    /// Interaction logic for TabDragVisual.xaml
    /// </summary>
    public partial class TabDragVisual : Window
    {
        public TabDragVisual(TextEditor textEditor, string fileName)
        {
            InitializeComponent();
            Visibility initialVisibility = textEditor.Visibility;
            textEditor.Visibility = System.Windows.Visibility.Visible;
            this.textEditor = textEditor;
            Dispatcher.BeginInvoke(
            new Action(
                delegate {
                    EditorSnapShot.Source = textEditor.Rasterize();
                    textEditor.UnRasterize();
                    textEditor.Visibility = initialVisibility;
                    this.Focus();
                }
            ));            
            EditorSnapShot.Height = textEditor.ActualHeight * 0.75 ;
            EditorSnapShot.Width = textEditor.ActualWidth * 0.75;
            Title.Text = fileName;
        }

        TextEditor textEditor;
    }
}
