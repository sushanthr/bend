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
using System.IO;
using System.Windows.Threading;

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
        private CancellationTokenSource watcherCancellation;
        private GitRepositoryStatus status;
        private bool updatingBranches;
        private volatile bool runningGitOperation;
        private FileSystemWatcher workspaceWatcher;
        private string watchedWorkspacePath;
        private bool watcherNeedsFullRefresh;
        private readonly DispatcherTimer watcherRefreshTimer;

        public SourceControlPanel() : this(new GitService()) { }
        internal SourceControlPanel(IGitService gitService)
        {
            git = gitService;
            watcherRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            watcherRefreshTimer.Tick += WatcherRefreshTimer_Tick;
            InitializeComponent();
            DataContext = this;
            Loaded += SourceControlPanel_Loaded;
            Unloaded += SourceControlPanel_Unloaded;
        }

        public ObservableCollection<ChangeGroup> ChangeGroups { get; private set; } = new ObservableCollection<ChangeGroup>();
        public ObservableCollection<ChangeGroup> CommitChangeGroups { get; private set; } = new ObservableCollection<ChangeGroup>();
        public ObservableCollection<GitCommit> Commits { get; private set; } = new ObservableCollection<GitCommit>();
        public ObservableCollection<GitReflogEntry> ReflogEntries { get; private set; } = new ObservableCollection<GitReflogEntry>();
        public DiffViewMode DiffMode { get { return DiffViewMode.Inline; } }
        public event EventHandler<DiffRequestedEventArgs> DiffRequested;
        public event EventHandler DiffModeChanged;

        public static readonly DependencyProperty WorkspacePathProperty = DependencyProperty.Register("WorkspacePath", typeof(string), typeof(SourceControlPanel), new PropertyMetadata(null, WorkspaceChanged));
        public string WorkspacePath { get { return (string)GetValue(WorkspacePathProperty); } set { SetValue(WorkspacePathProperty, value); } }
        private static void WorkspaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SourceControlPanel panel = (SourceControlPanel)d;
            panel.WatchWorkspace(e.NewValue as string);
            panel.RefreshAsync();
        }

        private void WatchWorkspace(string path)
        {
            watcherRefreshTimer.Stop();
            watcherNeedsFullRefresh = false;
            if (watcherCancellation != null) watcherCancellation.Cancel();
            watchedWorkspacePath = null;
            if (workspaceWatcher != null)
            {
                workspaceWatcher.EnableRaisingEvents = false;
                workspaceWatcher.Dispose();
                workspaceWatcher = null;
            }
            if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            try
            {
                watchedWorkspacePath = Path.GetFullPath(path);
                workspaceWatcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite | NotifyFilters.Size
                };
                workspaceWatcher.Changed += WorkspaceWatcher_Changed;
                workspaceWatcher.Created += WorkspaceWatcher_Changed;
                workspaceWatcher.Deleted += WorkspaceWatcher_Changed;
                workspaceWatcher.Renamed += WorkspaceWatcher_Changed;
                workspaceWatcher.Error += WorkspaceWatcher_Error;
                workspaceWatcher.EnableRaisingEvents = true;
            }
            catch (ArgumentException) { watchedWorkspacePath = null; }
            catch (IOException) { watchedWorkspacePath = null; }
        }

        private void WorkspaceWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (runningGitOperation) return;
            bool isGitMetadata = IsGitMetadataPath(e.FullPath);
            bool refreshHistory = isGitMetadata && IsGitHistoryMetadataPath(e.FullPath);
            bool refreshChanges = !isGitMetadata || IsGitIndexPath(e.FullPath);
            if (!refreshHistory && !refreshChanges) return;
            Dispatcher.BeginInvoke(new Action(() => ScheduleWatcherRefresh(refreshHistory)), DispatcherPriority.Background);
        }

        private bool IsGitMetadataPath(string path)
        {
            string workspace = watchedWorkspacePath;
            if (String.IsNullOrWhiteSpace(path) || String.IsNullOrWhiteSpace(workspace)) return false;
            string gitPath = Path.Combine(workspace, ".git").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return String.Equals(normalizedPath, gitPath, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(gitPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(gitPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsGitHistoryMetadataPath(string path)
        {
            string workspace = watchedWorkspacePath;
            if (String.IsNullOrWhiteSpace(path) || String.IsNullOrWhiteSpace(workspace)) return false;
            string gitPath = Path.Combine(workspace, ".git");
            string relative = path.Substring(gitPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return String.Equals(relative, "HEAD", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, "packed-refs", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("refs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("refs" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("logs" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsGitIndexPath(string path)
        {
            string workspace = watchedWorkspacePath;
            if (String.IsNullOrWhiteSpace(path) || String.IsNullOrWhiteSpace(workspace)) return false;
            return String.Equals(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.Combine(workspace, ".git", "index"), StringComparison.OrdinalIgnoreCase);
        }

        private void WorkspaceWatcher_Error(object sender, ErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WatchWorkspace(WorkspacePath);
                ScheduleWatcherRefresh(true);
            }), DispatcherPriority.Background);
        }

        private void ScheduleWatcherRefresh(bool fullRefresh)
        {
            watcherNeedsFullRefresh |= fullRefresh;
            watcherRefreshTimer.Stop();
            watcherRefreshTimer.Start();
        }

        private void WatcherRefreshTimer_Tick(object sender, EventArgs e)
        {
            watcherRefreshTimer.Stop();
            bool fullRefresh = watcherNeedsFullRefresh;
            watcherNeedsFullRefresh = false;
            if (fullRefresh) RefreshAsync(); else RefreshChangesAsync();
        }

        private async void RefreshChangesAsync()
        {
            if (String.IsNullOrWhiteSpace(WorkspacePath)) return;
            if (watcherCancellation != null) watcherCancellation.Cancel();
            watcherCancellation = new CancellationTokenSource();
            CancellationToken token = watcherCancellation.Token;
            string workspace = WorkspacePath;
            try
            {
                GitRepositoryStatus updatedStatus = await git.GetStatusAsync(workspace, token);
                if (token.IsCancellationRequested || !String.Equals(workspace, WorkspacePath, StringComparison.OrdinalIgnoreCase)) return;
                status = updatedStatus;
                BuildGroups();
                StateText.Text = status.Changes.Count == 0 ? "No changes." : "";
                ErrorText.Text = "";
                UpdateCommitEnabled();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    StateText.Text = "Source Control unavailable.";
                    ErrorText.Text = ex.Message;
                }
            }
        }

        private void SourceControlPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            watcherRefreshTimer.Stop();
            if (watcherCancellation != null) watcherCancellation.Cancel();
            watchedWorkspacePath = null;
            if (workspaceWatcher == null) return;
            workspaceWatcher.EnableRaisingEvents = false;
            workspaceWatcher.Dispose();
            workspaceWatcher = null;
        }

        private void SourceControlPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (workspaceWatcher == null) WatchWorkspace(WorkspacePath);
        }

        public async void RefreshAsync()
        {
            if (watcherCancellation != null) watcherCancellation.Cancel();
            if (cancellation != null) cancellation.Cancel();
            cancellation = new CancellationTokenSource(); CancellationToken token = cancellation.Token;
            ChangeGroups.Clear(); CommitChangeGroups.Clear(); Commits.Clear(); ReflogEntries.Clear(); ErrorText.Text = ""; StateText.Text = string.IsNullOrWhiteSpace(WorkspacePath) ? "Open a folder to use Source Control." : "Loading repository…";
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
                await LoadLogAsync(selected, token); await LoadReflogAsync(token); UpdateCommitEnabled();
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
        private async Task LoadReflogAsync(CancellationToken token)
        {
            ReflogEntries.Clear(); if (status == null) return;
            foreach (GitReflogEntry entry in await git.GetReflogAsync(status.RepositoryRoot, 200, token)) ReflogEntries.Add(entry);
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
        private void HistoryMode_Checked(object sender, RoutedEventArgs e)
        {
            if (HistoryList == null || ReflogList == null) return;
            bool reflog = ReflogMode.IsChecked == true;
            HistoryList.Visibility = reflog ? Visibility.Collapsed : Visibility.Visible;
            ReflogList.Visibility = reflog ? Visibility.Visible : Visibility.Collapsed;
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
        private void FetchActions_Click(object sender, RoutedEventArgs e)
        {
            if (status == null) return;
            var menu = new ContextMenu();
            AddMenu(menu, "Fetch all remotes", "Fetch updates from all remotes and prune deleted remote references.", async () => await RunAndRefresh(() => git.FetchAsync(status.RepositoryRoot, cancellation.Token)));
            AddMenu(menu, "Checkout FETCH_HEAD", "Check out the most recently fetched commit in detached HEAD mode.", async () => await RunAndRefresh(() => git.CheckoutAsync(status.RepositoryRoot, "FETCH_HEAD", cancellation.Token)));
            AddMenu(menu, "Create branch from FETCH_HEAD…", "Create and check out a new local branch at the most recently fetched commit.", async () => await CreateBranchAsync("FETCH_HEAD"));
            menu.Items.Add(CreateResetMenu("FETCH_HEAD"));
            OpenButtonMenu(sender as FrameworkElement, menu);
        }

        private void BranchActions_Click(object sender, RoutedEventArgs e)
        {
            if (status == null) return;
            string selected = BranchPicker.SelectedItem as string;
            var menu = new ContextMenu();
            AddMenu(menu, "Checkout selected branch", "Switch the working tree to the selected branch. Local changes are never discarded automatically.", !String.IsNullOrWhiteSpace(selected), async () => await RunAndRefresh(() => git.CheckoutAsync(status.RepositoryRoot, selected, cancellation.Token)));
            AddMenu(menu, "Checkout remote branch…", "Explicitly fetch a remote branch and create a local tracking branch, even when the remote fetch refspec excludes it.", async () => await CheckoutRemoteBranchAsync());
            AddMenu(menu, "Create new branch…", "Create and check out a new branch from the current HEAD.", async () => await CreateBranchAsync("HEAD"));
            AddMenu(menu, "Create branch from selected…", "Create and check out a new branch starting at the selected branch or remote reference.", !String.IsNullOrWhiteSpace(selected), async () => await CreateBranchAsync(selected));
            AddMenu(menu, "Rename current branch…", "Rename the current local branch. Remote branch names are not changed.", !status.IsDetached, async () => await RenameCurrentBranchAsync());
            AddMenu(menu, "Delete selected local branch…", "Delete the selected local branch if it is fully merged. The current branch cannot be deleted.", !String.IsNullOrWhiteSpace(selected) && !String.Equals(selected, status.Branch, StringComparison.Ordinal), async () => await DeleteBranchAsync(selected));
            OpenButtonMenu(sender as FrameworkElement, menu);
        }
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
        private void History_RightClick(object sender, MouseButtonEventArgs e)
        {
            GitCommit commit = FindDataContext<GitCommit>(e.OriginalSource as DependencyObject);
            if (commit == null || status == null) return;
            HistoryList.SelectedItem = commit;
            ContextMenu menu = CreateRevisionMenu(commit.Hash, commit.ShortHash, true);
            HistoryList.ContextMenu = menu; menu.IsOpen = true; e.Handled = true;
        }

        private void Reflog_RightClick(object sender, MouseButtonEventArgs e)
        {
            GitReflogEntry entry = FindDataContext<GitReflogEntry>(e.OriginalSource as DependencyObject);
            if (entry == null || status == null) return;
            ReflogList.SelectedItem = entry;
            ContextMenu menu = CreateRevisionMenu(entry.Hash, entry.Selector, false);
            ReflogList.ContextMenu = menu; menu.IsOpen = true; e.Handled = true;
        }

        private ContextMenu CreateRevisionMenu(string revision, string label, bool allowRevert)
        {
            var menu = new ContextMenu();
            AddMenu(menu, "Checkout " + label, "Check out this revision in detached HEAD mode. Local changes are never discarded automatically.", async () => await RunAndRefresh(() => git.CheckoutAsync(status.RepositoryRoot, revision, cancellation.Token)));
            AddMenu(menu, "Create branch from here…", "Create and check out a new local branch starting at this revision.", async () => await CreateBranchAsync(revision));
            if (allowRevert)
                AddMenu(menu, "Revert commit…", "Create a new commit that reverses this commit while preserving existing history.", async () => await RevertCommitAsync(revision, label));
            menu.Items.Add(CreateResetMenu(revision));
            return menu;
        }

        private MenuItem CreateResetMenu(string revision)
        {
            var reset = new MenuItem { Header = "Reset current branch to here", ToolTip = "Move the current branch and HEAD to this revision." };
            AddMenu(reset, "Soft", "Move the branch to this revision and keep all changes staged.", async () => await ResetAsync(revision, GitResetMode.Soft));
            AddMenu(reset, "Mixed", "Move the branch to this revision, keep file changes, and unstage them.", async () => await ResetAsync(revision, GitResetMode.Mixed));
            AddMenu(reset, "Hard…", "Move the branch to this revision and permanently discard tracked file changes.", async () => await ResetAsync(revision, GitResetMode.Hard));
            return reset;
        }

        private async Task ResetAsync(string revision, GitResetMode mode)
        {
            if (status == null) return;
            string effect = mode == GitResetMode.Soft ? "keep all changes staged" :
                (mode == GitResetMode.Mixed ? "keep file changes but unstage them" : "permanently discard all tracked file changes");
            if (!StyledMessageBox.Show("RESET CURRENT BRANCH", "Reset " + status.Branch + " to " + revision + " and " + effect + "?")) return;
            await RunAndRefresh(() => git.ResetAsync(status.RepositoryRoot, revision, mode, cancellation.Token));
        }

        private async Task RevertCommitAsync(string revision, string label)
        {
            if (status == null || !StyledMessageBox.Show("REVERT COMMIT", "Create a new commit that reverses " + label + "? Existing history will be preserved.")) return;
            await RunAndRefresh(() => git.RevertAsync(status.RepositoryRoot, revision, cancellation.Token));
        }

        private async Task CreateBranchAsync(string startPoint)
        {
            if (status == null) return;
            string name = Microsoft.VisualBasic.Interaction.InputBox("New branch name:", "Create Branch", "").Trim();
            if (name.Length == 0) return;
            await RunAndRefresh(() => git.CreateBranchAsync(status.RepositoryRoot, name, startPoint, cancellation.Token));
        }

        private async Task CheckoutRemoteBranchAsync()
        {
            if (status == null) return;
            var dialog = new RemoteBranchDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            string value = dialog.RemoteBranch;

            int separator = value.IndexOf('/');
            if (separator <= 0 || separator == value.Length - 1)
            {
                ErrorText.Text = "Enter the remote and branch as remote/branch, for example origin/feature/name.";
                return;
            }

            string remote = value.Substring(0, separator);
            string branch = value.Substring(separator + 1);
            await RunAndRefresh(() => git.CheckoutRemoteBranchAsync(status.RepositoryRoot, remote, branch, branch, cancellation.Token));
        }

        private async Task RenameCurrentBranchAsync()
        {
            if (status == null || status.IsDetached) return;
            string name = Microsoft.VisualBasic.Interaction.InputBox("New name for " + status.Branch + ":", "Rename Branch", status.Branch).Trim();
            if (name.Length == 0 || String.Equals(name, status.Branch, StringComparison.Ordinal)) return;
            await RunAndRefresh(() => git.RenameCurrentBranchAsync(status.RepositoryRoot, name, cancellation.Token));
        }

        private async Task DeleteBranchAsync(string branch)
        {
            if (status == null || String.IsNullOrWhiteSpace(branch)) return;
            if (!StyledMessageBox.Show("DELETE BRANCH", "Delete local branch " + branch + "? Git will refuse if it is not fully merged.")) return;
            await RunAndRefresh(() => git.DeleteBranchAsync(status.RepositoryRoot, branch, cancellation.Token));
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
        private static void AddMenu(ContextMenu menu, string title, Action action) { AddMenu(menu, title, null, true, action); }
        private static void AddMenu(ItemsControl menu, string title, string toolTip, Action action) { AddMenu(menu, title, toolTip, true, action); }
        private static void AddMenu(ItemsControl menu, string title, string toolTip, bool enabled, Action action)
        {
            var item = new MenuItem { Header = title, ToolTip = toolTip, IsEnabled = enabled };
            ToolTipService.SetShowOnDisabled(item, true);
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }
        private static void OpenButtonMenu(FrameworkElement button, ContextMenu menu)
        {
            if (button == null) return;
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
        private static T FindDataContext<T>(DependencyObject current) where T : class
        {
            while (current != null)
            {
                FrameworkElement element = current as FrameworkElement;
                if (element != null && element.DataContext is T)
                    return (T)element.DataContext;

                FrameworkContentElement contentElement = current as FrameworkContentElement;
                if (contentElement != null)
                {
                    if (contentElement.DataContext is T)
                        return (T)contentElement.DataContext;
                    current = contentElement.Parent;
                    continue;
                }

                if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                    current = VisualTreeHelper.GetParent(current);
                else
                    current = LogicalTreeHelper.GetParent(current);
            }
            return null;
        }
        private async Task RunAndRefresh(Func<Task<GitResult>> operation)
        {
            runningGitOperation = true;
            watcherRefreshTimer.Stop();
            watcherNeedsFullRefresh = false;
            try
            {
                GitResult result = await operation();
                if (!result.Success) ErrorText.Text = String.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                else RefreshAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException)) { ErrorText.Text = ex.Message; }
            finally { runningGitOperation = false; }
        }
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
