using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAPICodePack.DirectX.Controls;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl.SyntaxHighlighting
{
    /// <summary>
    ///     Syntax hightlighting service that the display manager
    ///     will interact with. Maintains state information and 
    ///     drives the SyntaxHighlighterEngine.
    /// </summary>
    internal class SyntaxHighlighterService
    {
        internal SyntaxHighlighterService(string fullSyntaxFilePath, Document document)
        {
            this.syntaxHighlighterEngine = new SyntaxHightlighterEngine(fullSyntaxFilePath);
            this.syntaxHighlighterEngine.highlightRange += new SyntaxHightlighterEngine.HighlightRange(syntaxHighlighterEngine_highlightRange);
            this.colorTable = null;
            this.document = document;
            this.syntaxHighlighterStates = new OrdinalKeyedLinkedList<int>();
            this.syntaxHighlighterStates.Insert(document.FirstOrdinal(), this.syntaxHighlighterEngine.GetInitialState());
            this.dirtySyntaxStateBeginOrdinal = int.MaxValue;
            this.dirtySyntaxHighlightBeginOrdinal = int.MaxValue;
        }

        internal void NotifyOfOrdinalShift(Document document, int beginOrdinal, int shift)
        {
            this.syntaxHighlighterStates.NotifyOfOrdinalShift(document, beginOrdinal, shift);
        }

        internal void NotifyOfSettingsChange()
        {
            // Nothing to invalidate, synxtaxhighlighter doesnt depend on settings currently.
        }

        internal void NotifyOfContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (beginOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // Full reset, most likely a new file was loaded.
                this.syntaxHighlighterStates = new OrdinalKeyedLinkedList<int>();
                this.syntaxHighlighterStates.Insert(document.FirstOrdinal(), this.syntaxHighlighterEngine.GetInitialState());
                this.dirtySyntaxStateBeginOrdinal = int.MaxValue;
                this.dirtySyntaxHighlightBeginOrdinal = int.MaxValue;
            }
            else
            {
                this.syntaxHighlighterStates.Delete(beginOrdinal, endOrdinal);
                if (beginOrdinal == document.FirstOrdinal())
                {
                    this.syntaxHighlighterStates.Insert(document.FirstOrdinal(), this.syntaxHighlighterEngine.GetInitialState());
                    this.dirtySyntaxStateBeginOrdinal = document.FirstOrdinal();
                }
                else
                {
                    this.dirtySyntaxStateBeginOrdinal = Math.Min(document.PreviousOrdinal(beginOrdinal), this.dirtySyntaxStateBeginOrdinal);
                }

                this.dirtySyntaxHighlightBeginOrdinal = this.dirtySyntaxStateBeginOrdinal;
            }
        }

        internal bool CanReuseLine(VisualLine visualLine)
        {
            return visualLine.BeginOrdinal <= this.dirtySyntaxHighlightBeginOrdinal;
        }

        internal void InitDisplayResources(HwndRenderTarget hwndRenderTarget)
        {
            // The color table matches SyntaxHightlighterEngine.HighlightStyle Enum.
            SolidColorBrush[] colorTable = {
                /*NONE*/                    null, 
                /*KEYWORD1*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingKeyword1), 
                /*KEYWORD2*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingKeyword2),
                /*KEYWORD3*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingKeyword3), 
                /*KEYWORD4*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingKeyword4), 
                /*KEYWORD5*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingKeyword5), 
                /*KEYWORD6*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingKeyword6), 
                /*PREPROCESSORKEYWORD*/     hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingPreProcessorKeyword),
                /*PREPROCESSOR*/            hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingPreProcessor),
                /*COMMENT*/                 hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingComment), 
                /*OPERATOR*/                hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingOperator),
                /*BRACKET*/                 hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingBracket),
                /*NUMBER*/                  hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingNumber),
                /*STRING*/                  hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingString),
                /*CHAR*/                    hwndRenderTarget.CreateSolidColorBrush(Settings.SyntaxHighlightingChar)
            };
            this.colorTable = colorTable;
        } 

        internal void HighlightLine(ref VisualLine visualLine)
        {
            if (!Settings.EnableSyntaxHighlighting)
                return;

            this.currentVisualLine = visualLine;
            this.dirtySyntaxHighlightBeginOrdinal = Math.Max(this.dirtySyntaxHighlightBeginOrdinal, visualLine.BeginOrdinal);

            int opaqueStateIn;
            int synthesizeStateForwardBeginOrdinal = visualLine.BeginOrdinal;
            if (synthesizeStateForwardBeginOrdinal > this.dirtySyntaxStateBeginOrdinal)
            {
                System.Diagnostics.Debug.Assert(this.dirtySyntaxStateBeginOrdinal != int.MaxValue);
                synthesizeStateForwardBeginOrdinal = this.dirtySyntaxStateBeginOrdinal;
            }
            if (synthesizeStateForwardBeginOrdinal == document.LastOrdinal())
            {
                synthesizeStateForwardBeginOrdinal = Document.UNDEFINED_ORDINAL;
            }

            if (!this.syntaxHighlighterStates.Find(synthesizeStateForwardBeginOrdinal, out synthesizeStateForwardBeginOrdinal, out opaqueStateIn))
            {                
                synthesizeStateForwardBeginOrdinal = document.FirstOrdinal();
                opaqueStateIn = this.syntaxHighlighterEngine.GetInitialState();
                this.syntaxHighlighterStates.Insert(document.FirstOrdinal(), opaqueStateIn);
            }

            if (synthesizeStateForwardBeginOrdinal != visualLine.BeginOrdinal)
            {
                // Need to synthesize forward
                System.Diagnostics.Debug.Assert(synthesizeStateForwardBeginOrdinal < visualLine.BeginOrdinal);                
                int targetOrdinal = visualLine.BeginOrdinal;
                int currentOrdinal = synthesizeStateForwardBeginOrdinal;

                int checkOrdinal;
                int opaqueStateCheckOrdinal;
                if (!this.syntaxHighlighterStates.FindNext(currentOrdinal, out checkOrdinal, out opaqueStateCheckOrdinal))
                {
                    checkOrdinal = int.MaxValue;
                }
                StringBuilder stringBuilder = new StringBuilder();

                while (currentOrdinal <= targetOrdinal)
                {
                    if (currentOrdinal >= checkOrdinal || currentOrdinal == targetOrdinal)
                    {
                        string text = stringBuilder.ToString();
                        opaqueStateIn = this.syntaxHighlighterEngine.SynthesizeStateForward(text, opaqueStateIn);

                        if (this.dirtySyntaxStateBeginOrdinal <= currentOrdinal)
                        {
                            this.syntaxHighlighterStates.Delete(this.dirtySyntaxStateBeginOrdinal, currentOrdinal);
                            this.dirtySyntaxStateBeginOrdinal = currentOrdinal;
                        }
                        this.syntaxHighlighterStates.Insert(currentOrdinal, opaqueStateIn);
                        
                        if (currentOrdinal == checkOrdinal && 
                            opaqueStateCheckOrdinal == opaqueStateIn &&
                            this.dirtySyntaxStateBeginOrdinal != int.MaxValue)
                        {
                            this.dirtySyntaxStateBeginOrdinal = int.MaxValue;
                            this.HighlightLine(ref visualLine);
                            return;
                        }
                        stringBuilder = new StringBuilder();
                        if (!this.syntaxHighlighterStates.FindNext(currentOrdinal, out checkOrdinal, out opaqueStateCheckOrdinal))
                        {
                            checkOrdinal = int.MaxValue;
                        }
                    }

                    stringBuilder.Append(document.CharacterAt(currentOrdinal));
                    currentOrdinal = document.NextOrdinal(currentOrdinal);
#if DEBUG
                    DebugHUD.IterationsSynthesizingSyntaxState++;
#endif
                }
            }

            // Call to highlight text, the engine will now use callbacks 
            // to highlight the text passed in.
            int opaqueStateOut;
            syntaxHighlighterEngine.HighlightText(visualLine.Text, opaqueStateIn, out opaqueStateOut);

            if (visualLine.NextOrdinal == Document.UNDEFINED_ORDINAL)
            {
                this.syntaxHighlighterStates.Delete(this.dirtySyntaxStateBeginOrdinal, int.MaxValue);
                this.syntaxHighlighterStates.Insert(document.LastOrdinal(), opaqueStateOut);
                this.dirtySyntaxStateBeginOrdinal = int.MaxValue;
            }
            else
            {
                bool insertedDifferentValue = this.syntaxHighlighterStates.Insert(visualLine.NextOrdinal, opaqueStateOut);

                if (visualLine.NextOrdinal > this.dirtySyntaxStateBeginOrdinal)
                {
                    this.syntaxHighlighterStates.Delete(this.dirtySyntaxHighlightBeginOrdinal, document.PreviousOrdinal(visualLine.NextOrdinal));
                    if (insertedDifferentValue)
                    {
                        this.dirtySyntaxStateBeginOrdinal = Math.Max(this.dirtySyntaxStateBeginOrdinal, visualLine.NextOrdinal);
                    }
                    else
                    {
                        // more dirtiness can be wiped out
                        this.dirtySyntaxStateBeginOrdinal = int.MaxValue;
                        this.dirtySyntaxHighlightBeginOrdinal = int.MaxValue;
                    }
                }
            }
        }

        private void syntaxHighlighterEngine_highlightRange(uint beginOffset, uint length, SyntaxHightlighterEngine.HighlightStyle style)
        {
            if (colorTable != null)
            {
                currentVisualLine.SetDrawingEffect(this.colorTable[(int)style], beginOffset, length);
            }
        }

        VisualLine currentVisualLine;
        SyntaxHightlighterEngine syntaxHighlighterEngine;
        SolidColorBrush[] colorTable;
        Document document;
        OrdinalKeyedLinkedList<int> syntaxHighlighterStates;
        
        /// <summary>
        ///     All ordinals greater than dirtySyntaxHighlighterStatesBeginOrdinal
        ///     in syntaxHighlighterStates have invalid state data.
        /// </summary>
        int dirtySyntaxStateBeginOrdinal;

        /// <summary>
        ///     All lines that begin after this ordinal need to be rehighlighted.
        /// </summary>
        int dirtySyntaxHighlightBeginOrdinal;
    }
}
