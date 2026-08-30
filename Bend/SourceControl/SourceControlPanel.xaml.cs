using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextCoreControl;
using System.ComponentModel;

namespace Bend.SourceControl
{
    public sealed class ChangeGroup : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public GitChangeLayer Layer { get; set; }
        public Visibility StageAllVisibility { get; set; } = Visibility.Collapsed;
        public ObservableCollection<ScmTreeNode> Nodes { get; private set; } = new ObservableCollection<ScmTreeNode>();
        private bool isExpanded = true;
        public bool IsExpanded { get { return isExpanded; } set { if (isExpanded == value) return; isExpanded = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("IsExpanded")); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class ScmTreeNode : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public GitChange Change { get; set; }
        public string CommitPatch { get; set; }
        public string CommitKey { get; set; }
        public bool IsAdded { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsRenamed { get; set; }
        public string OriginalPath { get; set; }
        public ObservableCollection<ScmTreeNode> Children { get; private set; } = new ObservableCollection<ScmTreeNode>();
        public bool IsDirectory { get { return Change == null; } }
        private bool isExpanded = true;
        public bool IsExpanded { get { return isExpanded; } set { if (isExpanded == value) return; isExpanded = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("IsExpanded")); } }
        public event PropertyChangedEventHandler PropertyChanged;
        public string IconGlyph { get { return IsDirectory ? "\uEAF7" : "\uEA7B"; } }
        public string StatusText { get { return CommitPatch != null ? (IsAdded ? "A" : (IsDeleted ? "D" : (IsRenamed ? "R" : null))) : (Change == null ? null : Change.StatusText); } }
        public string DisplayToolTip { get { return IsRenamed ? "Renamed from " + OriginalPath : FullPath; } }
        public string ActionGlyph { get { return Change == null ? null : Change.ActionGlyph; } }
        public string ActionToolTip { get { return Change == null ? null : Change.ActionToolTip; } }
        public Visibility ActionVisibility { get { return Change == null || CommitPatch != null ? Visibility.Collapsed : Visibility.Visible; } }
    }

    public sealed class DiffRequestedEventArgs : EventArgs
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Patch { get; set; }
        public DiffViewMode Mode { get; set; }
        public bool IsPinned { get; set; }
        public string FileName { get; set; }
        public string BaseText { get; set; }
        public string CurrentText { get; set; }
    }

    public partial class SourceControlPanel : UserControl
    {
        private readonly IGitService git;
        private CancellationTokenSource cancellation;
        private CancellationTokenSource diffCancellation;
        private GitRepositoryStatus status;
        private bool updatingBranches;

        public SourceControlPanel() : this(new GitService()) { }
        internal SourceControlPanel(IGitService gitService)
        {
            git = gitService; InitializeComponent(); DataContext = this;
        }

        public ObservableCollection<ChangeGroup> ChangeGroups { get; private set; } = new ObservableCollection<ChangeGroup>();
        public ObservableCollection<ChangeGroup> CommitChangeGroups { get; private set; } = new ObservableCollection<ChangeGroup>();
        public ObservableCollection<GitCommit> Commits { get; private set; } = new ObservableCollection<GitCommit>();
        public DiffViewMode DiffMode { get { return DiffViewMode.Inline; } }
        public event EventHandler<DiffRequestedEventArgs> DiffRequested;
        public event EventHandler DiffModeChanged;

        public static readonly DependencyProperty WorkspacePathProperty = DependencyProperty.Register("WorkspacePath", typeof(string), typeof(SourceControlPanel), new PropertyMetadata(null, WorkspaceChanged));
        public string WorkspacePath { get { return (string)GetValue(WorkspacePathProperty); } set { SetValue(WorkspacePathProperty, value); } }
        private static void WorkspaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((SourceControlPanel)d).RefreshAsync(); }

        public async void RefreshAsync()
        {
            if (cancellation != null) cancellation.Cancel();
            cancellation = new CancellationTokenSource(); CancellationToken token = cancellation.Token;
            ChangeGroups.Clear(); CommitChangeGroups.Clear(); Commits.Clear(); ErrorText.Text = ""; StateText.Text = string.IsNullOrWhiteSpace(WorkspacePath) ? "Open a folder to use Source Control." : "Loading repository…";
            CommitDescription.Text = "Select a commit from history."; CommitMetadata.Text = "";
            BranchPicker.ItemsSource = null;
            if (string.IsNullOrWhiteSpace(WorkspacePath)) return;
            try
            {
                status = await git.GetStatusAsync(WorkspacePath, token);
                BuildGroups(); StateText.Text = status.Changes.Count == 0 ? "No changes." : "";
                var branches = await git.GetBranchesAsync(status.RepositoryRoot, token);
                updatingBranches = true; BranchPicker.ItemsSource = branches;
                string selected = branches.FirstOrDefault(b => String.Equals(b, status.Branch, StringComparison.Ordinal)) ?? branches.FirstOrDefault();
                BranchPicker.SelectedItem = selected; updatingBranches = false;
                await LoadLogAsync(selected, token); UpdateCommitEnabled();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { status = null; StateText.Text = "Source Control unavailable."; ErrorText.Text = ex.Message; }
        }

        private void BuildGroups()
        {
            ChangeGroups.Clear();
            AddGroup("Merge Changes", GitChangeLayer.Conflict); AddGroup("Staged Changes", GitChangeLayer.Staged);
            AddGroup("Changes", GitChangeLayer.Unstaged); AddGroup("Untracked", GitChangeLayer.Untracked);
        }
        private void AddGroup(string name, GitChangeLayer layer)
        {
            var values = status.Changes.Where(c => c.Layer == layer).ToList(); if (values.Count == 0) return;
            var group = new ChangeGroup
            {
                Name = name + " (" + values.Count + ")",
                Layer = layer,
                StageAllVisibility = layer == GitChangeLayer.Unstaged || layer == GitChangeLayer.Untracked
                    ? Visibility.Visible : Visibility.Collapsed
            };
            foreach (GitChange change in values.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)) AddTreePath(group.Nodes, change);
            SortTree(group.Nodes);
            ChangeGroups.Add(group);
        }
        private static void AddTreePath(ObservableCollection<ScmTreeNode> roots, GitChange change)
        {
            string[] parts = change.Path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            ObservableCollection<ScmTreeNode> level = roots;
            string path = "";
            for (int index = 0; index < parts.Length; index++)
            {
                path = path.Length == 0 ? parts[index] : path + "/" + parts[index];
                bool file = index == parts.Length - 1;
                ScmTreeNode node = level.FirstOrDefault(n => String.Equals(n.Name, parts[index], StringComparison.OrdinalIgnoreCase) && n.IsDirectory != file);
                if (node == null)
                {
                    node = new ScmTreeNode { Name = parts[index], FullPath = path, Change = file ? change : null };
                    level.Add(node);
                }
                level = node.Children;
            }
        }
        private static void SortTree(ObservableCollection<ScmTreeNode> nodes)
        {
            foreach (ScmTreeNode node in nodes) SortTree(node.Children);
            List<ScmTreeNode> sorted = nodes.OrderByDescending(n => n.IsDirectory).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
            nodes.Clear(); foreach (ScmTreeNode node in sorted) nodes.Add(node);
        }
        private void Folder_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = e.OriginalSource as TreeViewItem;
            ScmTreeNode node = item == null ? null : item.DataContext as ScmTreeNode;
            if (node == null || !node.IsDirectory) return;
            ExpandUntilFiles(node);
        }
        private static void ExpandUntilFiles(ScmTreeNode node)
        {
            if (node.Children.Any(child => !child.IsDirectory)) return;
            foreach (ScmTreeNode directory in node.Children.Where(child => child.IsDirectory))
            {
                directory.IsExpanded = true;
                ExpandUntilFiles(directory);
            }
        }
        private async Task LoadLogAsync(string branch, CancellationToken token)
        {
            Commits.Clear(); if (status == null) return;
            foreach (GitCommit commit in await git.GetLogAsync(status.RepositoryRoot, branch, 200, token)) Commits.Add(commit);
        }
        private async void BranchPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingBranches || status == null || BranchPicker.SelectedItem == null) return;
            try
            {
                CommitChangeGroups.Clear(); CommitDescription.Text = "Select a commit from history."; CommitMetadata.Text = "";
                await LoadLogAsync(BranchPicker.SelectedItem.ToString(), cancellation.Token);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
        }
        private void TopMode_Checked(object sender, RoutedEventArgs e)
        {
            if (ChangesTree == null) return; bool commit = CommitMode.IsChecked == true;
            ChangesTree.Visibility = commit ? Visibility.Collapsed : Visibility.Visible; CommitView.Visibility = commit ? Visibility.Visible : Visibility.Collapsed;
            StateText.Visibility = commit ? Visibility.Collapsed : Visibility.Visible;
        }
        private void DiffMode_Checked(object sender, RoutedEventArgs e) { if (DiffModeChanged != null) DiffModeChanged(this, EventArgs.Empty); }
        private void Refresh_Click(object sender, RoutedEventArgs e) { RefreshAsync(); }
        private async void Push_Click(object sender, RoutedEventArgs e) { if (status != null) await RunAndRefresh(() => git.PushAsync(status.RepositoryRoot, false, cancellation.Token)); }
        private async void ForcePush_Click(object sender, RoutedEventArgs e)
        {
            if (status != null && StyledMessageBox.Show("FORCE PUSH", "Force push the current branch using --force-with-lease?"))
                await RunAndRefresh(() => git.PushAsync(status.RepositoryRoot, true, cancellation.Token));
        }
        private async void Pull_Click(object sender, RoutedEventArgs e) { if (status != null) await RunAndRefresh(() => git.PullAsync(status.RepositoryRoot, cancellation.Token)); }
        private async void Fetch_Click(object sender, RoutedEventArgs e) { if (status != null) await RunAndRefresh(() => git.FetchAsync(status.RepositoryRoot, cancellation.Token)); }
        private async void StageToggle_Click(object sender, RoutedEventArgs e)
        {
            GitChange change = (sender as FrameworkElement)?.Tag as GitChange; if (change == null || status == null) return;
            e.Handled = true;
            if (change.Layer == GitChangeLayer.Staged) await RunAndRefresh(() => git.UnstageAsync(status.RepositoryRoot, change.Path, cancellation.Token));
            else await RunAndRefresh(() => git.StageAsync(status.RepositoryRoot, change.Path, cancellation.Token));
        }
        private async void StageAll_Click(object sender, RoutedEventArgs e)
        {
            ChangeGroup group = (sender as FrameworkElement)?.Tag as ChangeGroup;
            if (group == null || status == null) return;
            e.Handled = true;
            try
            {
                List<GitChange> changes = status.Changes.Where(change => change.Layer == group.Layer).ToList();
                foreach (GitChange change in changes)
                {
                    GitResult result = await git.StageAsync(status.RepositoryRoot, change.Path, cancellation.Token);
                    if (!result.Success)
                    {
                        ErrorText.Text = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                        return;
                    }
                }
                RefreshAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
        }
        private void UpdateCommitEnabled() { if (CreateCommitButton != null) CreateCommitButton.IsEnabled = status != null; }

        private async void ChangesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { ScmTreeNode node = e.NewValue as ScmTreeNode; if (node != null && node.Change != null) await OpenChangeAsync(node.Change, false); }
        private async void ChangesTree_DoubleClick(object sender, MouseButtonEventArgs e) { ScmTreeNode node = (sender as TreeView)?.SelectedItem as ScmTreeNode; if (node != null && node.Change != null) { await OpenChangeAsync(node.Change, true); e.Handled = true; } }
        private async Task OpenChangeAsync(GitChange change, bool pin)
        {
            CancellationToken token = BeginDiffRequest();
            try
            {
                ErrorText.Text = "";
                GitFileComparison comparison = await git.GetFileComparisonAsync(status.RepositoryRoot, change, token);
                RaiseFileDiff(change.Layer + ":" + change.Path, change.Path, comparison, pin);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
        }
        private async void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GitCommit commit = HistoryList.SelectedItem as GitCommit; if (commit == null || status == null) return;
            CancellationToken token = BeginDiffRequest();
            try
            {
                string patch = await git.GetCommitDiffAsync(status.RepositoryRoot, commit.Hash, token);
                if (token.IsCancellationRequested) return;
                ShowCommitDetails(commit, patch);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
        }
        private async void History_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            GitCommit commit = HistoryList.SelectedItem as GitCommit; if (commit == null || status == null) return;
            CancellationToken token = BeginDiffRequest();
            try
            {
                string patch = await git.GetCommitDiffAsync(status.RepositoryRoot, commit.Hash, token);
                if (token.IsCancellationRequested) return;
                ShowCommitDetails(commit, patch);
                RaiseDiff("commit:" + commit.Hash, commit.ShortHash + " — " + commit.Subject, patch, true);
                e.Handled = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
        }
        private void RaiseDiff(string key, string title, string patch, bool pin) { if (DiffRequested != null) DiffRequested(this, new DiffRequestedEventArgs { Key = key, Title = title, Patch = patch, Mode = DiffMode, IsPinned = pin }); }
        private void RaiseFileDiff(string key, string path, GitFileComparison comparison, bool pin)
        {
            if (DiffRequested != null) DiffRequested(this, new DiffRequestedEventArgs { Key = key, Title = path, FileName = path, BaseText = comparison.BaseText, CurrentText = comparison.CurrentText, Mode = DiffMode, IsPinned = pin });
        }

        private void ShowCommitDetails(GitCommit commit, string patch)
        {
            CommitDescription.Text = commit.Subject;
            CommitMetadata.Text = commit.ShortHash + " · " + commit.Author + " · " + commit.Date.ToString("g");
            CommitChangeGroups.Clear();
            List<CommitFileEntry> files = SplitCommitPatchByFile(patch);
            if (files.Count > 0)
            {
                var group = new ChangeGroup { Name = "Files changed (" + files.Count + ")" };
                foreach (CommitFileEntry file in files)
                    AddCommitTreePath(group.Nodes, file, commit.Hash);
                SortTree(group.Nodes);
                CommitChangeGroups.Add(group);
            }
            CommitMode.IsChecked = true;
        }

        private sealed class CommitFileEntry
        {
            public string Path { get; set; }
            public string Patch { get; set; }
            public bool IsAdded { get; set; }
            public bool IsDeleted { get; set; }
            public bool IsRenamed { get; set; }
            public string OriginalPath { get; set; }
        }

        private static List<CommitFileEntry> SplitCommitPatchByFile(string patch)
        {
            var result = new List<CommitFileEntry>();
            string normalized = (patch ?? String.Empty).Replace("\r\n", "\n");
            int start = normalized.IndexOf("diff --git ", StringComparison.Ordinal);
            while (start >= 0)
            {
                int next = normalized.IndexOf("\ndiff --git ", start + 1, StringComparison.Ordinal);
                string filePatch = next < 0 ? normalized.Substring(start) : normalized.Substring(start, next - start + 1);
                DiffModel model = DiffModel.Parse(filePatch);
                DiffFile file = model.Files.FirstOrDefault();
                string path = file == null ? null : (file.NewPath == "/dev/null" ? file.OldPath : file.NewPath);
                if (!String.IsNullOrWhiteSpace(path))
                    result.Add(new CommitFileEntry { Path = path, Patch = filePatch,
                        IsAdded = file.OldPath == "/dev/null", IsDeleted = file.NewPath == "/dev/null",
                        IsRenamed = !String.IsNullOrWhiteSpace(file.OldPath) && !String.IsNullOrWhiteSpace(file.NewPath) &&
                            file.OldPath != "/dev/null" && file.NewPath != "/dev/null" &&
                            !String.Equals(file.OldPath, file.NewPath, StringComparison.Ordinal),
                        OriginalPath = file.OldPath });
                start = next < 0 ? -1 : next + 1;
            }
            return result;
        }

        private static void AddCommitTreePath(ObservableCollection<ScmTreeNode> roots, CommitFileEntry fileEntry, string commitHash)
        {
            string filePath = fileEntry.Path;
            string[] parts = filePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            ObservableCollection<ScmTreeNode> level = roots;
            string path = "";
            for (int index = 0; index < parts.Length; index++)
            {
                path = path.Length == 0 ? parts[index] : path + "/" + parts[index];
                bool file = index == parts.Length - 1;
                ScmTreeNode node = level.FirstOrDefault(n => String.Equals(n.Name, parts[index], StringComparison.OrdinalIgnoreCase) && n.IsDirectory != file);
                if (node == null)
                {
                    node = new ScmTreeNode { Name = parts[index], FullPath = path,
                        Change = file ? new GitChange { Path = filePath, Layer = GitChangeLayer.Staged } : null,
                        CommitPatch = file ? fileEntry.Patch : null, CommitKey = file ? commitHash : null,
                        IsAdded = file && fileEntry.IsAdded, IsDeleted = file && fileEntry.IsDeleted,
                        IsRenamed = file && fileEntry.IsRenamed, OriginalPath = file ? fileEntry.OriginalPath : null };
                    level.Add(node);
                }
                level = node.Children;
            }
        }

        private void CommitTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ScmTreeNode node = e.NewValue as ScmTreeNode;
            if (node != null && node.CommitPatch != null)
                RaiseDiff("commit:" + node.CommitKey + ":" + node.FullPath, node.Name, node.CommitPatch, false);
        }

        private void CommitTree_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ScmTreeNode node = CommitFilesTree.SelectedItem as ScmTreeNode;
            if (node != null && node.CommitPatch != null)
            {
                RaiseDiff("commit:" + node.CommitKey + ":" + node.FullPath, node.Name, node.CommitPatch, true);
                e.Handled = true;
            }
        }
        private CancellationToken BeginDiffRequest()
        {
            if (diffCancellation != null) diffCancellation.Cancel();
            diffCancellation = cancellation == null ? new CancellationTokenSource() : CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            return diffCancellation.Token;
        }

        private void ChangesTree_RightClick(object sender, MouseButtonEventArgs e)
        {
            TreeView tree = sender as TreeView; ScmTreeNode node = FindDataContext<ScmTreeNode>(e.OriginalSource as DependencyObject); GitChange change = node == null ? null : node.Change; if (change == null) return;
            var menu = new ContextMenu();
            AddMenu(menu, "Open Changes", async () => await OpenChangeAsync(change, true));
            if (change.Layer == GitChangeLayer.Staged) AddMenu(menu, "Unstage", async () => await RunAndRefresh(() => git.UnstageAsync(status.RepositoryRoot, change.Path, cancellation.Token)));
            else AddMenu(menu, "Stage", async () => await RunAndRefresh(() => git.StageAsync(status.RepositoryRoot, change.Path, cancellation.Token)));
            if (change.Layer != GitChangeLayer.Staged) AddMenu(menu, "Discard Changes…", async () => { if (StyledMessageBox.Show("DISCARD CHANGES", "Discard changes to " + change.Path + "? This cannot be undone.")) await RunAndRefresh(() => git.DiscardAsync(status.RepositoryRoot, change, cancellation.Token)); });
            tree.ContextMenu = menu; menu.IsOpen = true; e.Handled = true;
        }
        private static void AddMenu(ContextMenu menu, string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (s, e) => action(); menu.Items.Add(item); }
        private static T FindDataContext<T>(DependencyObject current) where T : class { while (current != null) { FrameworkElement f = current as FrameworkElement; if (f != null && f.DataContext is T) return (T)f.DataContext; current = VisualTreeHelper.GetParent(current); } return null; }
        private async Task RunAndRefresh(Func<Task<GitResult>> operation) { GitResult result = await operation(); if (!result.Success) ErrorText.Text = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error; else RefreshAsync(); }
        private async void CreateCommit_Click(object sender, RoutedEventArgs e)
        {
            if (status == null) return;
            if (!status.Changes.Any(c => c.Layer == GitChangeLayer.Staged))
            {
                StyledMessageBox.Show("COMMIT CHANGES", "Stage at least one changed file before creating a commit.");
                return;
            }
            var dialog = new CommitMessageDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            CreateCommitButton.IsEnabled = false;
            try { GitResult result = await git.CommitAsync(status.RepositoryRoot, dialog.CommitMessage, cancellation.Token); if (!result.Success) ErrorText.Text = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error; else RefreshAsync(); }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
            finally { UpdateCommitEnabled(); }
        }
    }
}
