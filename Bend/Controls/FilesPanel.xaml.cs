using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Bend.Controls
{
    public partial class FilesPanel : UserControl
    {
        public FilesPanel()
        {
            InitializeComponent();
            UpdateWorkspaceHeader();
        }

        public static readonly DependencyProperty RootPathProperty = DependencyProperty.Register(
            "RootPath", typeof(string), typeof(FilesPanel), new PropertyMetadata(null, RootPathChanged));

        public string RootPath
        {
            get { return (string)GetValue(RootPathProperty); }
            set { SetValue(RootPathProperty, value); }
        }

        public static readonly RoutedEvent OpenFolderRequestedEvent = EventManager.RegisterRoutedEvent(
            "OpenFolderRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilesPanel));
        public event RoutedEventHandler OpenFolderRequested
        {
            add { AddHandler(OpenFolderRequestedEvent, value); }
            remove { RemoveHandler(OpenFolderRequestedEvent, value); }
        }

        public static readonly RoutedEvent FileInvokedEvent = EventManager.RegisterRoutedEvent(
            "FileInvoked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilesPanel));
        public event RoutedEventHandler FileInvoked
        {
            add { AddHandler(FileInvokedEvent, value); }
            remove { RemoveHandler(FileInvokedEvent, value); }
        }

        private static void RootPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            FilesPanel panel = (FilesPanel)sender;
            panel.Tree.RootPath = e.NewValue as string;
            panel.UpdateWorkspaceHeader();
        }

        private void UpdateWorkspaceHeader()
        {
            string path = this.RootPath;
            bool hasRoot = !string.IsNullOrWhiteSpace(path);
            this.RootButton.Content = hasRoot ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToUpperInvariant() : "NO FOLDER OPEN";
            this.RootButton.ToolTip = hasRoot ? path : "No workspace folder is open";
            this.EmptyState.Visibility = hasRoot ? Visibility.Collapsed : Visibility.Visible;
            this.Tree.Visibility = hasRoot ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e) { RaiseEvent(new RoutedEventArgs(OpenFolderRequestedEvent)); }
        private void Root_Click(object sender, RoutedEventArgs e) { }
        private void NewFile_Click(object sender, RoutedEventArgs e) { }
        private void NewFolder_Click(object sender, RoutedEventArgs e) { }
        private void Refresh_Click(object sender, RoutedEventArgs e) { this.Tree.RefreshAsync(); }
        private void Collapse_Click(object sender, RoutedEventArgs e) { this.Tree.CollapseAll(); }

        private void Tree_FileInvoked(object sender, RoutedEventArgs e)
        {
            FolderTreeFileInvokedEventArgs fileEvent = e as FolderTreeFileInvokedEventArgs;
            if (fileEvent == null) return;
            RaiseEvent(new FolderTreeFileInvokedEventArgs(FileInvokedEvent, fileEvent.OriginalSource, fileEvent.IsDoubleClick));
        }
    }
}
