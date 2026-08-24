using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextCoreControl;

namespace TextCoreUnitTest
{
    [TestClass]
    public class DocumentTests
    {
        [TestMethod]
        public void NewDocumentContainsOnlyTerminator()
        {
            Document document = new Document();

            Assert.IsTrue(document.IsEmpty);
            Assert.AreEqual("\0", document.Text);
            Assert.AreEqual(0, document.LastOrdinal());
        }

        [TestMethod]
        public void LoadTextCreatesCleanInMemoryDocument()
        {
            Document document = new Document();
            document.LoadText("old\nnew", "sample.cs");
            Assert.AreEqual("old\nnew\0", document.Text);
            Assert.IsFalse(document.HasUnsavedContent);
            Assert.AreEqual(Encoding.UTF8, document.CurrentEncoding);
        }

        [TestMethod]
        public void InsertAndDeletePreserveTerminatorAndDirtyState()
        {
            Document document = new Document();

            document.InsertAt(0, "hello");
            Assert.AreEqual("hello\0", document.Text);
            Assert.IsTrue(document.HasUnsavedContent);

            document.DeleteAt(1, 3);
            Assert.AreEqual("ho\0", document.Text);
            document.HasUnsavedContent = false;
            Assert.IsFalse(document.HasUnsavedContent);
        }

        [TestMethod]
        public void EmptyInsertIsNoOp()
        {
            Document document = new Document();
            int changeCount = 0;
            document.ContentChange += delegate { changeCount++; };

            document.InsertAt(0, String.Empty);

            Assert.AreEqual("\0", document.Text);
            Assert.AreEqual(0, changeCount);
            Assert.IsFalse(document.HasUnsavedContent);
        }

        [TestMethod]
        public void DeleteCannotRemoveDocumentTerminator()
        {
            Document document = new Document();
            document.InsertAt(0, "abc");

            AssertThrows<ArgumentOutOfRangeException>(() => document.DeleteAt(0, 4));
            Assert.AreEqual("abc\0", document.Text);
        }

        [TestMethod]
        public void ReplaceAllReturnsExactCaseSensitiveMatchCount()
        {
            Document document = new Document();
            document.InsertAt(0, "one ONE one");

            int count = document.ReplaceAllText("one", "two", true, false,
                Document.UNDEFINED_ORDINAL, Document.UNDEFINED_ORDINAL);

            Assert.AreEqual(2, count);
            Assert.AreEqual("two ONE two\0", document.Text);
            Assert.AreEqual(0, document.ReplaceAllText("missing", "x", true, false,
                Document.UNDEFINED_ORDINAL, Document.UNDEFINED_ORDINAL));
        }

        [TestMethod]
        public void RegexReplacementUsesActualMatchAndSupportsDeletion()
        {
            Document document = new Document();
            document.InsertAt(0, "abc 123 xyz");

            document.ReplaceWithRegexAtOrdinal("\\d+", "<$&>", true, 0);
            Assert.AreEqual("abc <123> xyz\0", document.Text);

            document.ReplaceWithRegexAtOrdinal("<123>\\s*", String.Empty, true, 0);
            Assert.AreEqual("abc xyz\0", document.Text);
        }

        [TestMethod]
        public void InvalidRegexDoesNotChangeDocument()
        {
            Document document = new Document();
            document.InsertAt(0, "unchanged");

            int count = document.ReplaceAllText("[", "x", true, true,
                Document.UNDEFINED_ORDINAL, Document.UNDEFINED_ORDINAL);

            Assert.AreEqual(0, count);
            Assert.AreEqual("unchanged\0", document.Text);
        }

        [TestMethod]
        public void RegexReplaceAllSupportsDeletion()
        {
            Document document = new Document();
            document.InsertAt(0, "one 123 two 456");

            int count = document.ReplaceAllText("\\d+\\s*", String.Empty, true, true,
                Document.UNDEFINED_ORDINAL, Document.UNDEFINED_ORDINAL);

            Assert.AreEqual(2, count);
            Assert.AreEqual("one two \0", document.Text);
        }

        [TestMethod]
        public void CaseInsensitiveReplaceHandlesMatchAtEnd()
        {
            Document document = new Document();
            document.InsertAt(0, "Alpha beta BETA");

            int count = document.ReplaceAllText("beta", "x", false, false,
                Document.UNDEFINED_ORDINAL, Document.UNDEFINED_ORDINAL);

            Assert.AreEqual(2, count);
            Assert.AreEqual("Alpha x x\0", document.Text);
        }

        [TestMethod]
        public void ReplaceAllCanBeLimitedToSelection()
        {
            Document document = new Document();
            document.InsertAt(0, "cat cat cat");

            int count = document.ReplaceAllText("cat", "dog", true, false, 4, 7);

            Assert.AreEqual(1, count);
            Assert.AreEqual("cat dog cat\0", document.Text);
        }

        [TestMethod]
        public void InsertAndDeleteRaiseConsistentChangeNotifications()
        {
            Document document = new Document();
            int shiftBegin = -1;
            int shift = 0;
            string changedContent = null;
            document.OrdinalShift += delegate(Document sender, int begin, int amount)
            {
                Assert.AreSame(document, sender);
                shiftBegin = begin;
                shift = amount;
            };
            document.ContentChange += delegate(int begin, int end, string content)
            {
                changedContent = content;
            };

            document.InsertAt(0, "abc");
            Assert.AreEqual(0, shiftBegin);
            Assert.AreEqual(3, shift);
            Assert.AreEqual("abc", changedContent);

            document.DeleteAt(1, 1);
            Assert.AreEqual(2, shiftBegin);
            Assert.AreEqual(-1, shift);
            Assert.AreEqual("b", changedContent);
            Assert.AreEqual("ac\0", document.Text);
        }

        [TestMethod]
        public void InvalidInsertAndNullContentLeaveDocumentUnchanged()
        {
            Document document = new Document();

            AssertThrows<ArgumentNullException>(() => document.InsertAt(0, null));
            AssertThrows<ArgumentOutOfRangeException>(() => document.InsertAt(1, "x"));

            Assert.AreEqual("\0", document.Text);
            Assert.IsFalse(document.HasUnsavedContent);
        }

        [TestMethod]
        public void FailedSaveLeavesDocumentUsable()
        {
            Document document = new Document();
            document.InsertAt(0, "content");
            string invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "file.txt");

            AssertThrows<DirectoryNotFoundException>(() => document.SaveFile(invalidPath));
            Assert.AreEqual("content\0", document.Text);
            Assert.IsTrue(document.HasUnsavedContent);

            document.InsertAt(7, "!");
            Assert.AreEqual("content!\0", document.Text);
        }

        [TestMethod]
        public void SaveAndLoadRoundTripPreservesUnicodeAndEncoding()
        {
            string path = Path.GetTempFileName();
            try
            {
                Document source = new Document();
                source.CurrentEncoding = Encoding.UTF8;
                source.InsertAt(0, "héllo 世界");
                source.SaveFile(path);

                Document loaded = new Document();
                loaded.LoadFile(path);

                Assert.AreEqual("héllo 世界\0", loaded.Text);
                Assert.IsFalse(loaded.HasUnsavedContent);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void AssertThrows<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected exception of type " + typeof(TException).FullName + ".");
            }
            catch (TException)
            {
            }
        }
    }
}
