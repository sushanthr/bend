using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TextCoreControl
{
    public enum DiffFindScope { Current, Base, Both }
    public sealed class FindQuery
    {
        public string Text { get; set; }
        public bool MatchCase { get; set; }
        public bool UseRegex { get; set; }
        public bool InSelection { get; set; }
        public DiffFindScope DiffScope { get; set; } = DiffFindScope.Current;
    }

    public sealed class FindNavigationResult
    {
        public int MatchNumber { get; set; }
        public int MatchCount { get; set; }
        public string Message { get; set; }
    }

    public interface IFindTarget
    {
        FindNavigationResult StartFind(FindQuery query);
        FindNavigationResult FindNext();
        FindNavigationResult FindPrevious();
        void ClearFind();
    }

    internal sealed class TextFindSession
    {
        internal struct MatchLocation { internal int Index; internal uint Length; }
        private readonly TextEditor editor;
        private readonly List<MatchLocation> matches = new List<MatchLocation>();
        private int current = -1;

        internal TextFindSession(TextEditor editor) { this.editor = editor; }

        internal FindNavigationResult Start(FindQuery query)
        {
            Clear();
            if (query == null || String.IsNullOrEmpty(query.Text)) return Result(0, 0, "");
            string text = editor.Document.Text.TrimEnd('\0');
            try
            {
                if (query.UseRegex)
                {
                    RegexOptions options = query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                    foreach (Match match in Regex.Matches(text, query.Text, options, TimeSpan.FromSeconds(2)))
                        Add(match.Index, (uint)match.Length, query.InSelection);
                }
                else
                {
                    int index = 0;
                    while ((index = text.IndexOf(query.Text, index, query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) >= 0)
                    { Add(index, (uint)query.Text.Length, query.InSelection); index += Math.Max(1, query.Text.Length); }
                }
            }
            catch (RegexMatchTimeoutException) { return Result(0, 0, "SEARCH TIMED OUT"); }
            catch (ArgumentException) { return Result(0, 0, "INVALID SEARCH"); }
            return Next();
        }

        private void Add(int index, uint length, bool inSelection)
        {
            if (!inSelection || editor.IsInBackgroundHighlight(editor.Document.GetOrdinalForTextIndex(index))) matches.Add(new MatchLocation { Index = index, Length = length });
        }

        internal FindNavigationResult Next()
        {
            if (matches.Count == 0) { editor.CancelSelect(); return Result(0, 0, "NO MATCHES FOUND"); }
            if (current + 1 >= matches.Count) { editor.CancelSelect(); return Result(matches.Count, matches.Count, "NO MORE MATCHES"); }
            current++; return Activate();
        }

        internal FindNavigationResult Previous()
        {
            if (matches.Count == 0) { editor.CancelSelect(); return Result(0, 0, "NO MATCHES FOUND"); }
            if (current <= 0) { current = -1; editor.CancelSelect(); return Result(0, matches.Count, "NO MORE MATCHES"); }
            current--; return Activate();
        }

        private FindNavigationResult Activate()
        {
            // Navigation must not steal focus from an incremental-search input.
            // Explicit editor activation remains the responsibility of the host.
            MatchLocation match = matches[current]; editor.Select(match.Index, match.Length);
            return Result(current + 1, matches.Count, "MATCH " + (current + 1) + " OF " + matches.Count);
        }

        internal void Clear() { matches.Clear(); current = -1; editor.CancelSelect(); }
        private static FindNavigationResult Result(int number, int count, string message) { return new FindNavigationResult { MatchNumber = number, MatchCount = count, Message = message }; }
    }

    internal sealed class ComparisonFindSession
    {
        private sealed class LocatedMatch { internal TextEditor Editor; internal int Row; internal int Index; internal uint Length; internal int Side; }
        private readonly TextEditor currentEditor;
        private readonly Func<TextEditor> baseEditor;
        private readonly List<LocatedMatch> matches = new List<LocatedMatch>();
        private int current = -1;

        internal ComparisonFindSession(TextEditor currentEditor, Func<TextEditor> baseEditor) { this.currentEditor = currentEditor; this.baseEditor = baseEditor; }
        internal FindNavigationResult Start(FindQuery query)
        {
            Clear(); if (query == null || String.IsNullOrEmpty(query.Text)) return Result("");
            try
            {
                if (query.DiffScope != DiffFindScope.Base) AddEditor(currentEditor, 1, query);
                TextEditor old = baseEditor(); if (old != null && query.DiffScope != DiffFindScope.Current) AddEditor(old, 0, query);
                matches.Sort((a, b) => { int row = a.Row.CompareTo(b.Row); return row != 0 ? row : a.Side.CompareTo(b.Side); });
            }
            catch (RegexMatchTimeoutException) { Clear(); return Result("SEARCH TIMED OUT"); }
            catch (ArgumentException) { Clear(); return Result("INVALID SEARCH"); }
            return Next();
        }
        private void AddEditor(TextEditor editor, int side, FindQuery query)
        {
            string text = editor.Document.Text.TrimEnd('\0'); int lineStart = 0, row = 0;
            while (lineStart <= text.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < text.Length && text[lineEnd] != '\r' && text[lineEnd] != '\n') lineEnd++;
                string line = text.Substring(lineStart, lineEnd - lineStart);
                if (query.UseRegex)
                {
                    RegexOptions options = query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                    foreach (Match match in Regex.Matches(line, query.Text, options, TimeSpan.FromSeconds(2))) matches.Add(new LocatedMatch { Editor = editor, Row = row, Side = side, Index = lineStart + match.Index, Length = (uint)match.Length });
                }
                else
                {
                    int index = 0; while ((index = line.IndexOf(query.Text, index, query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) >= 0)
                    { matches.Add(new LocatedMatch { Editor = editor, Row = row, Side = side, Index = lineStart + index, Length = (uint)query.Text.Length }); index += Math.Max(1, query.Text.Length); }
                }
                if (lineEnd >= text.Length) break;
                lineStart = lineEnd + 1;
                if (text[lineEnd] == '\r' && lineStart < text.Length && text[lineStart] == '\n') lineStart++;
                row++;
            }
        }
        internal FindNavigationResult Next() { if (matches.Count == 0) return Result("NO MATCHES FOUND"); if (current + 1 >= matches.Count) return Result("NO MORE MATCHES"); current++; return Activate(); }
        internal FindNavigationResult Previous() { if (matches.Count == 0) return Result("NO MATCHES FOUND"); if (current <= 0) { current = -1; return Result("NO MORE MATCHES"); } current--; return Activate(); }
        private FindNavigationResult Activate() { ClearSelections(); LocatedMatch match = matches[current]; match.Editor.Select(match.Index, match.Length); return Result("MATCH " + (current + 1) + " OF " + matches.Count); }
        internal int ActiveMatchIndex { get { return current >= 0 && current < matches.Count ? matches[current].Index : -1; } }
        internal void Clear() { matches.Clear(); current = -1; ClearSelections(); }
        private void ClearSelections() { currentEditor.CancelSelect(); TextEditor old = baseEditor(); if (old != null) old.CancelSelect(); }
        private FindNavigationResult Result(string message) { return new FindNavigationResult { MatchNumber = current + 1, MatchCount = matches.Count, Message = message }; }
    }
}
