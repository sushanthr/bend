using System;
using System.Collections.Generic;

namespace Bend.Controls
{
    /// <summary>
    ///     A single command in a <see cref="FolderTree"/> context menu.
    /// </summary>
    public class FolderTreeCommand
    {
        public string Label;
        public string IconKey;
        public string Gesture;
        public bool IsSeparator;
        public bool IsEnabled;
        public Action Callback;

        public static FolderTreeCommand Separator()
        {
            return new FolderTreeCommand
            {
                IsSeparator = true,
                IsEnabled = false
            };
        }
    }

    /// <summary>
    ///     Supplies the context menu commands for a <see cref="FolderTree"/>. The host
    ///     (for example the Files activity pane) implements this interface so the tree
    ///     control itself stays free of Bend-specific commands.
    /// </summary>
    public interface IFolderTreeCommandProvider
    {
        /// <summary>
        ///     Returns the ordered command descriptors for the context menu shown at
        ///     <paramref name="invocationPath"/>. <paramref name="invocationPath"/> is
        ///     the full path of the node the menu was invoked on, or null when the menu
        ///     was invoked on empty space (the root). <paramref name="selectedPaths"/>
        ///     contains the currently selected node paths (empty when none).
        /// </summary>
        List<FolderTreeCommand> GetCommands(string rootPath, string invocationPath, IList<string> selectedPaths);
    }
}