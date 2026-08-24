using System;
using System.Collections.Generic;

namespace TextCoreControl
{
    public sealed class DiffAlignment
    {
        public IList<DiffLineKind> CurrentLineKinds { get; internal set; }
        public IList<DiffLineKind> BaseLineKinds { get; internal set; }
        public int RemovedLineCount { get; internal set; }
        public IList<int> CurrentToBaseLine { get; internal set; }
        public IList<int> BaseToCurrentLine { get; internal set; }
        public string BaseDisplayText { get; internal set; }
        public IList<DiffLineKind> BaseDisplayLineKinds { get; internal set; }
        public IList<int> BaseDisplayLineNumbers { get; internal set; }
        public IList<int> CurrentToBaseDisplayLine { get; internal set; }
        public IList<int> BaseDisplayToCurrentLine { get; internal set; }
        public string CurrentDisplayText { get; internal set; }
        public IList<DiffLineKind> CurrentDisplayLineKinds { get; internal set; }
        public IList<int> CurrentDisplayLineNumbers { get; internal set; }
    }

    public static class DiffEngine
    {
        public static DiffAlignment Compare(string baseText, string currentText)
        {
            string[] oldLines = Lines(baseText), newLines = Lines(currentText);
            List<DiffLineKind> oldKinds = NewKinds(oldLines.Length);
            List<DiffLineKind> newKinds = NewKinds(newLines.Length);
            int[] currentToBase = new int[newLines.Length];
            int[] baseToCurrent = new int[oldLines.Length];
            long cells = (long)(oldLines.Length + 1) * (newLines.Length + 1);
            // A 2M-cell cutoff classified ordinary 2,000-line source files as entirely
            // replaced. Besides bad highlighting, that left every scroll map entry at
            // zero. Keep exact alignment for normal source files; 16M cells is about
            // 64 MB and still bounded for this desktop control.
            if (cells > 16000000)
            {
                return CompareLarge(oldLines, newLines, oldKinds, newKinds, currentToBase, baseToCurrent);
            }
            int[,] lcs = new int[oldLines.Length + 1, newLines.Length + 1];
            for (int i = oldLines.Length - 1; i >= 0; i--)
                for (int j = newLines.Length - 1; j >= 0; j--)
                    lcs[i, j] = String.Equals(oldLines[i], newLines[j], StringComparison.Ordinal) ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            int oldIndex = 0, newIndex = 0, removed = 0;
            while (oldIndex < oldLines.Length || newIndex < newLines.Length)
            {
                if (oldIndex < oldLines.Length && newIndex < newLines.Length && String.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal)) { currentToBase[newIndex] = oldIndex; baseToCurrent[oldIndex] = newIndex; oldIndex++; newIndex++; }
                else if (newIndex < newLines.Length && (oldIndex == oldLines.Length || lcs[oldIndex, newIndex + 1] >= lcs[oldIndex + 1, newIndex])) { currentToBase[newIndex] = Math.Min(oldIndex, oldLines.Length); newKinds[newIndex++] = DiffLineKind.Added; }
                else { baseToCurrent[oldIndex] = Math.Min(newIndex, Math.Max(0, newLines.Length - 1)); oldKinds[oldIndex++] = DiffLineKind.Removed; removed++; }
            }
            return AddDisplays(oldLines, newLines, newKinds, oldKinds, new DiffAlignment { BaseLineKinds = oldKinds, CurrentLineKinds = newKinds, RemovedLineCount = removed, CurrentToBaseLine = currentToBase, BaseToCurrentLine = baseToCurrent });
        }

        private static DiffAlignment CompareLarge(string[] oldLines, string[] newLines, List<DiffLineKind> oldKinds, List<DiffLineKind> newKinds, int[] currentToBase, int[] baseToCurrent)
        {
            int prefix = 0;
            while (prefix < oldLines.Length && prefix < newLines.Length && String.Equals(oldLines[prefix], newLines[prefix], StringComparison.Ordinal))
            { currentToBase[prefix] = prefix; baseToCurrent[prefix] = prefix; prefix++; }

            int oldEnd = oldLines.Length - 1, newEnd = newLines.Length - 1;
            while (oldEnd >= prefix && newEnd >= prefix && String.Equals(oldLines[oldEnd], newLines[newEnd], StringComparison.Ordinal))
            { currentToBase[newEnd] = oldEnd; baseToCurrent[oldEnd] = newEnd; oldEnd--; newEnd--; }

            int removed = 0;
            for (int oldIndex = prefix; oldIndex <= oldEnd; oldIndex++)
            {
                oldKinds[oldIndex] = DiffLineKind.Removed; removed++;
                baseToCurrent[oldIndex] = Math.Min(Math.Max(prefix, newEnd + 1), Math.Max(0, newLines.Length - 1));
            }
            for (int newIndex = prefix; newIndex <= newEnd; newIndex++)
            {
                newKinds[newIndex] = DiffLineKind.Added;
                currentToBase[newIndex] = Math.Min(Math.Max(prefix, oldEnd + 1), Math.Max(0, oldLines.Length - 1));
            }
            return AddDisplays(oldLines, newLines, newKinds, oldKinds, new DiffAlignment { BaseLineKinds = oldKinds, CurrentLineKinds = newKinds, RemovedLineCount = removed, CurrentToBaseLine = currentToBase, BaseToCurrentLine = baseToCurrent });
        }

        private static DiffAlignment AddDisplays(string[] oldLines, string[] newLines, IList<DiffLineKind> newKinds, IList<DiffLineKind> oldKinds, DiffAlignment alignment)
        {
            var baseRows = new List<string>(); var currentRows = new List<string>();
            var baseKinds = new List<DiffLineKind>(); var currentKinds = new List<DiffLineKind>();
            var baseNumbers = new List<int>(); var currentNumbers = new List<int>();
            int[] currentToRow = new int[newLines.Length];
            int oldStart = 0, newStart = 0;
            Action<int, int> emitBlock = (oldEnd, newEnd) =>
            {
                int count = Math.Max(oldEnd - oldStart, newEnd - newStart);
                for (int offset = 0; offset < count; offset++)
                {
                    int old = oldStart + offset, current = newStart + offset, row = baseRows.Count;
                    bool hasOld = old < oldEnd, hasCurrent = current < newEnd;
                    baseRows.Add(hasOld ? oldLines[old] : ""); baseKinds.Add(hasOld ? oldKinds[old] : DiffLineKind.Padding); baseNumbers.Add(hasOld ? old + 1 : 0);
                    currentRows.Add(hasCurrent ? newLines[current] : ""); currentKinds.Add(hasCurrent ? newKinds[current] : DiffLineKind.Padding); currentNumbers.Add(hasCurrent ? current + 1 : 0);
                    if (hasCurrent) currentToRow[current] = row;
                }
                oldStart = oldEnd; newStart = newEnd;
            };
            for (int current = 0; current < newLines.Length; current++)
            {
                if (newKinds[current] != DiffLineKind.Context) continue;
                int old = alignment.CurrentToBaseLine[current];
                if (old < oldStart || old >= oldLines.Length || oldKinds[old] != DiffLineKind.Context) continue;
                emitBlock(old, current);
                int row = baseRows.Count;
                baseRows.Add(oldLines[old]); baseKinds.Add(DiffLineKind.Context); baseNumbers.Add(old + 1);
                currentRows.Add(newLines[current]); currentKinds.Add(DiffLineKind.Context); currentNumbers.Add(current + 1); currentToRow[current] = row;
                oldStart = old + 1; newStart = current + 1;
            }
            emitBlock(oldLines.Length, newLines.Length);
            alignment.BaseDisplayText = String.Join("\n", baseRows); alignment.BaseDisplayLineKinds = baseKinds; alignment.BaseDisplayLineNumbers = baseNumbers;
            alignment.CurrentDisplayText = String.Join("\n", currentRows); alignment.CurrentDisplayLineKinds = currentKinds; alignment.CurrentDisplayLineNumbers = currentNumbers;
            alignment.CurrentToBaseDisplayLine = currentToRow; alignment.BaseDisplayToCurrentLine = new int[baseRows.Count];
            for (int row = 0; row < baseRows.Count; row++) alignment.BaseDisplayToCurrentLine[row] = row;
            return alignment;
        }

        private static List<DiffLineKind> NewKinds(int count) { var values = new List<DiffLineKind>(count); for (int i = 0; i < count; i++) values.Add(DiffLineKind.Context); return values; }
        private static string[] Lines(string text) { return (text ?? String.Empty).Replace("\r\n", "\n").TrimEnd('\0').Split('\n'); }
    }
}
