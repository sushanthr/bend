using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Bend.Controls
{
    public class MinimumThumbTrack : System.Windows.Controls.Primitives.Track
    {
        private const double MinimumThumbLength = 40.0;

        protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeSize)
        {
            System.Windows.Size arrangedSize = base.ArrangeOverride(arrangeSize);
            if (this.Thumb == null)
                return arrangedSize;

            double trackLength = this.Orientation == System.Windows.Controls.Orientation.Vertical
                ? arrangeSize.Height : arrangeSize.Width;
            if (trackLength <= 0)
                return arrangedSize;

            double range = Math.Max(0, this.Maximum - this.Minimum);
            double viewport = Double.IsNaN(this.ViewportSize) ? 0 : Math.Max(0, this.ViewportSize);
            double proportionalLength = range <= 0
                ? trackLength
                : trackLength * viewport / (range + viewport);
            double thumbLength = Math.Min(trackLength, Math.Max(MinimumThumbLength, proportionalLength));
            double travel = Math.Max(0, trackLength - thumbLength);
            double position = range <= 0 ? 0 : travel * (this.Value - this.Minimum) / range;
            position = Math.Max(0, Math.Min(travel, position));

            if (this.Orientation == System.Windows.Controls.Orientation.Vertical)
            {
                if (this.DecreaseRepeatButton != null)
                    this.DecreaseRepeatButton.Arrange(new System.Windows.Rect(0, 0, arrangeSize.Width, position));
                this.Thumb.Arrange(new System.Windows.Rect(0, position, arrangeSize.Width, thumbLength));
                if (this.IncreaseRepeatButton != null)
                    this.IncreaseRepeatButton.Arrange(new System.Windows.Rect(0, position + thumbLength,
                        arrangeSize.Width, trackLength - position - thumbLength));
            }
            else
            {
                if (this.DecreaseRepeatButton != null)
                    this.DecreaseRepeatButton.Arrange(new System.Windows.Rect(0, 0, position, arrangeSize.Height));
                this.Thumb.Arrange(new System.Windows.Rect(position, 0, thumbLength, arrangeSize.Height));
                if (this.IncreaseRepeatButton != null)
                    this.IncreaseRepeatButton.Arrange(new System.Windows.Rect(position + thumbLength, 0,
                        trackLength - position - thumbLength, arrangeSize.Height));
            }
            return arrangedSize;
        }
    }

    public class SearchResultEventArgs : RoutedEventArgs
    {
        public SearchResultEventArgs(RoutedEvent routedEvent, SearchResult result) : base(routedEvent) { this.Result = result; }
        public SearchResult Result { get; private set; }
    }

    public sealed class SearchFileGroup : INotifyPropertyChanged
    {
        public SearchFileGroup(string displayPath) { this.DisplayPath = displayPath; }
        public string DisplayPath { get; private set; }
        public ObservableCollection<SearchResult> Matches { get; private set; } = new ObservableCollection<SearchResult>();
        public int HitCount { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Add(SearchResult result)
        {
            this.Matches.Add(result);
            this.HitCount++;
            if (this.PropertyChanged != null) this.PropertyChanged(this, new PropertyChangedEventArgs("HitCount"));
        }
    }

    public sealed class SearchResult
    {
        public string FullPath { get; set; }
        public int Line { get; set; }
        public string SearchText { get; set; }
        public string DisplayPath { get; set; }
        public string Preview { get; set; }
        public string LineLabel { get { return "Line " + this.Line; } }
    }

    public partial class SearchPanel : UserControl
    {
        private static readonly Regex FindstrLine = new Regex("^(.*):(\\d+):(.*)$", RegexOptions.Compiled);
        private readonly ObservableCollection<SearchFileGroup> results = new ObservableCollection<SearchFileGroup>();
        private readonly Dictionary<string, SearchFileGroup> resultGroups = new Dictionary<string, SearchFileGroup>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource searchCancellation;
        private string rootPath;
        private int matchCount;

        public SearchPanel()
        {
            InitializeComponent();
            this.Results.ItemsSource = this.results;
            this.Status.Text = "Open a folder to search";
            this.HitSummary.Text = "0 hits";
        }

        public static readonly DependencyProperty RootPathProperty = DependencyProperty.Register(
            "RootPath", typeof(string), typeof(SearchPanel), new PropertyMetadata(null, RootPathChanged));
        public string RootPath { get { return (string)GetValue(RootPathProperty); } set { SetValue(RootPathProperty, value); } }

        public static readonly RoutedEvent ResultInvokedEvent = EventManager.RegisterRoutedEvent(
            "ResultInvoked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SearchPanel));
        public event RoutedEventHandler ResultInvoked { add { AddHandler(ResultInvokedEvent, value); } remove { RemoveHandler(ResultInvokedEvent, value); } }

        private static void RootPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            SearchPanel panel = (SearchPanel)sender;
            panel.rootPath = e.NewValue as string;
            panel.CancelSearch();
            panel.results.Clear();
            panel.resultGroups.Clear();
            panel.matchCount = 0;
            panel.HitSummary.Text = "0 hits";
            panel.Status.Visibility = Visibility.Visible;
            panel.Status.Text = String.IsNullOrWhiteSpace(panel.rootPath) ? "Open a folder to search" : "Enter a search term";
        }

        private void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(this.QueryBox.Text))
            {
                this.CancelSearch();
                this.results.Clear();
                this.Status.Visibility = Visibility.Visible;
                this.Status.Text = "Enter a search term";
            }
        }

        private void QueryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Search_Click(sender, e); e.Handled = true; }
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string query = this.QueryBox.Text;
            if (String.IsNullOrWhiteSpace(this.rootPath) || !Directory.Exists(this.rootPath)) { this.Status.Text = "Open a folder to search"; return; }
            if (String.IsNullOrWhiteSpace(query)) return;
            this.CancelSearch();
            this.searchCancellation = new CancellationTokenSource();
            CancellationToken token = this.searchCancellation.Token;
            this.results.Clear();
            this.resultGroups.Clear();
            this.matchCount = 0;
            this.HitSummary.Text = "0 hits";
            this.Status.Visibility = Visibility.Visible;
            this.Status.Text = "Searching...";
            try
            {
                SearchOutput output = await RunFindstrAsync(this.rootPath, query, token, result =>
                {
                    if (!token.IsCancellationRequested) this.AddResult(result);
                });
                if (token.IsCancellationRequested) return;
                this.Status.Visibility = this.matchCount == 0 ? Visibility.Visible : Visibility.Collapsed;
                this.Status.Text = output.Error == null ? (this.matchCount == 0 ? "No results" : this.matchCount + " result(s)") : output.Error;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { this.Status.Text = "Search failed: " + exception.Message; }
        }

        private void AddResult(SearchResult result)
        {
            SearchFileGroup group;
            if (!this.resultGroups.TryGetValue(result.FullPath, out group))
            {
                group = new SearchFileGroup(result.DisplayPath);
                this.resultGroups.Add(result.FullPath, group);
                this.results.Add(group);
            }
            this.matchCount++;
            group.Add(result);
            this.HitSummary.Text = this.matchCount + (this.matchCount == 1 ? " hit" : " hits");
        }

        private static async Task<SearchOutput> RunFindstrAsync(string root, string query, CancellationToken token, Action<SearchResult> resultReceived)
        {
            ProcessStartInfo info = new ProcessStartInfo("findstr.exe", "/S /N /I /P /C:" + QuoteArgument(query) + " *")
            {
                WorkingDirectory = root, CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default, StandardErrorEncoding = Encoding.Default
            };
            using (Process process = new Process { StartInfo = info })
            {
                process.Start();
                using (token.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(); }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }))
                {
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    string line;
                    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                    {
                        token.ThrowIfCancellationRequested();
                        Match match = FindstrLine.Match(line);
                        int lineNumber;
                        if (!match.Success || !Int32.TryParse(match.Groups[2].Value, out lineNumber)) continue;
                        string relativePath = match.Groups[1].Value.Trim();
                        string fullPath;
                        try
                        {
                            fullPath = Path.GetFullPath(Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(root, relativePath));
                        }
                        catch (ArgumentException) { continue; }
                        catch (NotSupportedException) { continue; }
                        resultReceived(new SearchResult { FullPath = fullPath, DisplayPath = relativePath, Line = lineNumber, SearchText = query, Preview = match.Groups[3].Value.Trim() });
                    }
                    token.ThrowIfCancellationRequested();
                    await Task.Run(() => process.WaitForExit());
                    string error = (await errorTask).Trim();
                    SearchOutput output = new SearchOutput();
                    if (process.ExitCode > 1 && error.Length > 0) output.Error = error;
                    return output;
                }
            }
        }

        private static string QuoteArgument(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

        private void Result_Click(object sender, RoutedEventArgs e)
        {
            SearchResult result = ((FrameworkElement)sender).Tag as SearchResult;
            if (result != null) RaiseEvent(new SearchResultEventArgs(ResultInvokedEvent, result));
        }

        private void CancelSearch()
        {
            if (this.searchCancellation != null) this.searchCancellation.Cancel();
            this.searchCancellation = null;
        }

        private sealed class SearchOutput
        {
            internal string Error;
        }
    }
}
