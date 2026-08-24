using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Bend.Controls
{
    public class FolderTreeFileInvokedEventArgs : RoutedEventArgs
    {
        public FolderTreeFileInvokedEventArgs(RoutedEvent routedEvent, object source, bool isDoubleClick)
            : base(routedEvent, source)
        {
            this.IsDoubleClick = isDoubleClick;
        }

        public bool IsDoubleClick { get; private set; }
    }

    public partial class FolderTree : UserControl
    {
        private readonly IFileSystemTreeService fileSystemService;
        private CancellationTokenSource loadCancellation;
        private readonly ObservableCollection<FolderTreeNode> rootNodes = new ObservableCollection<FolderTreeNode>();
        private string selectedPath;
        private bool doubleClickHandled;
        private FolderTreeNode pressedNode;

        public IFolderTreeCommandProvider CommandProvider { get; set; }

        public FolderTree() : this(new FileSystemTreeService()) { }

        internal FolderTree(IFileSystemTreeService service)
        {
            this.fileSystemService = service;
            InitializeComponent();
            this.DataContext = this;
        }

        public ObservableCollection<FolderTreeNode> RootNodes { get { return this.rootNodes; } }

        public static readonly DependencyProperty RootPathProperty = DependencyProperty.Register(
            "RootPath", typeof(string), typeof(FolderTree), new PropertyMetadata(null, RootPathChanged));

        public string RootPath
        {
            get { return (string)GetValue(RootPathProperty); }
            set { SetValue(RootPathProperty, value); }
        }

        public static readonly DependencyProperty ShowRootProperty = DependencyProperty.Register(
            "ShowRoot", typeof(bool), typeof(FolderTree), new PropertyMetadata(false));

        public bool ShowRoot
        {
            get { return (bool)GetValue(ShowRootProperty); }
            set { SetValue(ShowRootProperty, value); }
        }

        private static readonly DependencyPropertyKey SelectedPathPropertyKey = DependencyProperty.RegisterReadOnly(
            "SelectedPath", typeof(string), typeof(FolderTree), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedPathProperty = SelectedPathPropertyKey.DependencyProperty;

        public string SelectedPath { get { return this.selectedPath; } }

        public static readonly RoutedEvent FileInvokedEvent = EventManager.RegisterRoutedEvent(
            "FileInvoked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FolderTree));

        public event RoutedEventHandler FileInvoked
        {
            add { AddHandler(FileInvokedEvent, value); }
            remove { RemoveHandler(FileInvokedEvent, value); }
        }

        private static void RootPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((FolderTree)sender).RefreshAsync();
        }

        public async void RefreshAsync()
        {
            if (this.loadCancellation != null) this.loadCancellation.Cancel();
            this.loadCancellation = new CancellationTokenSource();
            CancellationToken token = this.loadCancellation.Token;
            string path = NormalizePath(this.RootPath);
            this.rootNodes.Clear();
            this.selectedPath = null;
            SetValue(SelectedPathPropertyKey, null);
            if (path == null || !Directory.Exists(path)) return;

            FolderTreeNode root = new FolderTreeNode(null, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), path, FolderTreeNodeKind.Directory, false);
            if (this.ShowRoot) this.rootNodes.Add(root);
            try
            {
                await LoadChildrenAsync(root, token);
                if (!this.ShowRoot)
                {
                    foreach (FolderTreeNode child in root.Children.ToList()) this.rootNodes.Add(child);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void CollapseAll()
        {
            foreach (FolderTreeNode node in this.rootNodes)
                CollapseNode(node);
        }

        private static void CollapseNode(FolderTreeNode node)
        {
            node.IsExpanded = false;
            foreach (FolderTreeNode child in node.Children)
                CollapseNode(child);
        }

        private async Task LoadChildrenAsync(FolderTreeNode node, CancellationToken token)
        {
            node.IsLoading = true;
            node.Children.Clear();
            node.Children.Add(FolderTreeNode.CreatePlaceholder(node, "Loading..."));
            try
            {
                var entries = await this.fileSystemService.EnumerateChildrenAsync(node.FullPath, token);
                node.Children.Clear();
                foreach (FileSystemEntryDescriptor entry in entries)
                {
                    FolderTreeNode child = new FolderTreeNode(node, entry.Name, entry.FullPath, entry.Kind, false);
                    if (child.CanExpand)
                        child.Children.Add(FolderTreeNode.CreatePlaceholder(child, "Loading..."));
                    node.Children.Add(child);
                }
                if (node.Children.Count == 0) node.Children.Add(FolderTreeNode.CreatePlaceholder(node, "(empty)"));
                node.IsLoaded = true;
                node.HasLoadError = false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is System.Security.SecurityException)
            {
                node.Children.Clear();
                node.Children.Add(FolderTreeNode.CreatePlaceholder(node, "Unable to read this folder"));
                node.HasLoadError = true;
                node.LoadErrorMessage = exception.Message;
            }
            finally { node.IsLoading = false; }
        }

        private async void TreeItemExpanded(object sender, RoutedEventArgs e)
        {
            FolderTreeNode node = ((TreeViewItem)e.OriginalSource).DataContext as FolderTreeNode;
            if (node == null || node.IsPlaceholder || node.IsLoading || node.IsLoaded || !node.CanExpand) return;
            try { await LoadChildrenAsync(node, this.loadCancellation == null ? CancellationToken.None : this.loadCancellation.Token); }
            catch (OperationCanceledException) { }
        }

        private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            FolderTreeNode node = e.NewValue as FolderTreeNode;
            if (node == null || node.IsPlaceholder) return;
            this.selectedPath = node.FullPath;
            SetValue(SelectedPathPropertyKey, node.FullPath);
        }

        private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            if (FindAncestor<ToggleButton>(source) != null)
            {
                this.pressedNode = null;
                return;
            }
            TreeViewItem item = FindAncestor<TreeViewItem>(source);
            FolderTreeNode node = item == null ? null : item.DataContext as FolderTreeNode;
            this.pressedNode = node != null && !node.IsPlaceholder ? node : null;
            if (node != null && !node.IsPlaceholder && node.NodeKind == FolderTreeNodeKind.File && e.ClickCount > 1)
            {
                RaiseEvent(new FolderTreeFileInvokedEventArgs(FileInvokedEvent, node, true));
                this.doubleClickHandled = true;
                e.Handled = true;
            }
        }

        private void Tree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.doubleClickHandled)
            {
                this.doubleClickHandled = false;
                this.pressedNode = null;
                e.Handled = true;
                return;
            }
            DependencyObject source = e.OriginalSource as DependencyObject;
            if (FindAncestor<ToggleButton>(source) != null)
            {
                this.pressedNode = null;
                return;
            }
            TreeViewItem item = FindAncestor<TreeViewItem>(source);
            FolderTreeNode node = item == null ? null : item.DataContext as FolderTreeNode;
            bool isMatchingClick = node != null && node == this.pressedNode && item.IsMouseOver;
            this.pressedNode = null;
            if (!isMatchingClick || node.IsPlaceholder) return;
            if (node.IsDirectory)
            {
                node.IsExpanded = !node.IsExpanded;
                item.IsExpanded = node.IsExpanded;
                e.Handled = true;
            }
            else if (node.NodeKind == FolderTreeNodeKind.File)
            {
                RaiseEvent(new FolderTreeFileInvokedEventArgs(FileInvokedEvent, node, e.ClickCount > 1));
                e.Handled = true;
            }
        }

        private void Tree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (this.CommandProvider == null) return;
            DependencyObject source = e.OriginalSource as DependencyObject;
            TreeViewItem item = FindAncestor<TreeViewItem>(source);
            FolderTreeNode node = item == null ? null : item.DataContext as FolderTreeNode;
            if (node != null && !node.IsPlaceholder)
            {
                node.IsSelected = true;
                this.selectedPath = node.FullPath;
                SetValue(SelectedPathPropertyKey, node.FullPath);
            }
            string invocationPath = node != null && !node.IsPlaceholder ? node.FullPath : this.RootPath;
            List<string> selectedPaths = node == null ? new List<string>() : new List<string> { node.FullPath };
            List<FolderTreeCommand> commands = this.CommandProvider.GetCommands(this.RootPath, invocationPath, selectedPaths);
            if (commands == null || commands.Count == 0) return;
            ContextMenu menu = new ContextMenu { MinWidth = 210 };
            foreach (FolderTreeCommand command in commands)
            {
                if (command.IsSeparator)
                {
                    menu.Items.Add(new Separator());
                    continue;
                }
                MenuItem itemMenu = new MenuItem { Header = command.Label, IsEnabled = command.IsEnabled, InputGestureText = command.Gesture };
                itemMenu.Click += (menuSender, menuArgs) => command.Callback();
                menu.Items.Add(itemMenu);
            }
            this.Tree.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                T match = current as T;
                if (match != null) return match;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return Path.GetFullPath(path); }
            catch (ArgumentException) { return null; }
        }
    }
}
