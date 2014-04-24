﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequences
{
    /// <summary>
    /// Represents an immutable lazy sequence of elements.
    /// Elements are only evaluated when they're needed, and <see cref="Sequence{T}"/> employs memoization to store the computed values and avoid re-evaluation.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    public class Sequence<T> : ISequence<T>
    {
        private readonly T _head;
        private readonly Lazy<ISequence<T>> _tail;
        private int _count = int.MinValue;
        private bool _hasDefiniteSize = false;

        /// <summary>
        /// Tests whether the sequence is empty.
        /// </summary>
        public virtual bool IsEmpty
        {
            get { return false; }
        }

        /// <summary>
        /// Returns the first element of this sequence.
        /// </summary>
        public virtual T Head
        {
            get { return _head; }
        }

        /// <summary>
        /// Returns a sequence of all elements except the first.
        /// </summary>
        public virtual ISequence<T> Tail
        {
            get { return _tail.Value; }
        }

        /// <summary>
        /// Returns the length of this sequence.
        /// If this sequence represents an infinite series, this will never return!
        /// </summary>
        public int Count
        {
            get
            {
                if (_count < 0)
                    _count = this.Count();

                return _count;
            }
        }

        /// <summary>
        /// Tests whether this sequence is known to have a finite size.
        /// </summary>
        public bool HasDefiniteSize
        {
            get
            {
                if (!_hasDefiniteSize)
                {
                    ISequence<T> left = this;
                    while (!left.IsEmpty && left.IsTailDefined)
                        left = left.Tail;

                    _hasDefiniteSize = left.IsEmpty;
                }

                return _hasDefiniteSize;
            }
        }

        /// <summary>
        /// Checks whether this sequence's tail has been evaluated.
        /// </summary>
        public bool IsTailDefined
        {
            get { return _tail.IsValueCreated; }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Sequence{T}"/>.
        /// </summary>
        /// <param name="head">The first element of the sequence.</param>
        /// <param name="tail">A delegate that will be used to realize the sequence's tail when needed.</param>
        public Sequence(T head, Func<ISequence<T>> tail)
            : this(head, new Lazy<ISequence<T>>(tail))
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Sequence{T}"/>.
        /// </summary>
        /// <param name="head">The first element of the sequence.</param>
        /// <param name="tail">The tail of the sequence.</param>
        protected Sequence(T head, Lazy<ISequence<T>> tail)
        {
            _head = head;
            _tail = tail;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Sequence{T}"/>.
        /// </summary>
        /// <returns>An <see cref="IEnumerator{T}"/> for the sequence.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            //we use an iterative proccess, instead of recursively calling Tail.GetEnumerator
            //to avoid a stack overflow exception
            ISequence<T> sequence = this;

            while (!sequence.IsEmpty)
            {
                yield return sequence.Head;
                sequence = sequence.Tail;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Forces evaluation of the whole sequence and returns it.
        /// If this sequence represents an infinite series, the method will never return!
        /// </summary>
        /// <returns></returns>
        public ISequence<T> Force()
        {
            foreach (var elem in this)
            {
            }
            return this;
        }

        /// <summary>
        /// Returns a copy of this sequence with the given element appended.
        /// </summary>
        /// <param name="elem">The element to append to this sequence.</param>
        /// <returns>A copy of this sequence with the given element appended.</returns>
        public ISequence<T> Append(T elem)
        {
            return Concat(() => new Sequence<T>(elem, Sequence.Empty<T>));
        }

        /// <summary>
        /// Returns a copy of this sequence with the given element prepended.
        /// </summary>
        /// <param name="elem">The element to prepend.</param>
        /// <returns>A copy of this sequence with the given element prepended.</returns>
        public ISequence<T> Prepend(T elem)
        {
            return new Sequence<T>(elem, () => this);
        }

        /// <summary>
        /// Returns a copy of this sequence concatenated with <paramref name="otherSequence"/>.
        /// </summary>
        /// <param name="otherSequence">The sequence with which to concatenate this sequence; will be lazily evaluated.</param>
        /// <returns>A copy of this sequence concatenated with <paramref name="otherSequence"/>.</returns>
        public ISequence<T> Concat(Func<IEnumerable<T>> otherSequence)
        {
            return IsEmpty
                       ? otherSequence().AsSequence()
                       : new Sequence<T>(Head, () => Tail.Concat(otherSequence));
        }

        /// <summary>
        /// Folds the elements of this sequence using the specified accumulator function. 
        /// </summary> 
        /// <example><code>int sum = Sequence.For(1,2,3,4).Fold(0, (a, b) => a + b);</code></example>
        /// <param name="seed">The initial accumulator value. A neutral value for the fold operation (e.g., empty list, or 0 for adding the elements of this sequence, or 1 for multiplication).</param>
        /// <param name="op">A function that takes the accumulator and an element of this sequence, and computes the new accumulator.</param>
        /// <returns>The result of applying <paramref name="op"/> between all the elements and <paramref name="seed"/>.</returns>
        public T Fold(T seed, Func<T, T, T> op)
        {
            return this.Aggregate(seed, op);
        }

        /// <summary>
        /// Folds the elements of this sequence using the specified accumulator function, going right to left. 
        /// </summary> 
        /// <example><code>int sum = Sequence.For(1,2,3,4).FoldRight(0, (a, b) => a + b);</code></example>
        /// <param name="seed">The initial accumulator value. A neutral value for the fold operation (e.g., empty list, or 0 for adding the elements of this sequence, or 1 for multiplication).</param>
        /// <param name="op">A function that takes an element of this sequence and the accumulator, and computes the new accumulator.</param>
        /// <returns>The result of applying <paramref name="op"/> between all the elements and <paramref name="seed"/>.</returns>
        public T FoldRight(T seed, Func<T, T, T> op)
        {
            if (IsEmpty)
                return seed;
            return op(Head, Tail.FoldRight(seed, op));
        }

        /// <summary>
        /// Reduces the elements of this sequence using the specified function.
        /// </summary>
        /// <param name="op">The operation to perform on successive elements of the sequence.</param>
        /// <returns>The accumulated value from successive applications of <paramref name="op"/>.</returns>
        public T Reduce(Func<T, T, T> op)
        {
            if (IsEmpty)
                throw new InvalidOperationException("Cannot reduce empty sequence.");

            return Tail.Fold(Head, op);
        }

        /// <summary>
        /// Reduces the elements of this sequence using the specified function, going right to left. 
        /// </summary>
        /// <param name="op">The operation to perform on successive elements of the sequence.</param>
        /// <returns>The accumulated value from successive applications of <paramref name="op"/>.</returns>
        public T ReduceRight(Func<T, T, T> op)
        {
            if (IsEmpty)
                throw new InvalidOperationException("Cannot reduce empty sequence.");
            if (Tail.IsEmpty)
                return Head;

            return op(Head, Tail.ReduceRight(op));
        }

        /// <summary>
        /// Crates a new sequence which contains all intermediate results of successive applications of a function <paramref name="op"/> to subsequent elements left to right.
        /// </summary>
        /// <param name="seed">The initial value for the scan.</param>
        /// <param name="op">A function that will apply operations to successive values in the sequence against previous accumulated results.</param>
        /// <returns>A new sequence which contains all intermediate results of successive applications of a function <paramref name="op"/> to subsequent elements left to right.</returns>
        public ISequence<T> Scan(T seed, Func<T, T, T> op)
        {
            if (IsEmpty)
                return Sequence.For(seed);

            return new Sequence<T>(seed, () =>
                                         Tail.Scan(op(seed, Head), op));
        }

        /// <summary>
        /// Crates a new sequence which contains all intermediate results of successive applications of a function <paramref name="op"/> to subsequent elements right to left.
        /// </summary>
        /// <param name="seed">The initial value for the scan.</param>
        /// <param name="op">A function that will apply operations to successive values in the sequence against previous accumulated results.</param>
        /// <returns>A new sequence which contains all intermediate results of successive applications of a function <paramref name="op"/> to subsequent elements left to right.</returns>
        public ISequence<T> ScanRight(T seed, Func<T, T, T> op)
        {
            var scanned = new Stack<T>();
            scanned.Push(seed);

            var acc = seed;

            foreach (var elem in this.Reverse())
            {
                acc = op(elem, acc);
                scanned.Push(acc);
            }

            return Sequence.For(scanned as IEnumerable<T>);
        }

        /// <summary>
        /// Groups elements in fixed size blocks by passing a "sliding window" over them.
        /// </summary>
        /// <param name="size">The number of elements per group.</param>
        /// <returns>An iterator producing sequences of size <paramref name="size"/>. The last sequence will be truncated if there are fewer elements than size.</returns>
        public IEnumerable<ISequence<T>> Sliding(int size)
        {
            return Sliding(size, 1);
        }

        /// <summary>
        /// Groups elements in fixed size blocks by passing a "sliding window" over them.
        /// </summary>
        /// <param name="size">The number of elements per group.</param>
        /// <param name="step">The number of elements to skip per iteration.</param>
        /// <returns>An iterator producing sequences of size <paramref name="size"/>. The last sequence will be truncated if there are fewer elements than size.</returns>
        public IEnumerable<ISequence<T>> Sliding(int size, int step)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException("size", "size must be a positive integer.");
            if (step <= 0)
                throw new ArgumentOutOfRangeException("step", "step must be a positive integer.");


            return new SlidingIterator(this, size, step);
        }

        private class SlidingIterator : IEnumerable<ISequence<T>>
        {
            private readonly ISequence<T> _sequence;
            private readonly int _size;
            private readonly int _step;

            public SlidingIterator(ISequence<T> sequence, int size, int step)
            {
                _sequence = sequence;
                _size = size;
                _step = step;
            }

            public IEnumerator<ISequence<T>> GetEnumerator()
            {
                ISequence<T> seq = _sequence;
                var buffer = new List<T>(_size);

                bool hasMoreElems = !seq.IsEmpty;

                while (hasMoreElems)
                {
                    //group elements into a buffer
                    IEnumerator<T> iterator = seq.GetEnumerator();

                    for (int i = 0; i < _size && iterator.MoveNext(); i++)
                        buffer.Add(iterator.Current);

                    //force the evaluation of the buffer's contents, before we clear the buffer.
                    yield return buffer.AsSequence().Force();

                    //keep going if there's at least one more element
                    hasMoreElems = iterator.MoveNext();
                    buffer.Clear();
                    seq = seq.Skip(_step);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
