using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Text;

namespace TextCoreControl
{
    public enum DiffViewMode { None, Inline, SideBySide }
    public enum DiffLineKind { Context, Added, Removed, Modified, Header, Padding }

    public sealed class DiffLine
    {
        public int? OldLineNumber { get; set; }
        public int? NewLineNumber { get; set; }
        public string OldText { get; set; }
        public string NewText { get; set; }
        public DiffLineKind Kind { get; set; }
        public string Marker { get { return Kind == DiffLineKind.Added ? "+" : Kind == DiffLineKind.Removed ? "-" : Kind == DiffLineKind.Modified ? "±" : Kind == DiffLineKind.Header ? "@" : " "; } }
        public string InlineText { get { return Kind == DiffLineKind.Removed ? OldText : Kind == DiffLineKind.Modified ? "- " + OldText + Environment.NewLine + "+ " + NewText : NewText; } }
        public string OldNumberText { get { return OldLineNumber.HasValue ? OldLineNumber.Value.ToString() : ""; } }
        public string NewNumberText { get { return NewLineNumber.HasValue ? NewLineNumber.Value.ToString() : ""; } }
    }

    public sealed class DiffHunk
    {
        public int OldStart { get; set; }
        public int NewStart { get; set; }
        public ObservableCollection<DiffLine> Lines { get; private set; } = new ObservableCollection<DiffLine>();
    }

    public sealed class DiffFile
    {
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public bool IsBinary { get; set; }
        public ObservableCollection<DiffHunk> Hunks { get; private set; } = new ObservableCollection<DiffHunk>();
    }

    public sealed class DiffModel
    {
        public string Title { get; set; }
        public string RawPatch { get; set; }
        public ObservableCollection<DiffFile> Files { get; private set; } = new ObservableCollection<DiffFile>();
        public ObservableCollection<DiffLine> Lines { get; private set; } = new ObservableCollection<DiffLine>();

        public static DiffModel Parse(string patch, string title = null)
        {
            var model = new DiffModel { Title = title ?? "Diff", RawPatch = patch ?? "" };
            DiffFile file = null; DiffHunk hunk = null; int oldLine = 0, newLine = 0;
            var hunkPattern = new Regex("^@@+ -(?<old>\\d+)(?:,\\d+)? \\+(?<new>\\d+)(?:,\\d+)? @@+");
            foreach (string raw in (patch ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.StartsWith("diff --git "))
                {
                    file = new DiffFile(); model.Files.Add(file); hunk = null; continue;
                }
                if (file == null) { file = new DiffFile(); model.Files.Add(file); }
                if (raw.StartsWith("--- ")) { file.OldPath = NormalizePath(raw.Substring(4)); continue; }
                if (raw.StartsWith("+++ ")) { file.NewPath = NormalizePath(raw.Substring(4)); continue; }
                if (raw.StartsWith("rename from ")) { file.OldPath = DecodeGitPath(raw.Substring(12)); continue; }
                if (raw.StartsWith("rename to ")) { file.NewPath = DecodeGitPath(raw.Substring(10)); continue; }
                if (raw.StartsWith("Binary files ") || raw.StartsWith("GIT binary patch"))
                {
                    file.IsBinary = true;
                    AddLine(model, hunk, new DiffLine { Kind = DiffLineKind.Header, NewText = "Binary files differ" }); continue;
                }
                Match match = hunkPattern.Match(raw);
                if (match.Success)
                {
                    oldLine = Int32.Parse(match.Groups["old"].Value); newLine = Int32.Parse(match.Groups["new"].Value);
                    hunk = new DiffHunk { OldStart = oldLine, NewStart = newLine }; file.Hunks.Add(hunk);
                    AddLine(model, hunk, new DiffLine { Kind = DiffLineKind.Header, NewText = raw }); continue;
                }
                if (hunk == null) continue;
                if (raw.StartsWith("\\ No newline")) { AddLine(model, hunk, new DiffLine { Kind = DiffLineKind.Header, NewText = raw }); continue; }
                if (raw.StartsWith("-")) { AddLine(model, hunk, new DiffLine { Kind = DiffLineKind.Removed, OldLineNumber = oldLine++, OldText = raw.Substring(1), NewText = "" }); continue; }
                if (raw.StartsWith("+")) { AddLine(model, hunk, new DiffLine { Kind = DiffLineKind.Added, NewLineNumber = newLine++, OldText = "", NewText = raw.Substring(1) }); continue; }
                string text = raw.StartsWith(" ") ? raw.Substring(1) : raw;
                AddLine(model, hunk, new DiffLine { Kind = DiffLineKind.Context, OldLineNumber = oldLine++, NewLineNumber = newLine++, OldText = text, NewText = text });
            }
            AlignReplacements(model);
            return model;
        }

        public string BuildOldText() { return BuildText(true); }
        public string BuildNewText() { return BuildText(false); }
        private string BuildText(bool oldSide)
        {
            var text = new StringBuilder();
            foreach (DiffLine line in Lines)
            {
                if (line.Kind == DiffLineKind.Header) continue;
                string value = oldSide ? line.OldText : line.NewText;
                if (value != null) text.Append(value).Append('\n');
            }
            return text.ToString();
        }

        private static void AddLine(DiffModel model, DiffHunk hunk, DiffLine line) { model.Lines.Add(line); if (hunk != null) hunk.Lines.Add(line); }
        private static string NormalizePath(string path)
        {
            // Git can append a tab delimiter to an unquoted ---/+++ path that
            // contains spaces. It is header syntax, not part of the filename.
            path = path.TrimEnd('\t');
            path = DecodeGitPath(path);
            return path == "/dev/null" ? path : (path.StartsWith("a/") || path.StartsWith("b/") ? path.Substring(2) : path);
        }

        private static string DecodeGitPath(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length < 2 || path[0] != '"' || path[path.Length - 1] != '"')
                return path;

            var decoded = new StringBuilder();
            int end = path.Length - 1;
            for (int index = 1; index < end; index++)
            {
                if (path[index] != '\\' || index + 1 >= end)
                {
                    decoded.Append(path[index]);
                    continue;
                }

                char escaped = path[++index];
                if (escaped >= '0' && escaped <= '7' && index + 2 < end &&
                    path[index + 1] >= '0' && path[index + 1] <= '7' &&
                    path[index + 2] >= '0' && path[index + 2] <= '7')
                {
                    var bytes = new List<byte>();
                    while (index + 2 < end &&
                        path[index] >= '0' && path[index] <= '7' &&
                        path[index + 1] >= '0' && path[index + 1] <= '7' &&
                        path[index + 2] >= '0' && path[index + 2] <= '7')
                    {
                        bytes.Add((byte)((path[index] - '0') * 64 + (path[index + 1] - '0') * 8 + path[index + 2] - '0'));
                        index += 3;
                        if (index + 3 >= end || path[index] != '\\') break;
                        index++;
                    }
                    index--;
                    decoded.Append(Encoding.UTF8.GetString(bytes.ToArray()));
                    continue;
                }

                switch (escaped)
                {
                    case 'a': decoded.Append('\a'); break;
                    case 'b': decoded.Append('\b'); break;
                    case 't': decoded.Append('\t'); break;
                    case 'n': decoded.Append('\n'); break;
                    case 'v': decoded.Append('\v'); break;
                    case 'f': decoded.Append('\f'); break;
                    case 'r': decoded.Append('\r'); break;
                    default: decoded.Append(escaped); break;
                }
            }
            return decoded.ToString();
        }

        private static void AlignReplacements(DiffModel model)
        {
            // Pair adjacent delete/add blocks for side-by-side presentation without losing inline semantics.
            AlignReplacements(model.Lines);
            foreach (DiffFile file in model.Files)
                foreach (DiffHunk hunk in file.Hunks)
                    for (int i = hunk.Lines.Count - 1; i >= 0; i--)
                        if (!model.Lines.Contains(hunk.Lines[i])) hunk.Lines.RemoveAt(i);
        }

        private static void AlignReplacements(ObservableCollection<DiffLine> lines)
        {
            int i = 0;
            while (i < lines.Count)
            {
                if (lines[i].Kind != DiffLineKind.Removed) { i++; continue; }
                int removedStart = i, removedCount = 0, addedStart;
                while (i < lines.Count && lines[i].Kind == DiffLineKind.Removed) { removedCount++; i++; }
                addedStart = i; int addedCount = 0;
                while (i < lines.Count && lines[i].Kind == DiffLineKind.Added) { addedCount++; i++; }
                int paired = Math.Min(removedCount, addedCount);
                for (int p = 0; p < paired; p++)
                {
                    DiffLine removed = lines[removedStart + p], added = lines[addedStart];
                    removed.NewLineNumber = added.NewLineNumber; removed.NewText = added.NewText; removed.Kind = DiffLineKind.Modified;
                    lines.RemoveAt(addedStart);
                }
                i = removedStart + removedCount + addedCount - paired;
            }
        }
    }
}
