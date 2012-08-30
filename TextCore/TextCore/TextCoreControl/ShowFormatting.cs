using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;
using Microsoft.WindowsAPICodePack.DirectX.Direct2D1;

namespace TextCoreControl
{
    public static class ShowFormatting
    {
        private class ShowFormattingInlineObject : ICustomInlineObject
        {
            public ShowFormattingInlineObject(TextLayout displayText)
            {
                this.displayText = displayText;
            }

            public void Draw(float originX, float originY, bool isSideways, bool isRightToLeft, Brush clientDrawingEffect)
            {
                if (this.displayText.Text.Length == 1)
                {
                    renderTarget.DrawTextLayout(new Point2F(originX + showFormattingPadding, originY), this.displayText, showFormattingBrush);
                }
                else
                {
                    renderTarget.FillRoundedRectangle(new RoundedRect(new RectF(originX + 1.0f, originY, originX + this.Width + 1.0f, originY + displayText.Metrics.Height), showFormattingPadding, showFormattingPadding), showFormattingBrush);
                    renderTarget.DrawTextLayout(new Point2F(originX + showFormattingPadding + 1.0f, originY), this.displayText, ShowFormatting.showFormattingBrushAlt);
                }
            }

            public BreakCondition BreakConditionAfter { get { return BreakCondition.Neutral; } }
            public BreakCondition BreakConditionBefore { get { return BreakCondition.Neutral; } }
            public OverhangMetrics OverhangMetrics { get { return new OverhangMetrics(0,0,0,0); } }
            public InlineObjectMetrics Metrics
            {
                get
                {
                    return new InlineObjectMetrics(this.Width + 2.0f , this.Height, displayText.LineMetrics.First().Baseline, false);
                }
            }
            internal float Width
            {
                get
                {
                    return displayText.Metrics.Width + showFormattingPadding + showFormattingPadding;
                }
            }
            internal float Height
            {
                get
                {
                    return displayText.Metrics.Height + showFormattingPadding + showFormattingPadding;
                }
            }
            TextLayout displayText;
        }
        
        #region ApplyShowFormatting

        internal static void ApplyShowFormatting(string lineText, DWriteFactory dwriteFactory, TextLayout textLayout)
        {
            for (uint i = 0; i < lineText.Length; i++)
            {
                char ch = lineText[(int)i];
                if (IsStandardControlCharacter(ch))
                {
                    // We have to format this character
                    TextLayout formattingTextLayout;
                    lock (formattingTextLayouts)
                    {
                        if (!formattingTextLayouts.TryGetValue(ch, out formattingTextLayout))
                        {
                            formattingTextLayout = dwriteFactory.CreateTextLayout(StandardControlCharacter[(int)ch], Settings.DefaultShowFormattingTextFormat, int.MaxValue, int.MaxValue);
                            formattingTextLayouts.Add(ch, formattingTextLayout);
                        }
                    }
                    textLayout.SetInlineObject(new ShowFormattingInlineObject(formattingTextLayout), new TextRange(i, 1));
                }
            }
        }

        internal static string PrepareShowFormatting(string lineText)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (uint i = 0; i < lineText.Length; i++)
            {
                char ch = lineText[(int)i];
                if (IsStandardControlCharacter(ch))
                {
                    stringBuilder.Append('*');
                }
                else
                {
                    stringBuilder.Append(ch);
                }
            }
            return stringBuilder.ToString();
        }

        private static bool IsStandardControlCharacter(char ch)
        {
            return ch >= (char)0 && ch <= (char)32;
        }

        private static string[] StandardControlCharacter = { 
            "NUL", 
            "SOH", 
            "STX", 
            "ETX", 
            "EOT", 
            "ENQ", 
            "ACK", 
            "BEL", 
            "BS", 
            "TAB", 
            "NL", 
            "VTAB", 
            "NP", 
            "CR", 
            "SO", 
            "SI", 
            "DLE", 
            "DC1", 
            "DC2", 
            "DC3", 
            "DC4", 
            "NAK", 
            "SYN", 
            "ETB", 
            "CAN", 
            "EM", 
            "EOF", 
            "ESC", 
            "FS", 
            "GS", 
            "RS", 
            "US", 
            "."};

        #endregion

        #region Initialization
        static internal void InitDisplayResources(RenderTarget renderTarget)
        {
            ShowFormatting.renderTarget = renderTarget;
            ShowFormatting.showFormattingBrush = renderTarget.CreateSolidColorBrush(Settings.DefaultShowFormattingColor);
            ShowFormatting.showFormattingBrushAlt = renderTarget.CreateSolidColorBrush(Settings.DefaultShowFormattingColorAlt); 
            formattingTextLayouts = new Dictionary<char, TextLayout>();
        }
        static internal void NotifyOfSettingsChanged()
        {
            ShowFormatting.showFormattingBrush = ShowFormatting.renderTarget.CreateSolidColorBrush(Settings.DefaultShowFormattingColor);
            ShowFormatting.showFormattingBrushAlt = renderTarget.CreateSolidColorBrush(Settings.DefaultShowFormattingColorAlt); 
            formattingTextLayouts = new Dictionary<char, TextLayout>();
        }
        #endregion

        private static RenderTarget renderTarget;
        private static Brush showFormattingBrush;
        private static Brush showFormattingBrushAlt;
        private static Dictionary<char, TextLayout> formattingTextLayouts;
        private const float showFormattingPadding = 2.0f;
    }
   
}
