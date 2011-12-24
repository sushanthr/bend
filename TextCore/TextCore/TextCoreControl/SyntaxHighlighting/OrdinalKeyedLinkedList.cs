using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextCoreControl.SyntaxHighlighting
{
    internal class OrdinalKeyedLinkedList<T> where T : IComparable
    {
        private class OrdinalKeyedLinkedListNode
        {
            internal OrdinalKeyedLinkedListNode(int ordinal, T value)
            {
                this.ordinal = ordinal;
                this.value = value;
                this.next = null;
                this.previous = null;
            }

            internal int ordinal;
            internal T value;
            internal OrdinalKeyedLinkedListNode next;
            internal OrdinalKeyedLinkedListNode previous;
        }

        internal OrdinalKeyedLinkedList()
        {
            this.first = null;
            this.last = null;
            this.memoizationNode = null;
            this.compressionCount = 0;
        }

        private bool Find(int ordinal, out OrdinalKeyedLinkedListNode startNode)
        {
            startNode = first;
            if (first == null || first.ordinal > ordinal)
                return false;

            int delta = Math.Abs(first.ordinal - ordinal);
            int deltaLast = Math.Abs(last.ordinal - ordinal);
            int deltaMemoization = Math.Abs(memoizationNode.ordinal - ordinal);
            if (deltaLast < delta) startNode = last;
            if (deltaMemoization < delta && deltaMemoization < deltaLast) startNode = memoizationNode;

            if (startNode.ordinal < ordinal)
            {
                // Find forward
                while (startNode.next != null && ordinal >= startNode.next.ordinal)
                {
                    startNode = startNode.next;
#if DEBUG
                    DebugHUD.IterationsSearchingForSyntaxState++;
#endif
                }
            }
            else if (startNode.ordinal > ordinal)
            {
                // Find backward
                while (startNode.previous != null && ordinal < startNode.ordinal)
                {
                    startNode = startNode.previous;
#if DEBUG
                    DebugHUD.IterationsSearchingForSyntaxState++;
#endif
                }
            }
            return true;
        }

        /// <summary>
        ///     Returns the value lesser or equal to the ordinal passed in.
        /// </summary>
        /// <param name="ordinal">Ordinal to search for</param>
        /// <param name="ordinalFound">Ordinal found instead</param>
        /// <param name="value">Value corresponding to the found ordinal</param>
        internal bool Find(int ordinal, out int ordinalFound, out T value)
        {
            OrdinalKeyedLinkedListNode foundNode;
            if (this.Find(ordinal, out foundNode))
            {
                this.memoizationNode = foundNode;
                ordinalFound = foundNode.ordinal;
                value = foundNode.value;
                return true;
            }
            ordinalFound = -1;
            value = default(T);
            return false;
        }

        /// <summary>
        ///     Inserts data into the linked list
        /// </summary>
        /// <param name="ordinal">Ordinal to associate Value with</param>
        /// <param name="value">Value to associate with ordinal</param>
        /// <returns>false if inserted value is the same as the one already in place</returns>
        internal bool Insert(int ordinal, T value)
        {
            OrdinalKeyedLinkedListNode newNode = new OrdinalKeyedLinkedListNode(ordinal, value);
            OrdinalKeyedLinkedListNode foundNode;
            if (this.Find(ordinal, out foundNode))
            {
                if (foundNode.ordinal == ordinal)
                {
                    if (foundNode.value.CompareTo(value) == 0)
                        return false;
                    foundNode.value = value;
                }
                else if (foundNode.value.CompareTo(value) == 0 && 
                    foundNode.previous != null && 
                    foundNode.previous.ordinal != ordinal &&
                    this.compressionCount % 10 != 0)
                {
                    foundNode.ordinal = ordinal;
                    this.compressionCount++;
                }
                else
                {
                    newNode.next = foundNode.next;
                    foundNode.next = newNode;
                    newNode.previous = foundNode;
                    if (newNode.next != null)
                        newNode.next.previous = newNode;
                    if (foundNode == this.last)
                        this.last = newNode;
                }
            }
            else
            {
                this.first = newNode;
                this.last = newNode;
                this.memoizationNode = newNode;
            }

#if DEBUG
            if (DebugHUD.ShowOrdinalKeyedLinkedListContentsInDebugWindow)
            {
                System.Diagnostics.Debug.WriteLine("");
                OrdinalKeyedLinkedListNode tempNode = this.first;
                while (tempNode != null)
                {
                    System.Diagnostics.Debug.Write(tempNode.ordinal + " " + tempNode.value + " (" + (tempNode.previous == null ? "O" : "X") + ") " + " - ");
                    tempNode = tempNode.next;
                }
            }
#endif
            return true;
        }

        /// <summary>
        ///     Delete values in the linked list inclusive of beginOrdinal and endOrdinal
        /// </summary>
        /// <param name="beginOrdinal">beginordinal for delete</param>
        /// <param name="endOrdinal">endordinal for delete</param>
        internal void Delete(int beginOrdinal, int endOrdinal)
        {
            OrdinalKeyedLinkedListNode beginNode;
            if (this.Find(beginOrdinal, out beginNode))
            {
                if (beginNode.next == null) 
                    // Nothing to delete you are the last already
                    return;
                if (beginNode.ordinal < beginOrdinal)
                    beginNode = beginNode.next;

                if (beginNode.ordinal > endOrdinal)
                    return;

                OrdinalKeyedLinkedListNode endNode;
                if (this.Find(endOrdinal, out endNode))
                {
                    if (endNode.ordinal < beginOrdinal)
                        return;

                    // Now we have both begin and end;
                    // Delete inclusive being and end nodes.
                    if (first.ordinal >= beginOrdinal && first.ordinal <= endNode.ordinal)
                        first = endNode.next;
                    if (last.ordinal >= beginOrdinal && last.ordinal <= endNode.ordinal)
                        last = beginNode.previous;
                    if (memoizationNode.ordinal >= beginOrdinal && memoizationNode.ordinal <= endNode.ordinal)
                    {
                        memoizationNode = beginNode.previous;
                        if (memoizationNode == null)
                            memoizationNode = endNode.next;
                    }

                    if (beginNode.previous != null)
                        beginNode.previous.next = endNode.next;
                    if (endNode.next != null)
                        endNode.next.previous = beginNode.previous;
                }
            }

        }

        OrdinalKeyedLinkedListNode first;
        OrdinalKeyedLinkedListNode last;
        OrdinalKeyedLinkedListNode memoizationNode;
        uint compressionCount;
    }
}
