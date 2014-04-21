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
            TextEditor.CancelSelect();
            ProcessCommandLine();
        }

        private void ProcessCommandLine()
        {
            string[] commandLineArguments = Environment.GetCommandLineArgs();
            bool settingsChanged = false;
            foreach ( string argument in commandLineArguments)
            {
                if (argument.StartsWith("/playback="))
                {
                    string playBackRecord = argument.Substring("/playback=".Length);
                    TextEditor.PlaybackFlightRecord(playBackRecord);
                }
                else if (argument == "/hud")
                {
                    settingsChanged = true;
                    TextCoreControl.Settings.ShowDebugHUD = true;
                }
                else if (argument == "/linenumber")
                {
                    settingsChanged = true;
                    TextCoreControl.Settings.ShowLineNumber = true;
                }
                else if (argument == "/showformatting")
                {
                    settingsChanged = true;
                    TextCoreControl.Settings.ShowFormatting = true;
                }
                else if (argument == "/exit")
                {
                    TextEditor.ExitAfterPlayback();
                }
                else if (argument == "/hide")
                {
                    this.Top = -5000;
                    this.ShowInTaskbar = false;
                }
            }
            if (settingsChanged)
            {
                TextEditor.NotifyOfSettingsChange();
            }
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

        private void showLineNumnber_Click(object sender, RoutedEventArgs e)
        {
            TextCoreControl.Settings.ShowLineNumber = false;
            if (showLineNumnber.IsChecked.HasValue)
            {
                TextCoreControl.Settings.ShowLineNumber = showLineNumnber.IsChecked.Value;
            }
            TextEditor.NotifyOfSettingsChange();
        }

        private void showHUD_Click(object sender, RoutedEventArgs e)
        {
            TextCoreControl.Settings.ShowDebugHUD = false;
            if (showHUD.IsChecked.HasValue)
            {
                TextCoreControl.Settings.ShowDebugHUD = showHUD.IsChecked.Value;
            }
        }

        private void TextCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string command = TextCommand.Text;
                if (command[0] == 'g' || command[0] == 'G')
                {
                    string lineNumber = command.Substring(1);
                    int lineNumberInt = int.Parse(lineNumber);
                    TextEditor.DisplayManager.ScrollToContentLineNumber(lineNumberInt, /*moveCaret*/false);
                }
                else if (command.IndexOf("show", StringComparison.OrdinalIgnoreCase) >= 0 && command.IndexOf("formatting", StringComparison.OrdinalIgnoreCase) >= 0)  
                {
                    TextCoreControl.Settings.ShowFormatting = !TextCoreControl.Settings.ShowFormatting;                    
                    TextEditor.NotifyOfSettingsChange();
                }
            }
        }                
    }
}
