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
using Microsoft.Windows.Shell;

namespace Bend
{
    /// <summary>
    /// Interaction logic for ShadowWindow.xaml
    /// </summary>
    public partial class ShadowWindow : Window
    {
        public ShadowWindow(Window targetWindow)
        {
            InitializeComponent();
            this.targetWindow = targetWindow;
            this.Width = targetWindow.Width + 30;
            this.Height = targetWindow.Height + 30;
            this.Top = targetWindow.Top - 15;
            this.Left = targetWindow.Left - 15;
            targetWindow.SizeChanged += new SizeChangedEventHandler(targetWindow_SizeChanged);
            targetWindow.LocationChanged += new EventHandler(targetWindow_LocationChanged);
            targetWindow.StateChanged += new EventHandler(targetWindow_StateChanged);
            this.Owner = targetWindow;
            this.Shadow.Opacity = 0.0;
            this.Show();
            fadeInTimer = new System.Timers.Timer();
            fadeInTimer.Elapsed += new System.Timers.ElapsedEventHandler(fadeInTimer_Elapsed);
            isMinimized = false;
            compositionRendered = new EventHandler(CompositionTarget_Rendering);
            CompositionTarget.Rendering += compositionRendered;
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            CompositionTarget.Rendering -= compositionRendered;
            fadeInTimer.Interval = 300;
            fadeInTimer.Start();
        }


        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }

        void targetWindow_StateChanged(object sender, EventArgs e)
        {
            fadeInTimer.Stop();

            if (((Window)sender).WindowState == System.Windows.WindowState.Minimized)
            {
                this.Shadow.Opacity = 0.0;
                this.isMinimized = true;
            }
            else if (((Window)sender).WindowState == System.Windows.WindowState.Maximized)
            {
                this.Shadow.Opacity = 0.0;
                this.isMinimized = false;
            }
            else if (((Window)sender).WindowState == System.Windows.WindowState.Normal)
            {
                if (this.isMinimized)
                {
                    fadeInTimer.Interval = 350;
                    fadeInTimer.Start();
                }
                else
                {
                    this.Shadow.Opacity = 1.0;
                    this.Show();
                }                
                this.isMinimized = false;
            }
        }

        void fadeInTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(delegate { this.Shadow.Opacity = 1.0; this.Show(); }));
            fadeInTimer.Stop();
        }

        void targetWindow_LocationChanged(object sender, EventArgs e)
        {
            this.Top = targetWindow.Top - 15;
            this.Left = targetWindow.Left - 15;            
        }

        void targetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = targetWindow.Width + 30;
            this.Height = targetWindow.Height + 30;
        }
        
        System.Timers.Timer fadeInTimer;    
        Window targetWindow;
        bool isMinimized;
        EventHandler compositionRendered;
    }
}
