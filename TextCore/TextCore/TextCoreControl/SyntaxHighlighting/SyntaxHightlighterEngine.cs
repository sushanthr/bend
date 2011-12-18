using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TextCoreControl.SyntaxHighlighting
{
    /// <summary>
    ///     A class that is capable of syntax highlighting given
    ///     a syntax definition file and state information.
    /// </summary>
    class SyntaxHightlighterEngine
    {
        internal SyntaxHightlighterEngine(string fullSyntaxFilePath)
        {
            // Default definition values
            this.keywordsMapNamespace1 = new Dictionary<string, int>();
            this.keywordsMapNamespace2 = new Dictionary<string, int>();
            this.languageType = LanguageType.NONE;
            this.namespace1 = 6;
            this.keywordCharacters = new CharacterCollection("a-zA-Z0-9_!-");
            this.keywordLength = 0;
            this.operatorCharacters = new CharacterCollection("");
            this.bracketCharacters = new CharacterCollection("");
            this.hexPrefix = "";

            this.syntaxStart = "";
            this.syntaxEnd = "";
            this.commentStart = "";
            this.commentEnd = "";
            this.commentStartAlt = "";
            this.commentEndAlt = "";
            this.singleComment = "";
            this.singleCommentAlt = "";
            this.singleCommentEsc = "";
            this.stringStart = "";
            this.stringEnd = "";
            this.stringAlt = "";
            this.stringEsc = "";
            this.charStart = "";
            this.charEnd = "";
            this.charEsc = "";
            this.stringsSpanLines = false;

            this.highlightRangeStartStringsHash = 0;
            this.highlightRangeEndStringsHash = 0;

            this.LoadDefinition(fullSyntaxFilePath);
        }

        #region Syn file Parse support
        private void LoadDefinition(string fullSyntaxFilePath)
        {
            StreamReader fileStream = new StreamReader(fullSyntaxFilePath);

            while (!fileStream.EndOfStream)
            {
                string line = fileStream.ReadLine();
                line = line.Trim();
                line = line.ToUpper();

                switch (line)
                {
                    case "TEXT=1":
                        this.languageType = LanguageType.TEXT;
                        break;
                    case "C=1":
                        this.languageType = LanguageType.C;
                        break;
                    case "HTML=1":
                        this.languageType = LanguageType.HTML;
                        this.namespace1 = 1;
                        this.syntaxStart = "<";
                        this.syntaxEnd = ">";
                        break;
                    case "PERL=1":
                        this.languageType = LanguageType.PERL;
                        break;
                    case "LATEX=1":
                        this.languageType = LanguageType.LATEX;
                        break;
                    case "[SYNTAX]":
                        this.ParseSyntaxSection(fileStream);
                        break;
                    case "[KEYWORDS 1]":
                        ParseKeywords(fileStream, 1);
                        break;
                    case "[KEYWORDS 2]":
                        ParseKeywords(fileStream, 2);
                        break;
                    case "[KEYWORDS 3]":
                        ParseKeywords(fileStream, 3);
                        break;
                    case "[KEYWORDS 4]":
                        ParseKeywords(fileStream, 4);
                        break;
                    case "[KEYWORDS 5]":
                        ParseKeywords(fileStream, 5);
                        break;
                    case "[KEYWORDS 6]":
                        ParseKeywords(fileStream, 6);
                        break;
                    case "[PREPROCESSOR KEYWORDS]":
                        ParseKeywords(fileStream, 7);
                        break;
                }
            }

            fileStream.Close();
        }

        /// <summary>
        ///     Parse keywords into the appropriate dictionary respecting the namespace settings.
        /// </summary>
        /// <param name="fileStream">Input filestream to read from</param>
        /// <param name="keywordSection">The keyword section number that is being parsed</param>
        private void ParseKeywords(StreamReader fileStream, int keywordSection)
        {
            if (keywordSection > this.namespace1)
            {
                ParseDictionary(fileStream, ref this.keywordsMapNamespace2, keywordSection, this.ignoreCase);
            }
            else
            {
                ParseDictionary(fileStream, ref this.keywordsMapNamespace1, keywordSection, this.ignoreCase);
            }
        }

        /// <summary>
        ///     Parses a contiguous collection of non empty lines in 
        ///     the file stream into the dictionary as a key.
        ///     Assigns the passed in value to the parsed keys.
        /// </summary>
        /// <param name="fileStream">Input filestream to read from</param>
        /// <param name="dictionary">Dictionary to put entries into</param>
        /// <param name="value">Value to set for the keys</param>
        /// <param name="makeLower">Should the key be made lower case before insertion</param> 
        private static void ParseDictionary(StreamReader fileStream, ref Dictionary<string, int> dictionary, int value, bool makeLower)
        {
            while (!fileStream.EndOfStream)
            {
                string line = fileStream.ReadLine();
                line.Trim();

                // Check for array end
                if (line.Length == 0)
                {
                    // Check if this is a real end of definition
                    if ((char)fileStream.Peek() == '[')
                        break;
                    else
                        continue;
                }

                // Skip comments
                if (line[0] == ';')
                    continue;

                if (makeLower)
                    line = line.ToLower();

                // A good syntax file should not have duplicates keywords in the same namespace.
                if (!dictionary.ContainsKey(line))
                {
                    dictionary.Add(line, value);
                }
            }
        }

        /// <summary>
        /// Parses the syntax section of .syn file.
        /// 
        /// The [Syntax] section must contain the following lines:
        ///  Namespace1 = a number in the range 1 to 6, which specifies how many of the keyword sections are in the first namespace. There is only one namespace when "C=1" or "LaTeX=1", so this value must be 6. There are two namespaces when "HTML=1", so this value can be between 1 and 5, and defaults to 1. Change it to 2, say, if you want to be able to display HTML tags in two colors, and split the tag keywords between the [Keywords 1] and [Keywords 2] sections, depending on which color you want them in.
        ///  IgnoreCase = "Yes" if keywords are not case sensitive, otherwise "No".
        ///  KeyWordLength = the number of characters in each keyword. If this entry is omitted, or set to 0, keywords can be of variable length. It is only required for fixed length keywords that run together, without any delimiters.
        ///  BracketChars = characters that can be used for brackets.
        ///  OperatorChars = characters that can be used for operators.
        ///  PreprocStart = the character used to start a pre-processor statement.
        ///  HexPrefix = the characters that prefix hexadecimal numbers. If this entry is omitted, it defaults to "0x".
        ///  SyntaxStart = a string of characters that switch on keyword recognition (e.g. "<"), or leave blank.
        ///  SyntaxEnd = a string of characters that switch off keyword recognition (e.g. ">"), or leave blank.
        ///  CommentStart = a string of characters that start a multiple line comment, or leave blank.
        ///  CommentEnd = a string of characters that end a multiple line comment, or leave blank.
        ///  CommentStartAlt = an alternative string of characters that start a multiple line comment.
        ///  CommentEndAlt = an alternative string of characters that end a multiple line comment.
        ///  SingleComment = a string of characters that start a single line comment (e.g. "//" or "REM"), or leave blank.
        ///  SingleCommentCol = If a single line comment must start in a specific column, assign the number here. If it must start in the first non-blank column, assign "Leading" here.
        ///  SingleCommentAlt = an alternative string of characters that start a single line comment (e.g. "//" or "REM"), or leave blank.
        ///  SingleCommentColAlt = If the alternative single line comment must start in a specific column, assign the number here. If it must start in the first non-blank column, assign "Leading" here.
        ///  SingleCommentEsc = The character that is used to escape either of the SingleComment strings.
        ///  StringsSpanLines = "Yes" if strings can continue over line boundaries, otherwise "No". This can be used in conjunction with StringEsc, if the string can only be continued when the new line is escaped with that character.
        ///  StringStart = The character that indicates the start of a string (e.g. double quote).
        ///  StringEnd = The character that indicates the end of a string (e.g. double quote).
        ///  StringAlt = An alternative character that can be used to delimit strings, if the string contains the StringEnd character.
        ///  StringEsc = The character that is used to escape the StringEnd character, if it is part of a string (e.g. "\").
        ///  CharStart = The character that is used to start a character literal (e.g. single quote).
        ///  CharEnd = The character that is used to end a character literal (e.g. single quote).
        ///  CharEsc = The character that is used to escape the CharEnd character, if it is part of a character literal (e.g. "\").
        ///
        /// </summary>
        /// <param name="fileStream"> File Stream to parse</param>
        private void ParseSyntaxSection(StreamReader fileStream)
        {
            while (!fileStream.EndOfStream)
            {
                string line = fileStream.ReadLine();
                line.Trim();

                // Check for array end
                if (line.Length == 0)
                    break;

                // Skip comments
                if (line[0] == ';')
                    continue;

                int indexOfEqual = line.IndexOf('=');
                if (indexOfEqual > 0)
                {
                    string property = line.Substring(0, indexOfEqual);
                    string value = line.Substring(indexOfEqual + 1);
                    property = property.Trim();
                    value = value.Trim();

                    // If the value is not specified, leave it at the default value.
                    if (value.Length == 0)
                        continue;

                    int valInt;
                    switch (property.ToLower())
                    {
                        case "namespace1":
                            valInt = int.Parse(value);
                            if (valInt >= 1 && valInt <= 6) this.namespace1 = valInt;
                            break;
                        case "ignorecase":
                            this.ignoreCase = Boolify(value);
                            break;
                        case "keywordchars":
                            this.keywordCharacters = new CharacterCollection(value);
                            break;
                        case "keywordlength":
                            valInt = int.Parse(value);
                            if (valInt >= 1 && valInt <= 6) this.keywordLength = valInt;
                            break;
                        case "bracketchars":
                            this.bracketCharacters = new CharacterCollection(value);
                            break;
                        case "operatorchars":
                            this.operatorCharacters = new CharacterCollection(value);
                            break;
                        case "preprocstart":
                            if (value.Length == 1) this.keywordCharacters.AddCharacter(this.preProcessorCharacter = value[0]);
                            break;
                        case "hexprefix":
                            this.hexPrefix = value;
                            break;
                        case "syntaxstart":
                            this.syntaxStart = value;
                            break;
                        case "syntaxend":
                            this.syntaxEnd = value;
                            break;
                        case "commentstart":
                            this.commentStart = value;
                            break;
                        case "commentend":
                            this.commentEnd = value;
                            break;
                        case "commentstartalt":
                            this.commentStartAlt = value;
                            break;
                        case "commentendalt":
                            this.commentEndAlt = value;
                            break;
                        case "singlecomment":
                            this.singleComment = value;
                            break;
                        case "singlecommentcol":
                            break;
                        case "singlecommentalt":
                            this.singleCommentAlt = value;
                            break;
                        case "singlecommentcolalt":
                            break;
                        case "singlecommentesc":
                            this.singleCommentEsc = value;
                            break;
                        case "stringsspanlines":
                            this.stringsSpanLines = Boolify(value);
                            break;
                        case "stringstart":
                            this.stringStart = value;
                            break;
                        case "stringend":
                            this.stringEnd = value;
                            break;
                        case "stringalt":
                            this.stringAlt = value;
                            break;
                        case "stringesc":
                            this.stringEsc = value;
                            break;
                        case "charstart":
                            this.charStart = value;
                            break;
                        case "charend":
                            this.charEnd = value;
                            break;
                        case "charesc":
                            this.charEsc = value;
                            break;
                    }
                }
            }

            this.ComputeHightlightRangeHashs();
        }

        private static bool Boolify(string value)
        {
            if (value.ToLower() == "yes") return true;
            return false;
        }

        #endregion

        #region HighlightRange

        private enum HighlightState
        {
            NONE                    = 0x0000,
            IN_NO_SYNTAX            = 0x0001,
            IN_COMMENT              = 0X0002,
            IN_COMMENT_ALT          = 0x0004,
            IN_SINGLE_COMMENT       = 0x0008,
            IN_SINGLE_COMMENT_ALT   = 0x0010,
            IN_STRING               = 0x0020,
            IN_CHAR                 = 0x0040,
            AFTER_HARD_BREAK        = 0x00010000,

            STATE_FLAGS             = 0x0000FFFF,
            OTHER_FLAGS             = 0x0000FFFF
        };

        public int GetInitialState() 
        {
            if (this.syntaxStart == null || this.syntaxStart == "")
                return (int)HighlightState.NONE;
            else
                return (int)HighlightState.IN_NO_SYNTAX;
        }

        private void ComputeHightlightRangeHashs()
        {
            if (this.syntaxEnd != null && this.syntaxEnd.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.syntaxEnd[0]);
            if (this.commentStart != null && this.commentStart.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.commentStart[0]);
            if (this.commentStartAlt != null && this.commentStartAlt.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.commentStartAlt[0]);
            if (this.singleComment != null && this.singleComment.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.singleComment[0]);
            if (this.singleCommentAlt != null && this.singleCommentAlt.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.singleCommentAlt[0]);
            if (this.stringStart != null && this.stringStart.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.stringStart[0]);
            if (this.charStart != null && this.charStart.Length != 0) this.highlightRangeStartStringsHash |= ComputeHash(this.charStart[0]);

            if (this.syntaxStart != null && this.syntaxStart.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.syntaxStart[0]);
            if (this.commentEnd != null && this.commentEnd.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.commentEnd[0]);
            if (this.commentEndAlt != null && this.commentEndAlt.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.commentEndAlt[0]);
            if (this.singleCommentEsc != null && this.singleCommentEsc.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.singleCommentEsc[0]);
            if (this.stringEnd != null && this.stringEnd.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.stringEnd[0]);
            if (this.stringAlt != null && this.stringAlt.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.stringAlt[0]);
            if (this.charEnd != null && this.charEnd.Length != 0) this.highlightRangeEndStringsHash |= ComputeHash(this.charEnd[0]);
        }

        private static int ComputeHash(char character)
        {
            int hash = (int)character;
            if (hash < 256)
            {
                // We have 24 bits to encode extra info which slice of 256/24 this character falls in.
                int slice = (hash / 11) + 8;
                slice = (1 << slice);
                hash |= slice;
            }
            else
            {
                // Not Enough Information for a hash permit everything
                hash = (0XFFFF | 0X0000FFFF);
            }
            return hash;
        }

        private bool IsPossibleHighlightRangeStart(char character)
        {
            if ((character & this.highlightRangeStartStringsHash) != 0)
            {
                int slice = (1 << (((int)character / 11) + 8));
                return ((slice & this.highlightRangeStartStringsHash) != 0);
            }
            return false;
        }

        private bool IsPossibleHighlightRangeEnd(char character)
        {
            if ((character & this.highlightRangeEndStringsHash) != 0)
            {
                int slice = (1 << (((int)character / 11) + 8));
                return ((slice & this.highlightRangeEndStringsHash) != 0);
            }
            return false;
        }

        internal int SynthesizeStateForward(string text, int stateAtStart)
        {
            bool isAfterHardBreak = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (this.IsPossibleHighlightRangeStart(ch))
                {
                    if (CompareStrings(this.syntaxEnd, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_NO_SYNTAX;
                        i += (this.syntaxEnd.Length - 1);
                    }
                    else if (CompareStrings(this.commentStart, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_COMMENT;
                        i += (this.commentStart.Length - 1);
                    }
                    else if (CompareStrings(this.commentStartAlt, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_COMMENT_ALT;
                        i += (this.commentStartAlt.Length - 1);
                    }
                    else if (CompareStrings(this.singleComment, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_SINGLE_COMMENT;
                        i += (this.singleComment.Length - 1);
                    }
                    else if (CompareStrings(this.singleCommentAlt, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_SINGLE_COMMENT_ALT;
                        i += (this.singleCommentAlt.Length - 1);
                    }
                    else if (CompareStrings(this.stringStart, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_STRING;
                        i += (this.stringStart.Length - 1);
                    }
                    else if (CompareStrings(this.charStart, 0, text, i))
                    {
                        stateAtStart |= (int)HighlightState.IN_CHAR;
                        i += (this.charStart.Length - 1);
                    }
                }
                else if (this.IsPossibleHighlightRangeEnd(ch))
                {
                    if (((stateAtStart & (int)HighlightState.IN_NO_SYNTAX) != 0) && CompareStrings(this.syntaxStart, 0, text, i))
                    {
                        stateAtStart = (int)HighlightState.NONE;
                    }

                    else if (((stateAtStart & (int)HighlightState.IN_COMMENT) != 0) && CompareStrings(this.commentEnd, 0, text, i))
                    {
                        i += (this.commentEnd.Length - 1);
                        stateAtStart = (int)HighlightState.NONE;
                    }
                    else if (((stateAtStart & (int)HighlightState.IN_COMMENT_ALT) != 0) && CompareStrings(this.commentEndAlt, 0, text, i))
                    {
                        i += (this.commentEndAlt.Length - 1);
                        stateAtStart = (int)HighlightState.NONE;
                    }
                    else if (((stateAtStart & (int)HighlightState.IN_STRING) != 0) && CompareStrings(this.stringEnd, 0, text, i))
                    {
                        i += (this.stringEnd.Length - 1);
                        stateAtStart = (int)HighlightState.NONE;
                    }
                    else if (((stateAtStart & (int)HighlightState.IN_STRING) != 0) && CompareStrings(this.stringAlt, 0, text, i))
                    {
                        i += (this.stringAlt.Length - 1);
                        stateAtStart = (int)HighlightState.NONE;
                    }
                    else if (((stateAtStart & (int)HighlightState.IN_CHAR) != 0) && CompareStrings(this.charEnd, 0, text, i))
                    {
                        i += (this.charEnd.Length - 1);
                        stateAtStart = (int)HighlightState.NONE;
                    }
                    else if ((((stateAtStart & (int)HighlightState.IN_SINGLE_COMMENT) != 0) || ((stateAtStart & (int)HighlightState.IN_SINGLE_COMMENT_ALT) != 0))
                        && CompareStrings(this.singleCommentEsc, 0, text, i))
                    {
                        i += (this.singleCommentEsc.Length - 1);
                        stateAtStart = (int)HighlightState.NONE;
                    }
                }
                else if (ch == '\r' || ch == '\n')
                {
                    if ((stateAtStart & (int)HighlightState.IN_SINGLE_COMMENT) != 0) stateAtStart = (int)HighlightState.NONE;
                    if ((stateAtStart & (int)HighlightState.IN_STRING) != 0 && !this.stringsSpanLines) stateAtStart = (int)HighlightState.NONE;
                    if ((stateAtStart & (int)HighlightState.IN_CHAR) != 0) stateAtStart = (int)HighlightState.NONE;
                }
            }
            if (text.Length > 0 && (text[text.Length - 1] == '\r' || text[text.Length - 1] == '\n'))
            {
                stateAtStart |= (int)HighlightState.AFTER_HARD_BREAK;
            }
            return stateAtStart;
        }

        #endregion

        #region Highlighter

        /// <summary>
        ///     Finds and highlights a range of characters
        /// </summary>
        /// <param name="highlightState">Current highlighter state</param>
        /// <param name="text">text to highlight</param>
        /// <param name="highlightStartIndex">index to extend highlight of the first char to the left by</param>
        /// <param name="findStartIndex">index in text to start highlighting form</param>
        /// <param name="continueFromIndex">index for other systems to continue highlighting at</param>
        /// <param name="newHighlightState">the new state for the highlighter</param>
        private void FindAndHighlightRange(int highlightState, string text, int highlightStartIndex, int findStartIndex, out int continueFromIndex, out int newHighlightState, out bool transitionedIntoSyntaxRegion)
        {
            System.Diagnostics.Debug.Assert(highlightState != 0);
            newHighlightState = highlightState;
            continueFromIndex = findStartIndex;
            transitionedIntoSyntaxRegion = false;

            int index = findStartIndex;
            while (index < text.Length)
            {
                char ch = text[index];
                HighlightStyle highLightStyle = HighlightStyle.NONE;
                if (this.IsPossibleHighlightRangeEnd(ch))
                {
                    if (((highlightState & (int)HighlightState.IN_NO_SYNTAX) != 0) && CompareStrings(this.syntaxStart, 0, text, index))
                    {
                        continueFromIndex = index + 1;
                        newHighlightState = (int)HighlightState.NONE;
                        transitionedIntoSyntaxRegion = true;
                        this.highlightRange((uint)index, (uint)this.syntaxStart.Length, HighlightStyle.BRACKET);
                        break;
                    }

                    else if (((highlightState & (int)HighlightState.IN_COMMENT) != 0) && CompareStrings(this.commentEnd, 0, text, index))
                    {
                        index += this.commentEnd.Length;
                        highLightStyle = HighlightStyle.COMMENT;
                    }
                    else if (((highlightState & (int)HighlightState.IN_COMMENT_ALT) != 0) && CompareStrings(this.commentEndAlt, 0, text, index))
                    {
                        index += this.commentEndAlt.Length;
                        highLightStyle = HighlightStyle.COMMENT;
                    }
                    else if (((highlightState & (int)HighlightState.IN_STRING) != 0) && CompareStrings(this.stringEnd, 0, text, index))
                    {
                        index += this.stringEnd.Length;
                        highLightStyle = HighlightStyle.STRING;
                    }
                    else if (((highlightState & (int)HighlightState.IN_STRING) != 0) && CompareStrings(this.stringAlt, 0, text, index))
                    {
                        index += this.stringAlt.Length;
                        highLightStyle = HighlightStyle.STRING;
                    }
                    else if (((highlightState & (int)HighlightState.IN_CHAR) != 0) && CompareStrings(this.charEnd, 0, text, index))
                    {
                        index += this.charEnd.Length;
                        highLightStyle = HighlightStyle.CHAR;
                    }
                    else if ((((highlightState & (int)HighlightState.IN_SINGLE_COMMENT) != 0) || ((highlightState & (int)HighlightState.IN_SINGLE_COMMENT_ALT) != 0))
                        && CompareStrings(this.singleCommentEsc, 0, text, index))
                    {
                        index += this.singleCommentEsc.Length;
                        highLightStyle = HighlightStyle.COMMENT;
                    }
                }

                if (highLightStyle == HighlightStyle.NONE && (ch == '\r' || ch == '\n'))
                {
                    if ((highlightState & (int)HighlightState.IN_SINGLE_COMMENT) != 0) highLightStyle = HighlightStyle.COMMENT;
                    if ((highlightState & (int)HighlightState.IN_STRING) != 0 && !this.stringsSpanLines) highLightStyle = HighlightStyle.STRING;
                    if ((highlightState & (int)HighlightState.IN_CHAR) != 0) highLightStyle = HighlightStyle.STRING;
                }

                if (highLightStyle != HighlightStyle.NONE)
                {
                    continueFromIndex = index;
                    this.highlightRange((uint)highlightStartIndex, (uint)(index - highlightStartIndex), highLightStyle);
                    newHighlightState = (int)HighlightState.NONE;
                    break;
                }

                index++;
            }

            if (newHighlightState != 0)
            {
                HighlightStyle highLightStyle = HighlightStyle.NONE;
                if ((highlightState & (int)HighlightState.IN_COMMENT) != 0)
                    highLightStyle = HighlightStyle.COMMENT;
                else if ((highlightState & (int)HighlightState.IN_COMMENT_ALT) != 0)
                    highLightStyle = HighlightStyle.COMMENT;
                else if ((highlightState & (int)HighlightState.IN_STRING) != 0)
                    highLightStyle = HighlightStyle.STRING;
                else if ((highlightState & (int)HighlightState.IN_STRING) != 0)
                    highLightStyle = HighlightStyle.STRING;
                else if ((highlightState & (int)HighlightState.IN_CHAR) != 0)
                    highLightStyle = HighlightStyle.CHAR;
                else if ((highlightState & (int)HighlightState.IN_SINGLE_COMMENT) != 0)
                    highLightStyle = HighlightStyle.COMMENT;
                else if ((highlightState & (int)HighlightState.IN_SINGLE_COMMENT_ALT) != 0)
                    highLightStyle = HighlightStyle.COMMENT;

                continueFromIndex = index;
                if (highLightStyle != HighlightStyle.NONE)
                {
                    this.highlightRange((uint)highlightStartIndex, (uint)(text.Length - highlightStartIndex), highLightStyle);
                }
            }
        }

        public void HighlightText(string text, int opaqueStateIn, out int opaqueStateOut)
        {
            opaqueStateIn = opaqueStateIn & (int)HighlightState.STATE_FLAGS;
            opaqueStateOut = (int)HighlightState.NONE; 

            if (this.highlightRange != null)
            {
                int i = 0;
                char ch;
                bool onlyDetectKeywords = false;
                bool previousTokenIsSyntaxStart = false;

                if (opaqueStateIn != 0)
                    this.FindAndHighlightRange(opaqueStateIn, text, i, i, out i, out opaqueStateOut, out previousTokenIsSyntaxStart);
                else
                {
                    // Sniff for preprocessor directives
                    // Skip over leading whitespaces
                    for (; i < text.Length; i++)
                    {
                        if (!char.IsWhiteSpace(ch = text[i]))
                        {
                            // highlight whole line as preprocessor line
                            if (ch == preProcessorCharacter)
                            {
                                this.highlightRange(0, (uint)text.Length, HighlightStyle.PREPROCESSOR);
                                onlyDetectKeywords = true;
                            }
                            break;
                        }
                    }
                }

                string keyword = "";
                for (; i < text.Length; i++)
                {
                    // Look for keywords
                    ch = text[i];
                    if (this.keywordCharacters.Contains(ch) &&
                        (this.keywordLength == 0 || keyword.Length < this.keywordLength))
                    {
                        keyword += ch;
                    }
                    else
                    {
                        int beginNonKeywordScan = i;
                        int endNonKeywordScan = i + 1;
                        int style;
                        if (keyword.Length != 0)
                        {
                            if (this.keywordsMapNamespace1.TryGetValue(keyword, out style) ||
                                (!previousTokenIsSyntaxStart && this.keywordsMapNamespace2.TryGetValue(keyword, out style)))
                            {
                                // Found a keyword
                                HighlightStyle highlightStyle = (HighlightStyle)style;
                                uint length = (uint)keyword.Length;
                                uint rangeBegin = (uint)(i - length);
                                this.highlightRange(rangeBegin, length, highlightStyle);
                            }
                            else
                            {
                                // It wasnt a keyword so make a pass scanning for non keyword
                                beginNonKeywordScan = (i - keyword.Length);
                            }
                        }
                        keyword = "";
                        previousTokenIsSyntaxStart = false;

                        if (!onlyDetectKeywords)
                        {
                            // Now detect this non keyword character
                            while (beginNonKeywordScan < endNonKeywordScan)
                            {
                                ch = text[beginNonKeywordScan];

                                // Look for single line comment
                                if (this.IsPossibleHighlightRangeStart(ch))
                                {
                                    HighlightState highlightState = HighlightState.NONE;
                                    int highlightStartIndex = beginNonKeywordScan;
                                    if (CompareStrings(this.syntaxEnd, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_NO_SYNTAX;
                                        this.highlightRange((uint)beginNonKeywordScan, (uint)this.syntaxEnd.Length, HighlightStyle.BRACKET);
                                        beginNonKeywordScan += this.syntaxEnd.Length;
                                    }
                                    else if (CompareStrings(this.commentStart, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_COMMENT;
                                        beginNonKeywordScan += this.commentStart.Length;
                                    }
                                    else if (CompareStrings(this.commentStartAlt, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_COMMENT_ALT;
                                        beginNonKeywordScan += this.commentStartAlt.Length;
                                    }
                                    else if (CompareStrings(this.singleComment, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_SINGLE_COMMENT;
                                        beginNonKeywordScan += this.singleComment.Length;
                                    }
                                    else if (CompareStrings(this.singleCommentAlt, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_SINGLE_COMMENT_ALT;
                                        beginNonKeywordScan += this.singleCommentAlt.Length;
                                    }
                                    else if (CompareStrings(this.stringStart, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_STRING;
                                        beginNonKeywordScan += this.stringStart.Length;
                                    }
                                    else if (CompareStrings(this.charStart, 0, text, beginNonKeywordScan))
                                    {
                                        highlightState = HighlightState.IN_CHAR;
                                        beginNonKeywordScan += this.charStart.Length;
                                    }

                                    if (highlightState != HighlightState.NONE)
                                    {
                                        opaqueStateOut |= (int)highlightState;
                                        this.FindAndHighlightRange(opaqueStateOut, text, highlightStartIndex, beginNonKeywordScan, out beginNonKeywordScan, out opaqueStateOut, out previousTokenIsSyntaxStart);
                                        break;
                                    }
                                }

                                bool detectFollowingDigit = false; 
                                // Look for operator
                                if (this.operatorCharacters.Contains(ch))
                                {
                                    this.highlightRange((uint)beginNonKeywordScan, 1, HighlightStyle.OPERATOR);
                                    detectFollowingDigit = true;
                                }

                                // Look for bracket characters
                                else if (this.bracketCharacters.Contains(ch))
                                {
                                    this.highlightRange((uint)beginNonKeywordScan, 1, HighlightStyle.BRACKET);
                                    detectFollowingDigit = true;
                                }

                                detectFollowingDigit |= (ch == ' ' || ch == ',' || ch == '.');
                                beginNonKeywordScan++;

                                // look for hex prefix
                                if (detectFollowingDigit)
                                {
                                    bool isHexNumber = false;
                                    if (CompareStrings(hexPrefix, 0, text, beginNonKeywordScan))
                                    {
                                        this.highlightRange((uint)beginNonKeywordScan, (uint)hexPrefix.Length, HighlightStyle.NUMBER);
                                        beginNonKeywordScan += hexPrefix.Length;
                                        isHexNumber = true;
                                    }

                                    while (beginNonKeywordScan < text.Length)
                                    {
                                        if (char.IsDigit(text[beginNonKeywordScan]))
                                        {
                                            this.highlightRange((uint)beginNonKeywordScan, 1, HighlightStyle.NUMBER);
                                            beginNonKeywordScan++;
                                            continue;
                                        }
                                        else if (isHexNumber)
                                        {
                                            char chHex = char.ToLower(text[beginNonKeywordScan]);
                                            if (chHex >= 'a' && chHex <= 'f')
                                            {
                                                this.highlightRange((uint)beginNonKeywordScan, 1, HighlightStyle.NUMBER);
                                                beginNonKeywordScan++;
                                                continue;
                                            }
                                        }
                                        break;
                                    }
                                }
                            }

                            // we could have covered a lot more ground update i.
                            i = beginNonKeywordScan - 1;
                        }
                    }
                }
            }

            if (text.Length > 0 && (text[text.Length - 1] == '\r' || text[text.Length - 1] == '\n'))
            {
                opaqueStateOut |= (int)HighlightState.AFTER_HARD_BREAK;
            }
        }

        private static bool CompareStrings(string string1, int offset1, string string2, int offset2)
        {
            if (string1 == null)
                return false;

            int string1Length = string1.Length;
            int string2Length = string2.Length;

            if (string1Length > string2Length - offset2 || string1Length == 0)
                return false;

            while (offset1 < string1Length && offset2 < string2Length)
            {
                if (string1[offset1] != string2[offset2])
                    return false;

                offset1++;
                offset2++;
            }
            return true;
        }

        #region Highlighter callbacks

        public enum HighlightStyle 
        { 
            NONE = 0, 
            KEYWORD1 = 1, 
            KEYWORD2 = 2, 
            KEYWORD3 = 3, 
            KEYWORD4 = 4, 
            KEYWORD5 = 5, 
            KEYWORD6 = 6, 
            PREPROCESSORKEYWORD = 7, 
            PREPROCESSOR = 8,
            COMMENT = 9,
            OPERATOR = 10,
            BRACKET = 11,
            NUMBER = 12,
            STRING = 13,
            CHAR   = 14
        }

        public delegate void HighlightRange(uint beginOffset, uint length, HighlightStyle style);
        public event HighlightRange highlightRange;

        #endregion


        #endregion

        #region syntax rules
        private enum LanguageType { NONE, TEXT, C, HTML, PERL, LATEX };
        LanguageType    languageType;
        private bool    ignoreCase;
        /// <summary>
        ///     #number such that last [keyword #number] inclusive which are in first namespace.
        /// </summary>
        private int     namespace1;
        private CharacterCollection keywordCharacters;
        private CharacterCollection bracketCharacters;
        private int     keywordLength;
        private char    preProcessorCharacter;
        private CharacterCollection operatorCharacters;
        private string  hexPrefix;
        private string  syntaxStart;
        private string  syntaxEnd;
        private string  commentStart;
        private string  commentEnd;
        private string  commentStartAlt;
        private string  commentEndAlt;
        private string  singleComment;
        private string  singleCommentAlt;
        private string  singleCommentEsc;
        private string  stringStart;
        private string  stringEnd;
        private string  stringAlt;
        private string  stringEsc;
        private string  charStart;
        private string  charEnd;
        private string  charEsc;
        private bool stringsSpanLines;
        #endregion

        private int highlightRangeStartStringsHash;
        private int highlightRangeEndStringsHash;

        private Dictionary<string, int> keywordsMapNamespace1;
        private Dictionary<string, int> keywordsMapNamespace2;
    }
}