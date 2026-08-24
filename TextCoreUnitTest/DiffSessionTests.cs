using System;
using System.Threading;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextCoreControl;

namespace TextCoreUnitTest
{
    [TestClass]
    public class DiffSessionTests
    {
        [TestMethod]
        public void CompareMarksAddedAndRemovedLines()
        {
            DiffAlignment result = DiffEngine.Compare("one\ntwo\nthree", "one\nchanged\nthree\nfour");
            Assert.AreEqual(DiffLineKind.Removed, result.BaseLineKinds[1]);
            Assert.AreEqual(DiffLineKind.Added, result.CurrentLineKinds[1]);
            Assert.AreEqual(DiffLineKind.Added, result.CurrentLineKinds[3]);
            Assert.AreEqual(1, result.RemovedLineCount);
            Assert.AreEqual(0, result.CurrentToBaseLine[0]);
            Assert.AreEqual(2, result.CurrentToBaseLine[2]);
            Assert.AreEqual(2, result.BaseToCurrentLine[2]);
        }

        [TestMethod]
        public void SideBySideBaseDisplayAddsNumberlessPaddingForInsertedLines()
        {
            DiffAlignment result = DiffEngine.Compare("one\nthree", "one\ntwo\nthree");
            Assert.AreEqual("one\n\nthree", result.BaseDisplayText);
            Assert.AreEqual(DiffLineKind.Padding, result.BaseDisplayLineKinds[1]);
            Assert.AreEqual(0, result.BaseDisplayLineNumbers[1]);
            Assert.AreEqual(2, result.BaseDisplayLineNumbers[2]);
            Assert.AreEqual(1, result.CurrentToBaseDisplayLine[1]);
            Assert.AreEqual(result.BaseDisplayLineKinds.Count, result.CurrentDisplayLineKinds.Count);
        }

        [TestMethod]
        public void SideBySideDisplaysHaveEqualRowsAndPadTheCurrentSideForDeletions()
        {
            DiffAlignment result = DiffEngine.Compare("one\ntwo\nthree", "one\nthree");
            Assert.AreEqual(result.BaseDisplayLineKinds.Count, result.CurrentDisplayLineKinds.Count);
            Assert.AreEqual(DiffLineKind.Removed, result.BaseDisplayLineKinds[1]);
            Assert.AreEqual(DiffLineKind.Padding, result.CurrentDisplayLineKinds[1]);
            Assert.AreEqual(0, result.CurrentDisplayLineNumbers[1]);
        }

        [TestMethod]
        public void CompareKeepsAccurateMappingsForNormalLargeSourceFiles()
        {
            var oldText = new StringBuilder();
            var newText = new StringBuilder();
            for (int line = 0; line < 2400; line++)
            {
                oldText.Append("line ").Append(line).Append('\n');
                newText.Append(line == 1200 ? "changed line" : "line " + line).Append('\n');
            }
            DiffAlignment result = DiffEngine.Compare(oldText.ToString(), newText.ToString());
            Assert.AreEqual(DiffLineKind.Context, result.CurrentLineKinds[2300]);
            Assert.AreEqual(2300, result.CurrentToBaseLine[2300]);
            Assert.AreEqual(DiffLineKind.Removed, result.BaseLineKinds[1200]);
            Assert.AreEqual(DiffLineKind.Added, result.CurrentLineKinds[1200]);
        }

        [TestMethod]
        public void TextEditorOwnsEditableCurrentAndImmutableBaseDocuments()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var editor = new TextEditor();
                    editor.LoadText("current", "sample.cs", "base");
                    Assert.AreEqual("current\0", editor.Document.Text);
                    Assert.AreEqual("base\0", editor.BaseDocument.Text);
                    Assert.IsTrue(editor.HasDiffBase);
                    editor.AllowEdit = true;
                    editor.ReplaceText(0, 7, "edited");
                    Assert.AreEqual("edited\0", editor.Document.Text);
                    Assert.AreEqual("base\0", editor.BaseDocument.Text);
                    editor.ShowDiff(DiffViewMode.SideBySide);
                    Assert.AreEqual("edited\0", editor.Document.Text, "Changing diff presentation must not replace the editable current document.");
                    editor.SetVerticalOffset(10); // Safe before the render surface has created device resources.
                    Assert.AreEqual(DiffViewMode.SideBySide, editor.DiffMode);
                    editor.ClearDiffBase();
                    Assert.AreEqual(DiffViewMode.None, editor.DiffMode);
                    Assert.IsFalse(editor.HasDiffBase);
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure != null) Assert.Fail(failure.ToString());
        }

        [TestMethod]
        public void EditingTrackedContentRefreshesInlineAndSideBySideDiffs()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var editor = new TextEditor();
                    editor.LoadText("one\ntwo\nthree", "sample.cs", "one\ntwo\nthree");
                    editor.AllowEdit = true;
                    editor.ReplaceText(4, 3, "changed");

                    editor.ShowDiff(DiffViewMode.Inline);
                    Assert.AreEqual("one\nchanged\nthree\0", editor.Document.Text);
                    Assert.AreEqual(DiffLineKind.Added, editor.CurrentDiffAlignment.CurrentLineKinds[1]);

                    editor.ShowDiff(DiffViewMode.SideBySide);
                    Assert.AreEqual("one\nchanged\nthree\0", editor.Document.Text,
                        "Changing presentation must preserve the editable current document.");
                    Assert.AreEqual(editor.CurrentDiffAlignment.BaseDisplayLineKinds.Count,
                        editor.CurrentDiffAlignment.CurrentDisplayLineKinds.Count,
                        "Side-by-side rendering must remain row aligned after an edit.");
                    Assert.AreEqual(DiffLineKind.Added, editor.CurrentDiffAlignment.CurrentLineKinds[1]);
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure != null) Assert.Fail(failure.ToString());
        }

        [TestMethod]
        public void LineLayoutAlwaysAdvancesForTransientInvalidWidths()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var document = new Document();
                    document.LoadText("Microsoft Reciprocal License", "license.md");
                    var builder = new TextLayoutBuilder { AutoWrapOverride = true };
                    foreach (float width in new[] { 0f, -1f, float.NaN })
                    {
                        int begin = document.FirstOrdinal();
                        int next;
                        builder.GetNextLine(document, begin, width, out next);
                        Assert.AreNotEqual(begin, next, "Line layout did not advance for width " + width);
                    }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure != null) Assert.Fail(failure.ToString());
        }

        [TestMethod]
        public void ScrollMetricsRejectTransientInvalidGeometry()
        {
            Assert.AreEqual(0, ScrollBoundsManager.NormalizeScrollMetric(-7.079386369352));
            Assert.AreEqual(0, ScrollBoundsManager.NormalizeScrollMetric(double.NaN));
            Assert.AreEqual(0, ScrollBoundsManager.NormalizeScrollMetric(double.PositiveInfinity));
            Assert.AreEqual(120, ScrollBoundsManager.NormalizeScrollMetric(120));
        }

        [TestMethod]
        public void DiffFindUsesOriginalCrLfOrdinals()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var editor = new TextEditor();
                    editor.LoadText("using One;\r\nusing Two;\r\nusing System.Text;", "sample.cs",
                        "using One;\r\nusing Two;\r\nusing System;\r\n");
                    editor.ShowDiff(DiffViewMode.SideBySide);
                    FindNavigationResult result = editor.StartFind(new FindQuery { Text = "System" });
                    Assert.AreEqual(1, result.MatchNumber);
                    Assert.AreEqual(30, editor.ActiveComparisonFindIndex,
                        "CRLF normalization must not shift a match to an earlier token.");
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure != null) Assert.Fail(failure.ToString());
        }

    }
}
