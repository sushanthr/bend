using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using Bend.Controls;

namespace Bend
{
    public partial class LoopsTasksPanel : UserControl
    {
        private sealed class LoopRunTag
        {
            public LoopDefinition Loop;
            public ProgressBar Progress;
            public TextBlock Status;
        }

        private readonly System.Collections.Generic.Dictionary<string, CancellationTokenSource> runningLoops = new System.Collections.Generic.Dictionary<string, CancellationTokenSource>();
        public delegate void PathRequestedHandler(object sender, string path);
        public event PathRequestedHandler FileOpenRequested;
        public LoopsTasksPanel() { InitializeComponent(); Refresh(); }
        public void Refresh()
        {
            LoopsTasksStorage.EnsureFolders(); LoopsList.Children.Clear(); TasksList.Children.Clear();
            foreach (LoopDefinition loop in LoopsTasksStorage.LoadLoops()) AddLoop(loop);
            foreach (TaskDefinition task in LoopsTasksStorage.LoadTasks()) AddTask(task);
        }
        private void AddLoop(LoopDefinition loop)
        {
            Expander expander = new Expander { Header = loop.Name, IsExpanded = false, Style = (Style)FindResource("PaneExpanderStyle") };
            expander.ContextMenu = Menu(loop.FolderPath, true);
            StackPanel body = new StackPanel();
            Grid controls = new Grid { Margin = new Thickness(23, 0, 7, 4) };
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ProgressBar progress = new ProgressBar { Style = (Style)FindResource("PaneProgressStyle"), Margin = new Thickness(0, 0, 8, 0), Minimum = 0, Maximum = loop.MaxIterations, VerticalAlignment = VerticalAlignment.Center };
            controls.Children.Add(progress);

            TextBlock status = new TextBlock { Text = "0 / " + loop.MaxIterations, Margin = new Thickness(0, 0, 6, 0), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Foreground = FindBrush("ShellMutedBrush") };
            Grid.SetColumn(status, 1); controls.Children.Add(status);

            Button play = Action("▶", "Run loop", (s, e) => { });
            Grid.SetColumn(play, 2); controls.Children.Add(play);
            Button stop = Action("■", "Stop loop", (s, e) => { });
            Grid.SetColumn(stop, 3); controls.Children.Add(stop);

            body.Children.Add(controls); AddFiles(body, loop.FolderPath); expander.Content = body; LoopsList.Children.Add(expander);
            LoopRunTag tag = new LoopRunTag { Loop = loop, Progress = progress, Status = status }; play.Tag = tag; stop.Tag = tag;
            play.Click += RunLoop_Click; stop.Click += StopLoop_Click;
        }
        private void AddTask(TaskDefinition task)
        {
            Expander expander = new Expander { Header = task.Name, IsExpanded = false, Style = (Style)FindResource("PaneExpanderStyle") }; expander.ContextMenu = Menu(task.FolderPath, false);
            StackPanel body = new StackPanel(); Grid row = new Grid { Margin = new Thickness(23, 0, 7, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock status = new TextBlock { Margin = new Thickness(0, 0, 6, 0), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = FindBrush("ShellMutedBrush") };
            SetTaskStatus(status, task);
            row.Children.Add(status);

            string lastLogFile = FindLastRunLogFile(task);
            if (lastLogFile != null)
            {
                Button last = Action(Path.GetFileName(lastLogFile), "Open last run log", (s, e) => FileOpenRequested?.Invoke(this, lastLogFile));
                last.Content = new TextBlock { Text = Path.GetFileName(lastLogFile), TextDecorations = TextDecorations.Underline };
                Grid.SetColumn(last, 1); row.Children.Add(last);
            }

            Button agent = Action("", "Open agent in task folder", (s, e) => { }); agent.Tag = task;
            agent.Content = new TextBlock { Text = "\uEC67", FontFamily = (FontFamily)FindResource("CodiconFontFamily"), FontSize = 16 };
            Grid.SetColumn(agent, 2); row.Children.Add(agent);
            Button run = Action("▶", "Run task now", (s, e) => { }); run.Tag = task;
            Grid.SetColumn(run, 3); row.Children.Add(run);
            body.Children.Add(row); AddFiles(body, task.FolderPath); expander.Content = body; TasksList.Children.Add(expander);
            agent.Click += (s, e) => AgentRequested?.Invoke(this, task.FolderPath);
            run.Click += async (s, e) => { run.IsEnabled = false; status.Text = "Last Run: running (status: running)"; try { await ScheduledTaskEngine.RunTaskAsync(task); } catch (Exception exception) { status.Text = "Last Run: now (status: failed)"; MessageBox.Show(exception.Message, "Task failed", MessageBoxButton.OK, MessageBoxImage.Error); } finally { run.IsEnabled = true; Refresh(); } };
        }

        private void SetTaskStatus(TextBlock status, TaskDefinition task)
        {
            status.Inlines.Clear();
            if (!task.LastRun.HasValue)
            {
                status.Inlines.Add(new Run("Last Run: Never"));
                return;
            }

            string runStatus = String.IsNullOrWhiteSpace(task.LastStatus) ? "completed" : task.LastStatus.Trim().ToLowerInvariant();
            bool succeeded = runStatus.Equals("completed", StringComparison.OrdinalIgnoreCase);
            status.Inlines.Add(new Run("Last Run: " + task.LastRun.Value.ToString("g") + " "));
            status.Inlines.Add(new Run(succeeded ? "\uEBB3" : "\uEA6C")
            {
                FontFamily = (FontFamily)FindResource("CodiconFontFamily"),
                Foreground = FindBrush(succeeded ? "SourceControlStatusBrush" : "ErrorForegroundBrush")
            });
        }

        private static string FindLastRunLogFile(TaskDefinition task)
        {
            if (String.IsNullOrWhiteSpace(task.LastLogPath) || !Directory.Exists(task.LastLogPath)) return null;
            string[] names = { "run.log", "trigger.log", "wakeup.log", "status.txt" };
            foreach (string name in names)
            {
                string path = Path.Combine(task.LastLogPath, name);
                if (File.Exists(path)) return path;
            }
            return Directory.GetFiles(task.LastLogPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }
        private void AddFiles(Panel panel, string folder)
        {
            FolderTree tree = new FolderTree
            {
                RootPath = folder,
                ShowRoot = false
            };
            tree.FileInvoked += Tree_FileInvoked;
            panel.Children.Add(tree);
        }

        private void Tree_FileInvoked(object sender, RoutedEventArgs e)
        {
            FolderTreeNode node = e.OriginalSource as FolderTreeNode;
            if (node != null) FileOpenRequested?.Invoke(this, node.FullPath);
        }
        private ContextMenu Menu(string folder, bool loop)
        {
            ContextMenu menu = new ContextMenu(); MenuItem rename = new MenuItem { Header = "Rename" }; rename.Click += (s, e) => Rename(folder); MenuItem delete = new MenuItem { Header = "Delete" }; delete.Click += (s, e) => { if (MessageBox.Show("Delete this item and all its files?", "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { Directory.Delete(folder, true); Refresh(); } }; menu.Items.Add(rename); menu.Items.Add(delete); return menu;
        }
        private void Rename(string folder) { InputDialog dialog = new InputDialog("Rename", Path.GetFileName(folder)); if (dialog.ShowDialog() == true && !String.IsNullOrWhiteSpace(dialog.Value)) { Directory.Move(folder, Path.Combine(Path.GetDirectoryName(folder), dialog.Value.Trim())); Refresh(); } }
        private Button Action(string text, string tip, RoutedEventHandler click) { Button b = new Button { Content = text, ToolTip = tip, Style = (Style)FindResource("PaneActionButtonStyle") }; b.Click += click; return b; }
        private Brush FindBrush(string key) { return (Brush)TryFindResource(key) ?? Brushes.Gray; }
        private void NewLoop_Click(object sender, RoutedEventArgs e) { LoopDefinition loop = LoopsTasksStorage.CreateLoop(); Refresh(); FileOpenRequested?.Invoke(this, loop.PromptPath); }
        private void NewTask_Click(object sender, RoutedEventArgs e) { TaskDefinition task = LoopsTasksStorage.CreateTask(); Refresh(); FileOpenRequested?.Invoke(this, task.ConfigPath); }
        private async void RunLoop_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender; LoopRunTag data = (LoopRunTag)button.Tag; if (runningLoops.ContainsKey(data.Loop.FolderPath)) return;
            CancellationTokenSource source = new CancellationTokenSource(); runningLoops[data.Loop.FolderPath] = source; button.IsEnabled = false; data.Status.Text = "Starting…";
            try { await LoopsTasksRunner.RunLoopAsync(data.Loop, LoopsTasksStorage.GetConfiguredAgentTemplate(), (iteration, output) => Dispatcher.Invoke(() => { data.Progress.Value = iteration; data.Status.Text = output.IndexOf("/terminate_loop", StringComparison.OrdinalIgnoreCase) >= 0 ? "Complete" : iteration + " / " + data.Loop.MaxIterations; }), source.Token); }
            catch (OperationCanceledException) { data.Status.Text = "Stopped"; }
            catch (Exception exception) { data.Status.Text = "Failed"; MessageBox.Show(exception.Message, "Loop failed", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { runningLoops.Remove(data.Loop.FolderPath); button.IsEnabled = true; }
        }
        private void StopLoop_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender; LoopRunTag data = (LoopRunTag)button.Tag; CancellationTokenSource source; if (runningLoops.TryGetValue(data.Loop.FolderPath, out source)) source.Cancel();
        }
        public event PathRequestedHandler AgentRequested;
    }
    internal sealed class InputDialog : Window
    {
        private readonly TextBox box; public string Value { get { return box.Text; } }
        public InputDialog(string title, string value) { Title = title; Width = 320; Height = 125; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; StackPanel p = new StackPanel { Margin = new Thickness(12) }; box = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 10) }; p.Children.Add(box); Button ok = new Button { Content = "OK", IsDefault = true, Width = 70, HorizontalAlignment = HorizontalAlignment.Right }; ok.Click += (s, e) => { DialogResult = true; }; p.Children.Add(ok); Content = p; }
    }
}
