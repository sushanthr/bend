using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextCoreControl.SyntaxHighlighting;

namespace TextCoreUnitTest
{
    [TestClass]
    public class CharacterCollectionTests
    {
        [TestMethod]
        public void ParsesRangesAndLiteralCharacters()
        {
            CharacterCollection collection = new CharacterCollection("a-cX0-2");

            Assert.IsTrue(collection.Contains('a'));
            Assert.IsTrue(collection.Contains('b'));
            Assert.IsTrue(collection.Contains('c'));
            Assert.IsTrue(collection.Contains('X'));
            Assert.IsTrue(collection.Contains('1'));
            Assert.IsFalse(collection.Contains('d'));
        }

        [TestMethod]
        public void CharactersOutsideByteRangeAreSafelyIgnored()
        {
            CharacterCollection collection = new CharacterCollection("abc");

            collection.AddCharacter('\u0100');

            Assert.IsFalse(collection.Contains('\u0100'));
            Assert.IsFalse(collection.Contains('\u4e16'));
        }
    }
}
