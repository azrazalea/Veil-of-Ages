using System;
using System.Collections.Generic;

namespace VeilOfAges.Core.Lib
{
    public class ReorderableQueue<T>
    {
        private LinkedList<T> queue = new();

        // Add item to end (like push/enqueue)
        public void Enqueue(T item)
        {
            queue.AddLast(item);
        }

        public LinkedListNode<T>? First()
        {
            return queue.First;
        }

        // Remove item from front (like pop/dequeue)
        public T Dequeue()
        {
            LinkedListNode<T>? node = queue.First;
            if (queue.Count == 0 || node == null)
                throw new InvalidOperationException("Queue is empty");

            queue.RemoveFirst();
            return node.Value;
        }

        // Get reference to a specific node for later manipulation
        public LinkedListNode<T>? FindCommand(Predicate<T> match)
        {
            LinkedListNode<T>? current = queue.First;
            while (current != null)
            {
                if (match(current.Value))
                    return current;
                current = current.Next;
            }
            return null;
        }

        // Remove a specific node
        public void Remove(LinkedListNode<T> node)
        {
            queue.Remove(node);
        }

        // Move a node to a new position (e.g., to prioritize it)
        public void MoveToBefore(LinkedListNode<T> node, LinkedListNode<T> beforeNode)
        {
            queue.Remove(node);
            queue.AddBefore(beforeNode, node.Value);
        }

        public void MoveToAfter(LinkedListNode<T> node, LinkedListNode<T> afterNode)
        {
            queue.Remove(node);
            queue.AddAfter(afterNode, node);
        }

        public void MoveToFront(LinkedListNode<T> node)
        {
            queue.Remove(node);
            queue.AddFirst(node.Value);
        }

        public void MoveToLast(LinkedListNode<T> node)
        {
            queue.Remove(node);
            queue.AddLast(node.Value);
        }

        public int Count => queue.Count;
        public bool IsEmpty => queue.Count == 0;
    }
}
