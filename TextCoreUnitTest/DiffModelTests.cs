using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextCoreControl;

namespace TextCoreUnitTest
{
    [TestClass]
    public class DiffModelTests
    {
        [TestMethod]
        public void ParsePairsAdjacentReplacementLines()
        {
            DiffModel model = DiffModel.Parse("diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,2 +1,2 @@\n-old\n+new\n same");
            Assert.AreEqual(1, model.Files.Count);
            Assert.AreEqual("a.txt", model.Files[0].OldPath);
            Assert.AreEqual(DiffLineKind.Modified, model.Lines[1].Kind);
            Assert.AreEqual("old", model.Lines[1].OldText);
            Assert.AreEqual("new", model.Lines[1].NewText);
            Assert.AreEqual(2, model.Lines[2].OldLineNumber);
        }

        [TestMethod]
        public void ParsePairsMultiLineReplacementWithoutSkippingAddedLines()
        {
            DiffModel model = DiffModel.Parse("diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,3 @@\n-old one\n-old two\n-old three\n+new one\n+new two\n+new three");

            Assert.AreEqual(4, model.Lines.Count);
            Assert.AreEqual(4, model.Files[0].Hunks[0].Lines.Count);
            Assert.AreEqual("new one", model.Lines[1].NewText);
            Assert.AreEqual("new two", model.Lines[2].NewText);
            Assert.AreEqual("new three", model.Lines[3].NewText);
        }

        [TestMethod]
        public void ParseRepresentsBinaryChanges()
        {
            DiffModel model = DiffModel.Parse("diff --git a/p.png b/p.png\nBinary files a/p.png and b/p.png differ");
            Assert.IsTrue(model.Files[0].IsBinary);
            Assert.AreEqual("Binary files differ", model.Lines[0].NewText);
        }

        [TestMethod]
        public void ParseDecodesGitQuotedPaths()
        {
            DiffModel model = DiffModel.Parse(
                "diff --git \"a/folder/name\\t\\342\\230\\203.cs\" \"b/folder/name\\t\\342\\230\\203.cs\"\n" +
                "--- \"a/folder/name\\t\\342\\230\\203.cs\"\n" +
                "+++ \"b/folder/name\\t\\342\\230\\203.cs\"\n" +
                "@@ -1 +1 @@\n-old\n+new");

            Assert.AreEqual("folder/name\t☃.cs", model.Files[0].OldPath);
            Assert.AreEqual("folder/name\t☃.cs", model.Files[0].NewPath);
        }

        [TestMethod]
        public void ParseRecognizesPureRenamePathsWithoutContentHunk()
        {
            DiffModel model = DiffModel.Parse(
                "diff --git a/old.cs b/new.cs\n" +
                "similarity index 100%\n" +
                "rename from old.cs\n" +
                "rename to new.cs");

            Assert.AreEqual("old.cs", model.Files[0].OldPath);
            Assert.AreEqual("new.cs", model.Files[0].NewPath);
        }

        [TestMethod]
        public void ParseRemovesGitHeaderDelimiterAfterPathWithSpaces()
        {
            DiffModel model = DiffModel.Parse(
                "diff --git a/Rose Pine.xml b/Rose Pine.xml\n" +
                "--- /dev/null\n" +
                "+++ b/Rose Pine.xml\t\n" +
                "@@ -0,0 +1 @@\n+new");

            Assert.AreEqual("Rose Pine.xml", model.Files[0].NewPath);
        }
    }
}
