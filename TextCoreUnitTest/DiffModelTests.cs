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
        public void ParseRepresentsBinaryChanges()
        {
            DiffModel model = DiffModel.Parse("diff --git a/p.png b/p.png\nBinary files a/p.png and b/p.png differ");
            Assert.IsTrue(model.Files[0].IsBinary);
            Assert.AreEqual("Binary files differ", model.Lines[0].NewText);
        }
    }
}
