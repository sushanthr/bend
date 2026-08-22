using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextCoreControl;
using TextCoreControl.SyntaxHighlighting;

namespace TextCoreUnitTest
{
    [TestClass]
    public class OrdinalKeyedLinkedListTests
    {
        [TestMethod]
        public void FindReturnsNearestValueAtOrBeforeOrdinal()
        {
            OrdinalKeyedLinkedList<int> list = new OrdinalKeyedLinkedList<int>();
            list.Insert(2, 20);
            list.Insert(8, 80);

            int ordinal;
            int value;
            Assert.IsTrue(list.Find(6, out ordinal, out value));
            Assert.AreEqual(2, ordinal);
            Assert.AreEqual(20, value);
        }

        [TestMethod]
        public void DeleteRemovesInclusiveOrdinalRange()
        {
            OrdinalKeyedLinkedList<int> list = new OrdinalKeyedLinkedList<int>();
            list.Insert(1, 10);
            list.Insert(5, 50);
            list.Insert(9, 90);

            list.Delete(5, 9);

            int ordinal;
            int value;
            Assert.IsTrue(list.Find(20, out ordinal, out value));
            Assert.AreEqual(1, ordinal);
            Assert.AreEqual(10, value);
        }

        [TestMethod]
        public void OrdinalShiftMovesValuesAfterInsertionPoint()
        {
            Document document = new Document();
            OrdinalKeyedLinkedList<int> list = new OrdinalKeyedLinkedList<int>();
            list.Insert(2, 20);
            list.Insert(8, 80);

            list.NotifyOfOrdinalShift(document, 4, 3);

            int ordinal;
            int value;
            Assert.IsTrue(list.Find(11, out ordinal, out value));
            Assert.AreEqual(11, ordinal);
            Assert.AreEqual(80, value);
        }
    }
}
