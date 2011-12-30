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
            internal OrdinalKeyedLinkedListNode(int ordinalDelta, T value)
            {
                this.ordinalDelta = ordinalDelta;
                this.value = value;
                this.next = null;
                this.previous = null;
            }

            /// <summary>
            ///     OrdinalDelta - value to add from the pervious node ordinal to get the ordinal for this node.
            /// </summary>
            internal int ordinalDelta;
            internal T value;
            internal OrdinalKeyedLinkedListNode next;
            internal OrdinalKeyedLinkedListNode previous;
        }

        internal OrdinalKeyedLinkedList()
        {
            this.first = null;
            this.firstOrdinal = Document.UNDEFINED_ORDINAL;
            this.last = null;
            this.lastOrdinal = Document.UNDEFINED_ORDINAL;
            this.memoizationNode = null;
            this.memoizationOrdinal = Document.UNDEFINED_ORDINAL;
            this.compressionCount = 0;
        }

        internal void NotifyOfOrdinalShift(Document document, int beginOrdinal, int shift)
        {
            if (shift < 0)
                this.Delete(beginOrdinal + 1 + shift, beginOrdinal);

            OrdinalKeyedLinkedListNode foundNode;
            int foundOrdinal;
            if (this.Find(beginOrdinal, out foundNode, out foundOrdinal))
            {
                this.memoizationNode = foundNode;
                this.memoizationOrdinal = foundOrdinal;

                OrdinalKeyedLinkedListNode adjustNode = foundNode.next;
                if (adjustNode != null)
                    adjustNode.ordinalDelta += shift;
            }
            
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.firstOrdinal);
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.lastOrdinal);
            Document.AdjustOrdinalForShift(beginOrdinal, shift, ref this.memoizationOrdinal);
        }

        private bool Find(int ordinal, out OrdinalKeyedLinkedListNode foundNode, out int foundOrdinal)
        {
            foundNode = first;
            foundOrdinal = firstOrdinal;
            if (first == null || firstOrdinal > ordinal)
                return false;

            int delta = Math.Abs(firstOrdinal - ordinal);
            int deltaLast = Math.Abs(lastOrdinal - ordinal);
            int deltaMemoization = Math.Abs(memoizationOrdinal - ordinal);
            if (deltaLast < delta)
            {
                foundNode = last;
                foundOrdinal = lastOrdinal;
            }
            if (deltaMemoization < delta && deltaMemoization < deltaLast)
            {
                foundNode = memoizationNode;
                foundOrdinal = memoizationOrdinal;
            }

            if (foundOrdinal < ordinal)
            {
                // Find forward
                while (foundNode.next != null && ordinal >= (foundNode.next.ordinalDelta + foundOrdinal))
                {
                    foundNode = foundNode.next;
                    foundOrdinal += foundNode.ordinalDelta;
#if DEBUG
                    DebugHUD.IterationsSearchingForSyntaxState++;
#endif
                }
            }
            else if (foundOrdinal > ordinal)
            {
                // Find backward
                while (foundNode.previous != null && ordinal < foundOrdinal)
                {
                    foundOrdinal -= foundNode.ordinalDelta;
                    foundNode = foundNode.previous;
#if DEBUG
                    DebugHUD.IterationsSearchingForSyntaxState++;
#endif
                }
            }
            return true;
        }

        /// <summary>
        ///     Finds the value for a ordinal lesser or equal to the ordinal passed in.
        ///     Returns true for a sucessful find, returns false otherwise.
        /// </summary>
        /// <param name="ordinal">Ordinal to search for</param>
        /// <param name="foundOrdinal">Ordinal found instead</param>
        /// <param name="value">Value corresponding to the found ordinal</param>
        internal bool Find(int ordinal, out int foundOrdinal, out T value)
        {
            OrdinalKeyedLinkedListNode foundNode;
            if (this.Find(ordinal, out foundNode, out foundOrdinal))
            {
                this.memoizationNode = foundNode;
                this.memoizationOrdinal = foundOrdinal;
                value = foundNode.value;
                return true;
            }
            foundOrdinal = -1;
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
            this.compressionCount++;
            OrdinalKeyedLinkedListNode foundNode;
            int foundOrdinal;
            if (this.Find(ordinal, out foundNode, out foundOrdinal))
            {
                if (foundOrdinal == ordinal)
                {
                    if (foundNode.value.CompareTo(value) == 0)
                        return false;
                    foundNode.value = value;
                }
                else
                {
                    int previousNodeOrdinal = foundOrdinal - foundNode.ordinalDelta;
                    if (foundNode.value.CompareTo(value) == 0 &&
                        foundNode.previous != null &&
                        previousNodeOrdinal != ordinal &&
                        this.compressionCount % 10 != 0)
                    {
                        int oldOrdinalDelta = foundNode.ordinalDelta;
                        foundNode.ordinalDelta = ordinal - previousNodeOrdinal;
                        if (foundNode.next != null) foundNode.next.ordinalDelta -= (foundNode.ordinalDelta - oldOrdinalDelta);
                        if (this.first == foundNode) this.firstOrdinal = ordinal;
                        if (this.last == foundNode) this.lastOrdinal = ordinal;
                        if (this.memoizationNode == foundNode) this.memoizationOrdinal = ordinal;
                    }
                    else
                    {
                        OrdinalKeyedLinkedListNode newNode = new OrdinalKeyedLinkedListNode(ordinal - foundOrdinal, value);
                        newNode.next = foundNode.next;
                        foundNode.next = newNode;
                        newNode.previous = foundNode;
                        if (newNode.next != null)
                        {
                            newNode.next.previous = newNode;
                            newNode.next.ordinalDelta = (foundOrdinal + newNode.next.ordinalDelta - ordinal);
                        }
                        if (foundNode == this.last)
                        {
                            this.last = newNode;
                            this.lastOrdinal = ordinal;
                        }
                    }
                }
            }
            else
            {
                OrdinalKeyedLinkedListNode newNode = new OrdinalKeyedLinkedListNode(ordinal, value);
                this.first = newNode;
                this.firstOrdinal = ordinal;
                this.last = newNode;
                this.lastOrdinal = ordinal;
                this.memoizationNode = newNode;
                this.memoizationOrdinal = ordinal;
            }

#if DEBUG
            if (DebugHUD.ShowOrdinalKeyedLinkedListContentsInDebugWindow)
            {
                OrdinalKeyedLinkedListNode tempNode = this.first;
                int ordinalSum = 0;
                while (tempNode != null)
                {
                    ordinalSum += tempNode.ordinalDelta;
                    tempNode = tempNode.next;
                }
                if (ordinalSum != this.lastOrdinal)
                {
                    System.Diagnostics.Debug.Assert(ordinalSum == this.lastOrdinal);
                    this.Delete(ordinal, ordinal);
                }

                this.Dump();
            }
#endif
            return true;
        }

        internal void Dump()
        {
            System.Diagnostics.Debug.WriteLine("");
            OrdinalKeyedLinkedListNode tempNode = this.first;
            int ordinalSum = 0;
            while (tempNode != null)
            {
                ordinalSum += tempNode.ordinalDelta;
                System.Diagnostics.Debug.Write(ordinalSum + " (" + tempNode.ordinalDelta + ") " + tempNode.value + " => ");
                tempNode = tempNode.next;
            }
        }

        /// <summary>
        ///     Delete values in the linked list inclusive of beginOrdinal and endOrdinal
        /// </summary>
        /// <param name="beginOrdinal">beginordinal for delete</param>
        /// <param name="endOrdinal">endordinal for delete</param>
        internal void Delete(int beginOrdinal, int endOrdinal)
        {
            OrdinalKeyedLinkedListNode beginNode;
            int beginOrdinalFound;
            if (this.Find(beginOrdinal, out beginNode, out beginOrdinalFound))
            {
                if (beginNode.next == null) 
                    // Nothing to delete you are the last already
                    return;
                if (beginOrdinalFound < beginOrdinal)
                {
                    beginNode = beginNode.next;
                    beginOrdinalFound += beginNode.ordinalDelta;
                }

                if (beginOrdinalFound > endOrdinal)
                    return;

                OrdinalKeyedLinkedListNode endNode;
                int endOrdinalFound;
                if (this.Find(endOrdinal, out endNode, out endOrdinalFound))
                {
                    if (endOrdinalFound < beginOrdinal)
                        return;

                    if (lastOrdinal >= beginOrdinal && lastOrdinal <= endOrdinal)
                    {
                        last = beginNode.previous;
                        lastOrdinal = beginOrdinalFound - beginNode.ordinalDelta;
                    }

                    if (memoizationOrdinal >= beginOrdinal && memoizationOrdinal <= endOrdinal)
                    {
                        if (beginNode.previous == null)
                        {
                            memoizationNode = endNode.next;
                            if (memoizationNode != null)
                                memoizationOrdinal = endOrdinalFound + memoizationNode.ordinalDelta;
                            else
                                memoizationOrdinal = 0;
                        }
                        else
                        {
                            memoizationNode = beginNode.previous;
                            memoizationOrdinal = beginOrdinalFound - beginNode.ordinalDelta;
                        }
                    }

                    if (beginNode.previous != null)
                        beginNode.previous.next = endNode.next;
                    if (endNode.next != null)
                    {
                        endNode.next.previous = beginNode.previous;
                        int deleteOrdinalDelta = endOrdinalFound - (beginOrdinalFound - beginNode.ordinalDelta);
                        endNode.next.ordinalDelta += deleteOrdinalDelta;
                    }

                    // Now we have both begin and end;
                    // Delete inclusive begin and end nodes.
                    if (firstOrdinal >= beginOrdinal && firstOrdinal <= endOrdinal)
                    {
                        first = endNode.next;
                        if (first != null)
                        {
                            firstOrdinal = endNode.next.ordinalDelta;
                            endNode.next.ordinalDelta = 0;
                        }
                    }
                }
            }

        }

        OrdinalKeyedLinkedListNode first;
        /// <summary>
        ///     Ordinal value for the first node.
        /// </summary>
        int firstOrdinal;
        
        OrdinalKeyedLinkedListNode last;
        /// <summary>
        ///     Ordinal value for the last node.
        /// </summary>
        int lastOrdinal;
        
        OrdinalKeyedLinkedListNode memoizationNode;
        /// <summary>
        ///     Ordinal value for the memoization node.
        /// </summary>
        int memoizationOrdinal;

        uint compressionCount;
    }
}
