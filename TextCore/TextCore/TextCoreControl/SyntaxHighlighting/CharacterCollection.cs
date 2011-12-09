using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace TextCoreControl.SyntaxHighlighting
{
    /// <summary>
    ///     Class used to represent a set of characters. It is instantiated by parsing in a string
    ///     description of the character collection. 
    ///     Example: a-zA-Z     represents all characters from a to z and A to Z.
    ///              #$%        represents characters #, $ , % alone
    ///              a-zA-Z0-9$ represents charateds a to z, A to Z, 0 to 9 and $ character
    /// </summary>
    class CharacterCollection
    {
        /// <summary>
        ///     A string definition that describes this character collection
        /// </summary>
        /// <param name="collectionDefinition"></param>
        internal CharacterCollection(string collectionDefinition)
        {
            this.characterMap = new BitArray(256);

            // Parse the collectionDefinition
            int stringLength = collectionDefinition.Length;
            for (int i = 0; i < stringLength; i++)
            {
                char character = collectionDefinition[i];
                if (character == '-' && i > 0 && i < stringLength-1)
                {
                    // need to parse a range.
                    char characterBefore = collectionDefinition[i - 1];
                    char characterAfter = collectionDefinition[i + 1];
                    if (characterBefore < characterAfter)
                    {
                        while (characterBefore != characterAfter)
                        {
                            this.AddCharacter(characterBefore);
                            characterBefore++;
                        }
                    }
                }

                // parse as single character entry
                this.AddCharacter(character);
            }
        }

        internal void AddCharacter(char character)
        {
            int characterAsInt = character;
            if (characterAsInt >= 0 && characterAsInt <= 256)
            {
                characterMap[characterAsInt] = true;
            }
        }

        /// <summary>
        ///     Checks if a given character is a member of this collection
        /// </summary>
        /// <param name="character">character to check membership of</param>
        /// <returns>true if character is contained</returns>
        internal bool Contains(char character)
        {
            int characterAsInt = character;
            if (characterAsInt >= 0 && characterAsInt <= 256)
            {
                return characterMap[characterAsInt];
            }

            return false;
        }

        BitArray characterMap;
    }
}
