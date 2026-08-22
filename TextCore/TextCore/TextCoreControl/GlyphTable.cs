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
            this.hasNonAsciiCharacters = false;
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

                float charWidth = 0;
                using (TextLayout measuringLayout = Settings.ShowFormatting
                    ? this.dwriteFactory.CreateTextLayout(
                        showFormattingService.PrepareShowFormatting(letterAsString, false),
                        defaultFormat,
                        float.MaxValue,
                        float.MaxValue)
                    : this.dwriteFactory.CreateTextLayout(letterAsString, defaultFormat, float.MaxValue, float.MaxValue))
                {
                    if (Settings.ShowFormatting)
                        showFormattingService.ApplyShowFormatting(letterAsString, this.dwriteFactory, measuringLayout);
                    foreach (ClusterMetrics cm in measuringLayout.ClusterMetrics)
                    {
                        charWidth = cm.Width;
                        break;
                    }
                }
                charWidths.Add(letter, charWidth);
                if (!this.hasNonAsciiCharacters)
                { 
                    this.hasNonAsciiCharacters = (Encoding.UTF8.GetByteCount(letterAsString) > 1);
                }
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
        
        internal bool HasNonAsciiCharacters {
            get { return this.hasNonAsciiCharacters; }
        }

        private readonly DWriteFactory dwriteFactory;
        private TextFormat defaultFormat;
        private Dictionary<char, float> charWidths;
        private readonly ShowFormattingService showFormattingService;
        private bool hasNonAsciiCharacters;
    }
}
