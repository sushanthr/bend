using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Bend.Controls
{
    public partial class FilesPanel : UserControl
    {
        public FilesPanel()
        {
            InitializeComponent();
            this.Tree.CommandProvider = new FileTreeCommandProvider(this);
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

        private sealed class FileTreeCommandProvider : IFolderTreeCommandProvider
        {
            private readonly FilesPanel panel;

            public FileTreeCommandProvider(FilesPanel panel) { this.panel = panel; }

            public List<FolderTreeCommand> GetCommands(string rootPath, string invocationPath, IList<string> selectedPaths)
            {
                List<string> paths = selectedPaths == null ? new List<string>() : selectedPaths.Where(path => File.Exists(path) || Directory.Exists(path)).ToList();
                bool isDirectory = !string.IsNullOrWhiteSpace(invocationPath) && Directory.Exists(invocationPath);
                List<FolderTreeCommand> commands = new List<FolderTreeCommand>();
                if (paths.Count > 0)
                {
                    commands.Add(new FolderTreeCommand { Label = "Cut", Gesture = "Ctrl+X", IsEnabled = true, Callback = () => panel.SetClipboard(paths, true) });
                    commands.Add(new FolderTreeCommand { Label = "Copy", Gesture = "Ctrl+C", IsEnabled = true, Callback = () => panel.SetClipboard(paths, false) });
                    commands.Add(new FolderTreeCommand { Label = "Rename", IsEnabled = paths.Count == 1, Callback = () => panel.Rename(paths[0]) });
                    commands.Add(new FolderTreeCommand { Label = "Delete", IsEnabled = true, Callback = () => panel.Delete(paths) });
                    commands.Add(FolderTreeCommand.Separator());
                    commands.Add(new FolderTreeCommand { Label = "Copy Full Path", IsEnabled = true, Callback = () => Clipboard.SetText(paths[0]) });
                    commands.Add(new FolderTreeCommand { Label = "Copy Relative Path", IsEnabled = !string.IsNullOrWhiteSpace(rootPath), Callback = () => Clipboard.SetText(panel.RelativePath(paths[0])) });
                }
                if (isDirectory)
                {
                    if (commands.Count > 0) commands.Add(FolderTreeCommand.Separator());
                    commands.Add(new FolderTreeCommand { Label = "Paste", Gesture = "Ctrl+V", IsEnabled = panel.CanPaste(), Callback = () => panel.Paste(invocationPath) });
                }
                return commands;
            }
        }

        private string RelativePath(string path)
        {
            return string.IsNullOrWhiteSpace(this.RootPath) ? path : new Uri(this.RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).MakeRelativeUri(new Uri(path)).ToString().Replace('/', Path.DirectorySeparatorChar);
        }

        private void SetClipboard(IList<string> paths, bool cut)
        {
            StringCollection files = new StringCollection();
            foreach (string path in paths) files.Add(path);
            System.Windows.DataObject data = new System.Windows.DataObject();
            data.SetFileDropList(files);
            data.SetData("Preferred DropEffect", new MemoryStream(new byte[] { (byte)(cut ? 2 : 5), 0, 0, 0 }));
            Clipboard.SetDataObject(data, true);
        }

        private bool CanPaste()
        {
            return Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Count > 0;
        }

        private void Paste(string destination)
        {
            StringCollection sources = Clipboard.GetFileDropList();
            bool move = IsMoveClipboardData();
            foreach (string source in sources)
            {
                string target = Path.Combine(destination, Path.GetFileName(source));
                if (File.Exists(target) || Directory.Exists(target)) continue;
                if (move) MoveEntry(source, target); else CopyEntry(source, target);
            }
            this.Tree.RefreshAsync();
        }

        private static bool IsMoveClipboardData()
        {
            Stream effect = Clipboard.GetData("Preferred DropEffect") as Stream;
            if (effect == null) return false;
            effect.Position = 0;
            return effect.ReadByte() == 2;
        }

        private static void MoveEntry(string source, string target)
        {
            if (Directory.Exists(source)) Directory.Move(source, target); else File.Move(source, target);
        }

        private static void CopyEntry(string source, string target)
        {
            if (Directory.Exists(source))
            {
                Directory.CreateDirectory(target);
                foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
                foreach (string directory in Directory.GetDirectories(source)) CopyEntry(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
            else File.Copy(source, target);
        }

        private void Rename(string path)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox("New name:", "Rename", Path.GetFileName(path));
            if (string.IsNullOrWhiteSpace(name) || name == Path.GetFileName(path)) return;
            string target = Path.Combine(Path.GetDirectoryName(path), name);
            if (File.Exists(path)) File.Move(path, target); else Directory.Move(path, target);
            this.Tree.RefreshAsync();
        }

        private void Delete(IList<string> paths)
        {
            if (MessageBox.Show("Delete the selected item(s)?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            foreach (string path in paths)
            {
                if (File.Exists(path)) File.Delete(path); else if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            this.Tree.RefreshAsync();
        }
    }
}
