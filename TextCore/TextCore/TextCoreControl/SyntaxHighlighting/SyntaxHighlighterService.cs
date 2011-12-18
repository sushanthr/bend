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
            this.document.ContentChange += new Document.ContentChangeEventHandler(document_ContentChange);
            this.dirtySyntaxHighlighterStatesBeginOrdinal = int.MaxValue;
        }

        private void document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (beginOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // Full reset, most likely a new file was loaded.
                this.syntaxHighlighterStates = new OrdinalKeyedLinkedList<int>();
                this.syntaxHighlighterStates.Insert(document.FirstOrdinal(), this.syntaxHighlighterEngine.GetInitialState());
                this.DirtySyntaxHighlighterStatesBeginOrdinal = int.MaxValue;
            }
            else
            {
                this.syntaxHighlighterStates.Delete(beginOrdinal, endOrdinal);
                this.DirtySyntaxHighlighterStatesBeginOrdinal = Math.Min(endOrdinal, this.DirtySyntaxHighlighterStatesBeginOrdinal);
            }
        }

        internal bool CanReuseLine(VisualLine visualLine)
        {
            return visualLine.BeginOrdinal < document.PreviousOrdinal(this.DirtySyntaxHighlighterStatesBeginOrdinal);
        }

        internal void InitDisplayResources(HwndRenderTarget hwndRenderTarget)
        {
            // The color table matches SyntaxHightlighterEngine.HighlightStyle Enum.
            SolidColorBrush[] colorTable = {
                /*NONE*/                    null, 
                /*KEYWORD1*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0, 102f/255, 153f/255)), 
                /*KEYWORD2*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0,   0, 128f/255)),
                /*KEYWORD3*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0,   0, 255f/255)), 
                /*KEYWORD4*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0,   0, 255f/255)), 
                /*KEYWORD5*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0,   0, 255f/255)), 
                /*KEYWORD6*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF( 139f/255,   0,   0)), 
                /*PREPROCESSORKEYWORD*/     hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0, 128f/255,   0)),
                /*PREPROCESSOR*/            hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0, 155f/255,  91f/255)),
                /*COMMENT*/                 hwndRenderTarget.CreateSolidColorBrush(new ColorF( 170f/255, 170f/255, 170f/255)), 
                /*OPERATOR*/                hwndRenderTarget.CreateSolidColorBrush(new ColorF( 230f/255,  51f/255,  51f/255)),
                /*BRACKET*/                 hwndRenderTarget.CreateSolidColorBrush(new ColorF( 250f/255,  51f/255,  51f/255)),
                /*NUMBER*/                  hwndRenderTarget.CreateSolidColorBrush(new ColorF( 184f/255, 134f/255,  11f/255)),
                /*STRING*/                  hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0f/255, 100f/255,  0f/255)),
                /*CHAR*/                    hwndRenderTarget.CreateSolidColorBrush(new ColorF(   0f/255, 100f/255,  0f/255))
            };
            this.colorTable = colorTable;
        } 

        internal void HighlightLine(ref VisualLine visualLine)
        {
            this.currentVisualLine = visualLine;

            int opaqueStateIn;
            int ordinalFound;
            int stateNeededOrdinal = visualLine.BeginOrdinal;
            if (stateNeededOrdinal >= this.DirtySyntaxHighlighterStatesBeginOrdinal)
            {
                System.Diagnostics.Debug.Assert(this.DirtySyntaxHighlighterStatesBeginOrdinal != int.MaxValue);
                stateNeededOrdinal = document.PreviousOrdinal(this.DirtySyntaxHighlighterStatesBeginOrdinal);
            }
            bool found = this.syntaxHighlighterStates.Find(stateNeededOrdinal, out ordinalFound, out opaqueStateIn);
            System.Diagnostics.Debug.Assert(found, "Atleast the first ordinal is available in the states collection.");

            if (ordinalFound != visualLine.BeginOrdinal)
            {
                // Need to synthesize forward
                System.Diagnostics.Debug.Assert(ordinalFound < visualLine.BeginOrdinal);
                StringBuilder stringBuilder = new StringBuilder();
                int beginOrdinal = visualLine.BeginOrdinal;
                int currentOrdinal = ordinalFound;
                while (currentOrdinal <= beginOrdinal)
                {
                    stringBuilder.Append(document.CharacterAt(currentOrdinal));
                    currentOrdinal = document.NextOrdinal(currentOrdinal);
#if DEBUG
                    DebugHUD.IterationsSynthesizingSyntaxState++;
#endif
                }
                string text = stringBuilder.ToString();
                opaqueStateIn = this.syntaxHighlighterEngine.SynthesizeStateForward(text, opaqueStateIn);
                this.syntaxHighlighterStates.Insert(beginOrdinal, opaqueStateIn);
                this.DirtySyntaxHighlighterStatesBeginOrdinal = Math.Max(this.DirtySyntaxHighlighterStatesBeginOrdinal, beginOrdinal+1);
            }

            // Call to highlight text, the engine will now use callbacks 
            // to highlight the text passed in.
            int opaqueStateOut;
            syntaxHighlighterEngine.HighlightText(visualLine.Text, opaqueStateIn, out opaqueStateOut);

            if (visualLine.NextOrdinal == Document.UNDEFINED_ORDINAL)
            {
                // The plus one is hacky, but indicates a position just after the last ordinal.
                this.syntaxHighlighterStates.Insert(document.LastOrdinal() + 1, opaqueStateOut);
                this.DirtySyntaxHighlighterStatesBeginOrdinal = int.MaxValue;
            }
            else
            {
                bool insertedDiffentValue = this.syntaxHighlighterStates.Insert(visualLine.NextOrdinal, opaqueStateOut);

                if (this.DirtySyntaxHighlighterStatesBeginOrdinal <= visualLine.NextOrdinal)
                {
                    if (insertedDiffentValue)
                    {
                        int nextOrdinal = document.NextOrdinal(visualLine.NextOrdinal);
                        this.DirtySyntaxHighlighterStatesBeginOrdinal = Math.Max(nextOrdinal, this.DirtySyntaxHighlighterStatesBeginOrdinal);
                    }
                    else
                    {
                        // more dirtiness can be wiped out
                        this.DirtySyntaxHighlighterStatesBeginOrdinal = int.MaxValue;
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

        private int DirtySyntaxHighlighterStatesBeginOrdinal
        {
            get { return dirtySyntaxHighlighterStatesBeginOrdinal; }
            set { dirtySyntaxHighlighterStatesBeginOrdinal = value; System.Diagnostics.Debug.Assert(value >= 0); }
        }

        VisualLine currentVisualLine;
        SyntaxHightlighterEngine syntaxHighlighterEngine;
        SolidColorBrush[] colorTable;
        Document document;
        OrdinalKeyedLinkedList<int> syntaxHighlighterStates;
        /// <summary>
        ///  All ordinals greater than or equal to dirtySyntaxHighlighterStatesBeginOrdinal in the 
        ///  syntaxHighlighterStates have invalid state data.
        /// </summary>
        int dirtySyntaxHighlighterStatesBeginOrdinal;
    }
}
