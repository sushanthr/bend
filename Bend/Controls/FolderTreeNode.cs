using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Bend.Controls
{
    /// <summary>
    ///     The kind of file system item a tree node represents.
    /// </summary>
    public enum FolderTreeNodeKind
    {
        File,
        Directory,
        SymbolicLink,
        ReparsePoint
    }

    /// <summary>
    ///     A single node in the <see cref="FolderTree"/> control. Holds only file system
    ///     metadata (name, path, kind) and tree state (expansion, selection, load status).
    ///     It never holds file contents, <see cref="FileInfo"/> objects, or editor references.
    /// </summary>
    public class FolderTreeNode : INotifyPropertyChanged
    {
        #region Member data

        private readonly FolderTreeNode parent;
        private readonly ObservableCollection<FolderTreeNode> children;
        private bool isExpanded;
        private bool isSelected;
        private bool isLoading;
        private bool isLoaded;
        private bool hasLoadError;
        private string loadErrorMessage;
        private readonly bool isPlaceholder;
        private bool isEditing;
        private string editName;
        private bool isCut;

        #endregion

        #region Properties

        public string Name
        {
            get;
        }

        public string FullPath
        {
            get;
        }

        public FolderTreeNodeKind NodeKind
        {
            get;
        }

        public string Extension
        {
            get;
        }

        public ObservableCollection<FolderTreeNode> Children
        {
            get
            {
                return this.children;
            }
        }

        public FolderTreeNode Parent
        {
            get
            {
                return this.parent;
            }
        }

        public bool IsDirectory
        {
            get
            {
                return this.NodeKind == FolderTreeNodeKind.Directory
                    || this.NodeKind == FolderTreeNodeKind.SymbolicLink
                    || this.NodeKind == FolderTreeNodeKind.ReparsePoint;
            }
        }

        public bool CanExpand
        {
            get
            {
                return this.IsDirectory;
            }
        }

        public bool IsExpanded
        {
            get
            {
                return this.isExpanded;
            }
            set
            {
                if (this.isExpanded != value)
                {
                    this.isExpanded = value;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged("IconKey");
                }
            }
        }

        public bool IsSelected
        {
            get
            {
                return this.isSelected;
            }
            set
            {
                if (this.isSelected != value)
                {
                    this.isSelected = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get
            {
                return this.isLoading;
            }
            set
            {
                if (this.isLoading != value)
                {
                    this.isLoading = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public bool IsLoaded
        {
            get
            {
                return this.isLoaded;
            }
            set
            {
                if (this.isLoaded != value)
                {
                    this.isLoaded = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public bool HasLoadError
        {
            get
            {
                return this.hasLoadError;
            }
            set
            {
                if (this.hasLoadError != value)
                {
                    this.hasLoadError = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public string LoadErrorMessage
        {
            get
            {
                return this.loadErrorMessage;
            }
            set
            {
                if (this.loadErrorMessage != value)
                {
                    this.loadErrorMessage = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     A short key identifying which glyph to draw for this node.
        ///     "folder", "folderOpen", "file", "link".
        /// </summary>
        public string IconKey
        {
            get
            {
                if (this.NodeKind == FolderTreeNodeKind.SymbolicLink || this.NodeKind == FolderTreeNodeKind.ReparsePoint)
                {
                    return "link";
                }
                if (this.NodeKind == FolderTreeNodeKind.Directory)
                {
                    return this.IsExpanded ? "folderOpen" : "folder";
                }
                return "file";
            }
        }

        /// <summary>
        ///     The Segoe MDL2 Assets glyph to draw for this node.
        /// </summary>
        public string IconGlyph
        {
            get
            {
                if (this.isPlaceholder)
                {
                    return string.Empty;
                }
                switch (this.IconKey)
                {
                    case "folder":
                        return "\uE8B7";
                    case "folderOpen":
                        return "\uE838";
                    case "link":
                        return "\uE71B";
                    default:
                        return "\uE8A5";
                }
            }
        }

        public string ExpansionGlyph
        {
            get
            {
                if (!this.CanExpand || this.isPlaceholder) return string.Empty;
                return this.IsExpanded ? "\uE70D" : "\uE76C";
            }
        }

        /// <summary>
        ///     True for synthetic rows such as "Loading…" or "(empty)" that do not
        ///     represent a real file system entry.
        /// </summary>
        public bool IsPlaceholder
        {
            get
            {
                return this.isPlaceholder;
            }
        }

        /// <summary>
        ///     True while the row label is being edited inline (new entry or rename).
        /// </summary>
        public bool IsEditing
        {
            get
            {
                return this.isEditing;
            }
            set
            {
                if (this.isEditing != value)
                {
                    this.isEditing = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     The text shown in the inline editor while <see cref="IsEditing"/> is true.
        /// </summary>
        public string EditName
        {
            get
            {
                return this.editName;
            }
            set
            {
                if (this.editName != value)
                {
                    this.editName = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        ///     True when the node has been cut (Ctrl+X) and is awaiting a paste.
        /// </summary>
        public bool IsCut
        {
            get
            {
                return this.isCut;
            }
            set
            {
                if (this.isCut != value)
                {
                    this.isCut = value;
                    this.OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructor

        internal FolderTreeNode(FolderTreeNode parent, string name, string fullPath, FolderTreeNodeKind nodeKind, bool isPlaceholder)
        {
            this.parent = parent;
            this.Name = name;
            this.FullPath = fullPath;
            this.NodeKind = nodeKind;
            this.Extension = nodeKind == FolderTreeNodeKind.File ? Path.GetExtension(name) : string.Empty;
            this.children = new ObservableCollection<FolderTreeNode>();
            this.isPlaceholder = isPlaceholder;
        }

        /// <summary>
        ///     Creates a synthetic row (for example "Loading…" or "(empty)") that is
        ///     shown inside a directory while its children are being enumerated.
        /// </summary>
        internal static FolderTreeNode CreatePlaceholder(FolderTreeNode parent, string text)
        {
            return new FolderTreeNode(parent, text, string.Empty, FolderTreeNodeKind.File, /*isPlaceholder*/true);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}