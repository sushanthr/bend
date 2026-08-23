using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bend.Controls
{
    /// <summary>
    ///     An immutable descriptor of a single file system entry, produced by
    ///     <see cref="IFileSystemTreeService.EnumerateChildrenAsync"/>.
    /// </summary>
    public struct FileSystemEntryDescriptor
    {
        public string Name;
        public string FullPath;
        public FolderTreeNodeKind Kind;
    }

    /// <summary>
    ///     Enumerates the children of a directory. Implementations must perform the
    ///     enumeration off the UI thread and must not follow reparse points (symbolic
    ///     links / junctions) when classifying entries.
    /// </summary>
    public interface IFileSystemTreeService
    {
        /// <summary>
        ///     Returns the children of <paramref name="directoryPath"/> as an immutable
        ///     list of descriptors, sorted with directories first (case-insensitive
        ///     ordinal by name), then files. Directories that cannot be read produce an
        ///     empty list; per-entry errors are skipped.
        /// </summary>
        Task<List<FileSystemEntryDescriptor>> EnumerateChildrenAsync(string directoryPath, CancellationToken cancellationToken);
    }

    /// <summary>
    ///     Default <see cref="IFileSystemTreeService"/> backed by <see cref="Directory.EnumerateFileSystemInfos"/>.
    /// </summary>
    public class FileSystemTreeService : IFileSystemTreeService
    {
        public Task<List<FileSystemEntryDescriptor>> EnumerateChildrenAsync(string directoryPath, CancellationToken cancellationToken)
        {
            return Task.Run(() => this.EnumerateChildren(directoryPath, cancellationToken), cancellationToken);
        }

        private List<FileSystemEntryDescriptor> EnumerateChildren(string directoryPath, CancellationToken cancellationToken)
        {
            List<FileSystemEntryDescriptor> entries = new List<FileSystemEntryDescriptor>();
            try
            {
                foreach (FileSystemInfo info in new DirectoryInfo(directoryPath).EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip the "." and ".." entries that can appear on some volumes.
                    if (info.Name == "." || info.Name == "..")
                    {
                        continue;
                    }

                    FolderTreeNodeKind kind = Classify(info);
                    entries.Add(new FileSystemEntryDescriptor
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        Kind = kind
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                // The directory cannot be read - report it as empty.
            }
            catch (IOException)
            {
                // The directory is not accessible right now - report it as empty.
            }
            catch (ArgumentException)
            {
                // The path is not a valid directory - report it as empty.
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private static FolderTreeNodeKind Classify(FileSystemInfo info)
        {
            try
            {
                if (info is DirectoryInfo)
                {
                    DirectoryInfo directoryInfo = (DirectoryInfo)info;
                    if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        // Do not follow reparse points; surface them as a distinct kind.
                        return FolderTreeNodeKind.ReparsePoint;
                    }
                    return FolderTreeNodeKind.Directory;
                }
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return FolderTreeNodeKind.SymbolicLink;
                }
                return FolderTreeNodeKind.File;
            }
            catch
            {
                // If attributes cannot be read, fall back to the type we already know.
                return info is DirectoryInfo ? FolderTreeNodeKind.Directory : FolderTreeNodeKind.File;
            }
        }

        private static int CompareEntries(FileSystemEntryDescriptor x, FileSystemEntryDescriptor y)
        {
            int xIsDirectory = x.Kind == FolderTreeNodeKind.Directory || x.Kind == FolderTreeNodeKind.ReparsePoint ? 0 : 1;
            int yIsDirectory = y.Kind == FolderTreeNodeKind.Directory || y.Kind == FolderTreeNodeKind.ReparsePoint ? 0 : 1;
            if (xIsDirectory != yIsDirectory)
            {
                return xIsDirectory.CompareTo(yIsDirectory);
            }
            return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}