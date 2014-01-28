using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    internal class GlyphTable
    {
        internal GlyphTable(TextFormat textFormat, ShowFormattingService showFormattingService)
        {
            this.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            this.defaultFormat = textFormat;
            charWidths = new Dictionary<char, float>();
            this.showFormattingService = showFormattingService;
        }

        internal float GetCharacterWidth(char letter)
        {
            if (charWidths.ContainsKey(letter))
            {
                return charWidths[letter];
            }
            else
            {
                string letterAsString = new string(letter, 1);

                TextLayout measuringLayout;
                if (Settings.ShowFormatting)
                {
                    string StyledLetterAsString = showFormattingService.PrepareShowFormatting(letterAsString, /*ignoreLastCharacter*/false);
                    measuringLayout = this.dwriteFactory.CreateTextLayout(StyledLetterAsString, defaultFormat, float.MaxValue, float.MaxValue);
                    showFormattingService.ApplyShowFormatting(letterAsString, this.dwriteFactory, measuringLayout);
                }
                else
                {
                    measuringLayout = this.dwriteFactory.CreateTextLayout(letterAsString, defaultFormat, float.MaxValue, float.MaxValue);
                }
                float charWidth = 0;
                foreach (ClusterMetrics cm in measuringLayout.ClusterMetrics) 
                {
                    charWidth = cm.Width;
                    break;
                }
                charWidths.Add(letter, charWidth);
                return charWidth;
            }
        }

        internal TextFormat DefaultFormat
        {
            get
            {
                return this.defaultFormat;
            }
            set
            {
                this.defaultFormat = value;
                charWidths = new Dictionary<char, float>();
            }
        }

        private readonly DWriteFactory dwriteFactory;
        private TextFormat defaultFormat;
        private Dictionary<char, float> charWidths;
        private readonly ShowFormattingService showFormattingService;
    }
}
