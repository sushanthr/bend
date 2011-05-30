using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.WindowsAPICodePack.DirectX.DirectWrite;

namespace TextCoreControl
{
    internal class GlyphTable
    {
        internal GlyphTable(TextFormat textFormat)
        {
            this.dwriteFactory = DWriteFactory.CreateFactory(DWriteFactoryType.Shared);
            this.defaultFormat = textFormat;
            charWidths = new Dictionary<char, float>();
        }

        internal float GetCharacterWidth(char letter)
        {
            if (charWidths.ContainsKey(letter))
            {
                return charWidths[letter];
            }
            else
            {
                TextLayout measuringLayout = this.dwriteFactory.CreateTextLayout(new string(letter, 1), defaultFormat, float.MaxValue, float.MaxValue);
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

        public TextFormat DefaultFormat
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
    }
}
