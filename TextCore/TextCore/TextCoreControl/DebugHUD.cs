using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextCoreControl.SyntaxHighlighting;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    /// <summary>
    ///     This is a class that draws a debug HUD on top of the text control.
    ///
    ///     Showing key statics.
    ///     VL - Number of visual lines in memory.
    ///     CL - Numbeer of content lines in the document.
    ///     
    ///     Since this is a debug feature class, its like a global sink where all kinds
    ///     of objects plug into. It is like the other half of the settings class, which
    ///     is read everywhere. This class is written to everywhere.
    /// </summary>
    static class DebugHUD
    {
        static DebugHUD()
        {
            DebugHUD.DisplayManager = null;
            DebugHUD.ContentLineManager = null;
            DebugHUD.LanguageDetector = null;
        }

        static internal void Draw(RenderTarget renderTarget, SizeF scrollOffset)
        {
            if (Settings.ShowDebugHUD)
            {
                string output = " ";
                if (DisplayManager != null)
                {
                    output += "VL " + DisplayManager.VisualLineCount.ToString() + " / ";
                }

                if (ContentLineManager != null)
                {
                    output += "CL " + ContentLineManager.MaxContentLines.ToString() + " / ";
                }

                if (LanguageDetector != null)
                {
                    output += LanguageDetector.SyntaxDefinitionFile;
                }

                if (output != "")
                {
                    RectF rect = new RectF(2 + scrollOffset.Width, 0 + scrollOffset.Height, 300 + scrollOffset.Width, 20 + scrollOffset.Height);
                    renderTarget.FillRectangle(rect, renderTarget.CreateSolidColorBrush(new ColorF(0, 0, 0, 0.5f)));
                    renderTarget.DrawText(output, Settings.DefaultTextFormat, rect, renderTarget.CreateSolidColorBrush(new ColorF(0, 128, 0, 0.5f)));
                }
            }
        }

        static internal DisplayManager DisplayManager;
        static internal ContentLineManager ContentLineManager;
        static internal LanguageDetector LanguageDetector;
    }
}
