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
        public TabDragVisual(TextEditor textEditor, string tabTitleText)
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

            Title.Text = tabTitleText;
        }

        #region Windows API
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }
        #endregion  

        internal void UpdatePosition()
        {
            Point mousePosition = GetMousePosition();

            this.Top = mousePosition.Y + 5;
            this.Left = mousePosition.X - Title.ActualWidth / 2;
        }

        TextEditor textEditor;
    }
}
