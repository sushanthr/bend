using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextCoreControl;

namespace TextCoreUnitTest
{
    [TestClass]
    public class UndoRedoManagerTests
    {
        [TestMethod]
        public void UndoAndRedoRestoreInsertedText()
        {
            Document document = new Document();
            UndoRedoManager manager = new UndoRedoManager(document);
            document.InsertAt(0, "hello");

            manager.Undo();
            Assert.AreEqual("\0", document.Text);

            manager.Redo();
            Assert.AreEqual("hello\0", document.Text);
        }

        [TestMethod]
        public void TransactionIsUndoneAndRedoneAsOneOperation()
        {
            Document document = new Document();
            UndoRedoManager manager = new UndoRedoManager(document);

            manager.BeginTransaction();
            document.InsertAt(0, "one");
            document.InsertAt(3, " two");
            manager.EndTransaction();

            manager.Undo();
            Assert.AreEqual("\0", document.Text);

            manager.Redo();
            Assert.AreEqual("one two\0", document.Text);
        }

        [TestMethod]
        public void NewEditAfterUndoClearsRedoBranch()
        {
            Document document = new Document();
            UndoRedoManager manager = new UndoRedoManager(document);
            document.InsertAt(0, "first");
            manager.Undo();

            document.InsertAt(0, "second");
            manager.Redo();

            Assert.AreEqual("second\0", document.Text);
        }

        [TestMethod]
        public void UndoAndRedoRestoreDeletedText()
        {
            Document document = new Document();
            document.InsertAt(0, "abcdef");
            UndoRedoManager manager = new UndoRedoManager(document);

            document.DeleteAt(2, 3);
            Assert.AreEqual("abf\0", document.Text);

            manager.Undo();
            Assert.AreEqual("abcdef\0", document.Text);

            manager.Redo();
            Assert.AreEqual("abf\0", document.Text);
        }

        [TestMethod]
        public void UndoAndRedoOnEmptyHistoryAreNoOps()
        {
            Document document = new Document();
            UndoRedoManager manager = new UndoRedoManager(document);

            manager.Undo();
            manager.Redo();

            Assert.AreEqual("\0", document.Text);
        }
    }
}
