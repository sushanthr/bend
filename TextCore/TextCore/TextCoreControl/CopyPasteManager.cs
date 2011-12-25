using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextCoreControl
{
    public class CopyPasteManager
    {
        public CopyPasteManager()
        {
            this.clipBoardRing = new LinkedList<string>();
            this.ringIndex = 0;
        }

        public void Cut(TextEditor textCoreControl)
        {
            this.Copy(textCoreControl);

            // Delete the text
            int selectionBeginOrdinal;
            string cutString = textCoreControl.DisplayManager.GetSelectedText(out selectionBeginOrdinal);
            if (cutString.Length > 0)
            {
                textCoreControl.Document.DeleteAt(selectionBeginOrdinal, cutString.Length);
            }
        }

        public void Copy(TextEditor textCoreControl)
        {
            this.SyncClipBoardRing();
            int selectionBeginOrdinal;
            string copyString = textCoreControl.DisplayManager.GetSelectedText(out selectionBeginOrdinal);
            if (copyString.Length > 0)
            {
                this.PrivateAddToClipBoardRing(copyString);
                System.Windows.Clipboard.SetText(copyString);
            }
        }

        public void Paste(TextEditor textCoreControl, int item = 0)
        {
            this.SyncClipBoardRing();

            if (this.clipBoardRing.Count != 0)
            {
                // Remove current selected text
                int selectionBeginOrdinal;
                string copyString = textCoreControl.DisplayManager.GetSelectedText(out selectionBeginOrdinal);
                if (copyString.Length > 0)
                {
                    textCoreControl.Document.DeleteAt(selectionBeginOrdinal, copyString.Length);
                }

                item = item % this.clipBoardRing.Count;
                string pasteText = this.clipBoardRing.ElementAt(item);
                textCoreControl.Document.InsertAt(textCoreControl.DisplayManager.CaretOrdinal, pasteText);
                this.ringIndex = item;
            }
        }

        public void PasteNextRingItem(TextEditor textCoreControl)
        {
            if (this.clipBoardRing.Count != 0)
            {
                System.Diagnostics.Debug.Assert(this.ringIndex >= 0 && this.ringIndex < this.clipBoardRing.Count);

                // Remove current selected text
                int selectionBeginOrdinal;
                string copyString = textCoreControl.DisplayManager.GetSelectedText(out selectionBeginOrdinal);
                if (copyString.Length > 0)
                {
                    textCoreControl.Document.DeleteAt(selectionBeginOrdinal, copyString.Length);
                }

                string pasteText = this.clipBoardRing.ElementAt(this.ringIndex);
                System.Diagnostics.Debug.Assert(pasteText.Length > 0);

                // Insert that text
                textCoreControl.Document.InsertAt(textCoreControl.DisplayManager.CaretOrdinal, pasteText);

                // Select it, so that subsequent PastNextRingItem can delete it.
                int selectionBegin = textCoreControl.Document.PreviousOrdinal(textCoreControl.DisplayManager.CaretOrdinal, (uint)pasteText.Length);
                textCoreControl.DisplayManager.SelectRange(selectionBegin, textCoreControl.DisplayManager.CaretOrdinal);

                this.ringIndex++;
                this.ringIndex = this.ringIndex % this.clipBoardRing.Count;
            }
        }

        public LinkedList<string>.Enumerator GetClipBoardRingEnumerator()
        {
            this.SyncClipBoardRing();
            return clipBoardRing.GetEnumerator();
        }

        /// <summary>
        ///  Ensures that the clipBoardRing has the system clipboard entry included as
        ///  the first item.
        /// </summary>
        private void SyncClipBoardRing()
        {
            string systemClipBoardString = System.Windows.Clipboard.GetText();
            this.PrivateAddToClipBoardRing(systemClipBoardString);
        }

        private void PrivateAddToClipBoardRing(string text)
        {
            if (text != null && text.Length > 0)
            {
                if (this.clipBoardRing.Count == 0 || this.clipBoardRing.First.Value != text)
                {
                    this.clipBoardRing.AddFirst(text);
                    if (this.clipBoardRing.Count >= Settings.CopyPaste_ClipRing_Max_Entries)
                    {
                        this.clipBoardRing.RemoveLast();
                    }
                    this.ringIndex = 0;
                }
            }
        }

        LinkedList<String> clipBoardRing;
        int ringIndex;
    }
}
