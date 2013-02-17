using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextCoreControl
{
    internal class UndoRedoManager
    {
        internal class Action
        {
            public Action(bool isInTransaction)
            {
                this.isInTransaction = isInTransaction;
            }

            /// <summary>
            ///     Undoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be undone
            /// </returns>
            internal virtual bool UndoAction(Document document, int shiftCorrectedOrdinal)
            {
                return false;
            }

            /// <summary>
            ///     Redoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be redone
            /// </returns>
            internal virtual bool RedoAction(Document document, int shiftCorrectedOrdinal)
            {
                return false;
            }

            internal virtual int Ordinal 
            { 
                get { return Document.BEFOREBEGIN_ORDINAL; } 
            }

            protected bool isInTransaction;
        }

        // TransactionBarrier overrides nothing only provides a mark on the 
        // action stack, and response to undo / redo with appropriate sucess.
        internal class TransactionBarrier : Action
        {
            public TransactionBarrier(bool isTransactionBegin) : base(false) 
            {
                this.isTransactionBegin = isTransactionBegin;
            }

            /// <summary>
            ///     Undoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be undone
            /// </returns>
            internal override bool UndoAction(Document document, int shiftCorrectedOrdinal)
            {
                return isTransactionBegin;
            }

            /// <summary>
            ///     Redoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be redone
            /// </returns>
            internal override bool RedoAction(Document document, int shiftCorrectedOrdinal)
            {
                return !isTransactionBegin;
            }

            private bool isTransactionBegin;
        }

        // Savefile Action overrides nothing only provides a mark on the 
        // action stack, which indicates that file was saved at this point.
        internal class SaveFileAction : Action 
        {
            public SaveFileAction(bool isInTransaction) : base (isInTransaction) {}
        }

        internal class InsertTextAction : Action
        {
            public InsertTextAction(int ordinal, string text, bool isInTransaction) : base (isInTransaction)
            {
                this.ordinal = ordinal;
                this.text = text;
            }

            /// <summary>
            ///     Undoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be undone
            /// </returns>
            internal override bool UndoAction(Document document, int shiftCorrectedOrdinal)
            {
                document.DeleteAt(shiftCorrectedOrdinal, text.Length);
                return !this.isInTransaction;
            }

            /// <summary>
            ///     Redoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be redone
            /// </returns>
            internal override bool RedoAction(Document document, int shiftCorrectedOrdinal)
            {
                document.InsertAt(shiftCorrectedOrdinal, text);
                return !this.isInTransaction;
            }

            internal override int Ordinal { get { return this.ordinal; } }

            /// <summary>
            ///     Merges with another action if possible
            /// </summary>
            /// <param name="otherAction">other insert action to merge with</param>
            /// <returns>False if not mergeable</returns>
            internal bool Merge(Document document, int shiftCorrectedOrdinal, InsertTextAction otherAction)
            {
                if (document.NextOrdinal(shiftCorrectedOrdinal, (uint)text.Length) == otherAction.ordinal)
                {
                    // Consecutive text entries which is good. Check for space or control characters
                    char lastChar = text[text.Length - 1];
                    char nextFirstChar = otherAction.text[otherAction.text.Length - 1];

                    if (char.IsSeparator(lastChar) || char.IsControl(lastChar) || char.IsSeparator(nextFirstChar) || char.IsControl(nextFirstChar))
                        return false;

                    // Mergeable
                    this.text += otherAction.text;
                    return true;
                }
                return false;
            }

            private int ordinal;
            private string text;
        }

        internal class DeleteTextAction : Action
        {

            public DeleteTextAction(int ordinal, string text, bool isInTransaction) : base(isInTransaction)
            {
                this.ordinal = ordinal;
                this.text = text;
            }

            /// <summary>
            ///     Undoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be undone
            /// </returns>
            internal override bool UndoAction(Document document, int shiftCorrectedOrdinal)
            {
                document.InsertAt(shiftCorrectedOrdinal, text);
                return !this.isInTransaction;
            }

            /// <summary>
            ///     Redoes the action represented by this object
            /// </summary>
            /// <returns>
            ///     Returns true if this object represents something that can be redone
            /// </returns>
            internal override bool RedoAction(Document document, int shiftCorrectedOrdinal)
            {
                document.DeleteAt(shiftCorrectedOrdinal, text.Length);
                return !this.isInTransaction;
            }

            internal override int Ordinal { get { return this.ordinal; } }

            private int ordinal;
            private string text;
        }

        private class OrdinalShiftAction : Action
        {
            internal OrdinalShiftAction(int beginOrdinal, int shift, bool isInTransaction) : base (isInTransaction)
            {
                this.beginOrdinal = beginOrdinal;
                this.shift = shift;
            }

            internal int BeginOrdinal
            {
                get { return this.beginOrdinal; }
            }

            internal int Shift
            {
                get { return this.shift; }
            }

            private int beginOrdinal;
            private int shift;
        }

        internal UndoRedoManager(Document document)
        {
            document.OrdinalShift += new Document.OrdinalShiftEventHandler(Document_OrdinalShift);
            document.ContentChange += new Document.ContentChangeEventHandler(document_ContentChange);
            this.document = document;
            this.actionList = new LinkedList<Action>();
            this.currentActionNode = this.actionList.First;
            this.currentActionNodeIsAfterEnd = false;
            this.isPerformingUndoRedo = false;
            this.maxInterestingOrdinal = Document.BEFOREBEGIN_ORDINAL;
        }

        void document_ContentChange(int beginOrdinal, int endOrdinal, string content)
        {
            if (content != null)
            { 
                System.Diagnostics.Debug.Assert(beginOrdinal != Document.UNDEFINED_ORDINAL);
                if (beginOrdinal < endOrdinal)
                {
                    // Content insertion
                    this.AddAction(new UndoRedoManager.InsertTextAction(beginOrdinal, content, this.isInTransaction));
                }
                else
                {
                    // Content deletion
                    this.AddAction(new UndoRedoManager.DeleteTextAction(beginOrdinal, content, this.isInTransaction));
                }
            }
        }

        internal void AddAction(Action action)
        {
            if (!isPerformingUndoRedo)
            {
                if (this.currentActionNode == null) this.MoveCurrentActionNodeForward();

                if (this.currentActionNode != this.actionList.First)
                {
                    // We have already undone a bit and we are recording a new action.
                    // Delete all entries above current in the actionList
                    LinkedListNode<Action> tempLinkedListNode = this.actionList.First;
                    while (tempLinkedListNode != this.currentActionNode)
                    {
                        if (!(tempLinkedListNode.Value is OrdinalShiftAction))
                        {
                            LinkedListNode<Action> tempDeleteLinkedListNode = tempLinkedListNode;
                            tempLinkedListNode = tempLinkedListNode.Next;

                            // This is a regular action that must be deleted.
                            actionList.Remove(tempDeleteLinkedListNode);
                        }
                        else
                        {
                            tempLinkedListNode = tempLinkedListNode.Next;
                        }
                    }
                }

                bool actionMerged = false;
                if (action is InsertTextAction)
                {
                    LinkedListNode<Action> tempLinkedListNode = this.actionList.First;
                    while (tempLinkedListNode != null)
                    {
                        if (tempLinkedListNode.Value is InsertTextAction)
                        {
                            // We have two insert text action right next to each other.
                            InsertTextAction oldInsertTextAction = (InsertTextAction)tempLinkedListNode.Value;
                            InsertTextAction newInsertTextAction = (InsertTextAction)action;
                            actionMerged = oldInsertTextAction.Merge(this.document, this.ComputeShiftCorrectedOrdinal(tempLinkedListNode), newInsertTextAction);
                            break;
                        }
                        else if (!(tempLinkedListNode.Value is OrdinalShiftAction || tempLinkedListNode.Value is SaveFileAction))
                        {
                            break;
                        }
                        tempLinkedListNode = tempLinkedListNode.Next;
                    }
                }

                if (!actionMerged)
                {
                    this.actionList.AddFirst(action);
                }

                this.currentActionNode = this.actionList.First;
                this.maxInterestingOrdinal = System.Math.Max(action.Ordinal, this.maxInterestingOrdinal);
            }
        }

        internal void Undo()
        {
            isPerformingUndoRedo = true;

            if (this.currentActionNode == null) this.MoveCurrentActionNodeForward();

            bool success = false;
            while (this.currentActionNode != null && !success)
            {
                // Compute shift corrected ordinal
                int shiftCorrectedOrdinal = this.ComputeShiftCorrectedOrdinal(this.currentActionNode);

                // Peform the undo
                success = this.currentActionNode.Value.UndoAction(this.document, shiftCorrectedOrdinal);

                this.MoveCurrentActionNodeForward();
            }

            isPerformingUndoRedo = false;
        }

        internal void Redo()
        {
            isPerformingUndoRedo = true;

            this.MoveCurrentActionNodeBackward();
            while (this.currentActionNode != null)
            {
                // Compute shift corrected ordinal
                int shiftCorrectedOrdinal = this.ComputeShiftCorrectedOrdinal(this.currentActionNode);

                // Peform the undo
                if (this.currentActionNode.Value.RedoAction(this.document, shiftCorrectedOrdinal))
                {
                    // Dont move the currentActionNode past this node.
                    break;
                }

                this.MoveCurrentActionNodeBackward();
            }

            isPerformingUndoRedo = false;
        }

        private int ComputeShiftCorrectedOrdinal(LinkedListNode<Action> action)
        {
            System.Diagnostics.Debug.Assert(action != null);

            int ordinal = action.Value.Ordinal;
            if (ordinal != Document.BEFOREBEGIN_ORDINAL || ordinal != Document.UNDEFINED_ORDINAL)
            {
                while (action != null)
                {
                    if (action.Value is OrdinalShiftAction)
                    {
                        OrdinalShiftAction ordinalShiftAction = (OrdinalShiftAction)action.Value;
                        if (ordinal > ordinalShiftAction.BeginOrdinal)
                        {
                            ordinal += ordinalShiftAction.Shift;
                        }
                    }

                    action = action.Previous;
                }
            }

            return ordinal;
        }

        /// <summary>
        ///     Handler that keeps track of the shifting ordinals in the document.
        /// </summary>
        internal void Document_OrdinalShift(Document document, int beginOrdinal, int shift)
        {
            if (currentActionNode != null)
            {
                this.maxInterestingOrdinal = Document.BEFOREBEGIN_ORDINAL;
            }

            if (this.maxInterestingOrdinal > beginOrdinal && this.maxInterestingOrdinal != Document.UNDEFINED_ORDINAL)
            {
                OrdinalShiftAction ordinalShiftAction = new OrdinalShiftAction(beginOrdinal, shift, this.isInTransaction);
                this.actionList.AddFirst(ordinalShiftAction);
                Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.maxInterestingOrdinal);
            }
        }

        private void MoveCurrentActionNodeForward()
        {
            if (this.currentActionNode == null)
            {
                if (!currentActionNodeIsAfterEnd)
                {
                    this.currentActionNode = this.actionList.First;
                }
            }
            else
            {
                this.currentActionNode = this.currentActionNode.Next;
            }
            if (this.currentActionNode == null) currentActionNodeIsAfterEnd = true;
        }

        private void MoveCurrentActionNodeBackward()
        {
            if (this.currentActionNode == null)
            {
                if (currentActionNodeIsAfterEnd)
                {
                    this.currentActionNode = this.actionList.Last;
                }
            }
            else
            {
                this.currentActionNode = this.currentActionNode.Previous;
            }
            if (this.currentActionNode == null) currentActionNodeIsAfterEnd = false;
        }

        internal void BeginTransaction()
        {
            System.Diagnostics.Debug.Assert(!this.isInTransaction);
            this.AddAction(new UndoRedoManager.TransactionBarrier(/*isTransactionBegin*/true));
            this.isInTransaction = true;
        }

        internal void EndTransaction()
        {
            System.Diagnostics.Debug.Assert(this.isInTransaction);
            this.isInTransaction = false;
            this.AddAction(new UndoRedoManager.TransactionBarrier(/*isTransactionBegin*/false));
        }

        private LinkedList<Action> actionList;

        private LinkedListNode<Action> currentActionNode;
        private bool currentActionNodeIsAfterEnd;

        private Document document;
        private bool isPerformingUndoRedo;
        private int maxInterestingOrdinal;

        private bool isInTransaction;
    }
}
