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
using System.Windows.Interop;
using System.Runtime.InteropServices;
using TextCoreControl;

namespace Bend
{
    /// <summary>
    /// Interaction logic for TabDragVisual.xaml
    /// </summary>
    public partial class TabDragVisual : Window
    {
        internal TabDragVisual(TextEditor textEditor, TabTitle tabTitle)
        {
            InitializeComponent();
            Visibility initialVisibility = textEditor.Visibility;
            textEditor.Visibility = System.Windows.Visibility.Visible;
            this.textEditor = textEditor;
            Dispatcher.BeginInvoke(
            new Action(
                delegate {
                    try
                    {
                        EditorSnapShot.Source = textEditor.Rasterize();
                        textEditor.UnRasterize();
                        textEditor.Visibility = initialVisibility;
                        this.Focus();
                    }
                    catch
                    {
                        textEditor.UnRasterize();
                    }
                }
            ));            
            EditorSnapShot.Height = textEditor.ActualHeight ;
            EditorSnapShot.Width = textEditor.ActualWidth;

            FileName.Text = tabTitle.TitleText;
            Point tabPosition = tabTitle.TranslatePoint(new Point(), (UIElement)tabTitle.Parent);
            Tab.Margin = new Thickness(tabPosition.X + 70,0,0,0);
        }

        internal void UpdatePosition(Window mainwindow)
        {
            this.Top = mainwindow.Top;
            this.Left = mainwindow.Left;
        }

        TextEditor textEditor;
    }
}
