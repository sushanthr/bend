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
        internal SyntaxHighlighterService(string fullSyntaxFilePath)
        {
            syntaxHighlighterEngine = new SyntaxHightlighterEngine(fullSyntaxFilePath);
            syntaxHighlighterEngine.highlightRange += new SyntaxHightlighterEngine.HighlightRange(syntaxHighlighterEngine_highlightRange);
            colorTable = null;
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
                /*COMMENT*/                 hwndRenderTarget.CreateSolidColorBrush(new ColorF( 170f/255, 170f/255, 170f/255))
            };
            this.colorTable = colorTable;
        } 

        internal void HighlightLine(ref VisualLine visualLine)
        {
            this.currentVisualLine = visualLine;

            // Call to highlight text, the engine will now use callbacks 
            // to highlight the text passed in.
            syntaxHighlighterEngine.HighlightText(visualLine.Text);
        }

        void syntaxHighlighterEngine_highlightRange(uint beginOffset, uint length, SyntaxHightlighterEngine.HighlightStyle style)
        {
            if (colorTable != null)
            {
                currentVisualLine.SetDrawingEffect(this.colorTable[(int)style], beginOffset, length);
            }
        }

        VisualLine currentVisualLine;
        SyntaxHightlighterEngine syntaxHighlighterEngine;
        SolidColorBrush[] colorTable;
    }
}
