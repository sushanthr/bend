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
            this.keywordsMap = new Dictionary<string, int>();
            this.languageType = LanguageType.NONE;
            this.namespace1 = 6;

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
                        this.namespace1 = 1;
                        break;
                    case "C=1":
                        this.languageType = LanguageType.C;
                        this.namespace1 = 1;
                        break;
                    case "HTML=1":
                        this.languageType = LanguageType.HTML;
                        this.namespace1 = 6;
                        break;
                    case "PERL=1":
                        this.languageType = LanguageType.PERL;
                        break;
                    case "LATEX=1":
                        this.languageType = LanguageType.LATEX;
                        this.namespace1 = 1;
                        break;
                    case "[SYNTAX]":
                        this.ParseSyntaxSection(fileStream);
                        break;
                    case "[KEYWORDS 1]":
                        ParseArray(fileStream, ref this.keywordsMap, 1, this.ignoreCase);
                        break;
                    case "[KEYWORDS 2]":
                        ParseArray(fileStream, ref this.keywordsMap, Math.Min(2, this.namespace1), this.ignoreCase);
                        break;
                    case "[KEYWORDS 3]":
                        ParseArray(fileStream, ref this.keywordsMap, Math.Min(3, this.namespace1), this.ignoreCase);
                        break;
                    case "[KEYWORDS 4]":
                        ParseArray(fileStream, ref this.keywordsMap, Math.Min(4, this.namespace1), this.ignoreCase);
                        break;
                    case "[KEYWORDS 5]":
                        ParseArray(fileStream, ref this.keywordsMap, Math.Min(5, this.namespace1), this.ignoreCase);
                        break;
                    case "[KEYWORDS 6]":
                        ParseArray(fileStream, ref this.keywordsMap, Math.Min(6, this.namespace1), this.ignoreCase);
                        break;
                    case "[PREPROCESSOR KEYWORDS]":
                        ParseArray(fileStream, ref this.keywordsMap, 7, this.ignoreCase);
                        break;
                }
            }

            fileStream.Close();
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
        private static void ParseArray(StreamReader fileStream, ref Dictionary<string, int> dictionary, int value, bool makeLower)
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

                if (makeLower)
                    line = line.ToLower();

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

                    switch (property.ToLower())
                    {
                        case "namespace1":
                            int valInt = int.Parse(value);
                            if (valInt >= 1 && valInt <= 6)
                            {
                                namespace1 = valInt;
                            }
                            break;
                        case "ignorecase":
                            this.ignoreCase = Boolify(value);
                            break;
                        case "keywordlength":
                            break;
                        case "bracketchars":
                            break;
                        case "operatorchars":
                            break;
                        case "preprocstart":
                            break;
                        case "hexprefix":
                            break;
                        case "syntaxstart":
                            break;
                        case "syntaxend":
                            break;
                        case "commentstart":
                            break;
                        case "commentend":
                            break;
                        case "commentstartalt":
                            break;
                        case "commentendalt":
                            break;
                        case "singlecomment":
                            break;
                        case "singlecommentcol":
                            break;
                        case "singlecommentalt":
                            break;
                        case "singlecommentcolalt":
                            break;
                        case "singlecommentesc":
                            break;
                        case "stringsspanlines":
                            break;
                        case "stringstart":
                            break;
                        case "stringend":
                            break;
                        case "stringalt":
                            break;
                        case "stringesc":
                            break;
                        case "charstart":
                            break;
                        case "charend":
                            break;
                        case "charesc":
                            break;
                    }
                }
            }
        }

        private static bool Boolify(string value)
        {
            if (value.ToLower() == "yes") return true;
            return false;
        }

        #endregion

        #region Highlighter

        public void HighlightText(string text)
        {
            if (this.highlightRange != null)
            {
                string keyword = "";
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];
                    if (char.IsWhiteSpace(ch))
                    {
                        int style;
                        if (keyword.Length != 0 && this.keywordsMap.TryGetValue(keyword, out style))
                        {
                            // Found a keyword
                            HighlightStyle highlightStyle = (HighlightStyle)style;
                            uint length = (uint)keyword.Length;
                            uint rangeBegin = (uint)(i - length);
                            this.highlightRange(rangeBegin, length, highlightStyle);
                        }

                        keyword = "";
                    }
                    else
                    {
                        keyword += ch;
                    }
                }
            }
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
            COMMENT = 9 
        }

        public delegate void HighlightRange(uint beginOffset, uint endOffset, HighlightStyle style);
        public event HighlightRange highlightRange;

        #endregion


        #endregion

        #region syntax rules
        private enum LanguageType { NONE, TEXT, C, HTML, PERL, LATEX };
        LanguageType    languageType;
        private int     namespace1;
        private bool    ignoreCase;
        #endregion

        private Dictionary<string, int> keywordsMap;
    }
}