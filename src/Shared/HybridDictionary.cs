// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// HybridDictionary is a dictionary which is implemented to efficiently store both small and large numbers of items.  When only a single item is stored, we use no 
    /// collections at all.  When 1 &lt; n &lt;= MaxListSize is stored, we use a list.  For any larger number of elements, we use a dictionary.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    [Serializable]
    internal class HybridDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, ICollection where TValue : class
    {
        /// <summary>
        /// The dictionary, list, or pair used for a store
        /// </summary>
        private KeyValuePair<TKey, TValue>? _single;
        private Dictionary<TKey, TValue> _dictionary;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HybridDictionary()
            : this(0)
        {
        }

        /// <summary>
        /// Capacity constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity of the collection.</param>
        public HybridDictionary(int capacity)
            : this(capacity, EqualityComparer<TKey>.Default)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="comparer">The comparer to use.</param>
        public HybridDictionary(IEqualityComparer<TKey> comparer)
            : this()
        {
            this.Comparer = comparer;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        /// <param name="comparer">The comparer to use.</param>
        public HybridDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            this.Comparer = comparer ?? EqualityComparer<TKey>.Default;

            if (capacity > 1)
            {
                _dictionary = new Dictionary<TKey, TValue>(capacity, comparer);
            }
        }

        /// <summary>
        /// Serialization constructor.
        /// </summary>
        public HybridDictionary(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cloning constructor.
        /// </summary>
        public HybridDictionary(HybridDictionary<TKey, TValue> other, IEqualityComparer<TKey> comparer)
            : this(other.Count, comparer)
        {
            foreach (KeyValuePair<TKey, TValue> keyValue in other)
            {
                Add(keyValue.Key, keyValue.Value);
            }
        }

        /// <summary>
        /// Gets the comparer used to compare keys.
        /// </summary>
        public IEqualityComparer<TKey> Comparer { get; private set; }

        /// <summary>
        /// Returns the collection of keys in the dictionary.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                if (!_single.HasValue && _dictionary == null)
                {
                    return ReadOnlyEmptyCollection<TKey>.Instance;
                }

                if (_single.HasValue)
                {
                    return new[] { _single.Value.Key };
                }

                return _dictionary.Keys;
            }
        }

        /// <summary>
        /// Returns the collection of values in the dictionary.
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                if (!_single.HasValue && _dictionary == null)
                {
                    return ReadOnlyEmptyCollection<TValue>.Instance;
                }

                if (_single.HasValue) // Can't use 'as' for structs
                {
                    return new[] {_single.Value.Value};
                }

                return _dictionary.Values;
            }
        }

        /// <summary>
        /// Gets the number of items in the dictionary.
        /// </summary>
        public int Count
        {
            get
            {
                if (!_single.HasValue && _dictionary == null)
                {
                    return 0;
                }

                return _single.HasValue ? 1 : _dictionary.Count;
            }
        }

        /// <summary>
        /// Returns true if this is a read-only collection.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Returns true if this collection is synchronized.
        /// </summary>
        public bool IsSynchronized => false;

        /// <summary>
        /// Gets the sync root for this collection.
        /// </summary>
        /// <remarks>
        /// NOTE: Returns "this", which is not normally recommended as a caller
        /// could implement its own locking scheme on "this" and deadlock. However, a
        /// sync object would be significant wasted space as there are a lot of these, 
        /// and the caller is not foolish.
        /// </remarks>
        public object SyncRoot => this;

        /// <summary>
        /// Returns true if the dictionary is a fixed size.
        /// </summary>
        public bool IsFixedSize => false;

        /// <summary>
        /// Returns a collection of the keys in the dictionary.
        /// </summary>
        ICollection IDictionary.Keys => (ICollection)((IDictionary<TKey, TValue>)this).Keys;

        /// <summary>
        /// Returns a collection of the values in the dictionary.
        /// </summary>
        ICollection IDictionary.Values => (ICollection)((IDictionary<TKey, TValue>)this).Values;

        /// <summary>
        /// Item accessor.
        /// </summary>
        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException("The specified key was not found in the collection.");
            }

            set
            {
                if (!_single.HasValue && _dictionary == null)
                {
                    _single = new KeyValuePair<TKey, TValue>(key, value);
                    return;
                }

                if (_single.HasValue)
                {
                    if (Comparer.Equals(_single.Value.Key, key))
                    {
                        _single = new KeyValuePair<TKey, TValue>(key, value);
                        return;
                    }

                    _dictionary = new Dictionary<TKey, TValue>(Comparer)
                    {
                        {_single.Value.Key, _single.Value.Value}, {key, value}
                    };
                    _single = null;
                    
                    return;
                }

                _dictionary[key] = value;
            }
        }

        /// <summary>
        /// Item accessor.
        /// </summary>
        public object this[object key]
        {
            get => ((IDictionary<TKey, TValue>)this)[(TKey)key];
            set => ((IDictionary<TKey, TValue>)this)[(TKey)key] = (TValue)value;
        }

        /// <summary>
        /// Adds an item to the dictionary.
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            ErrorUtilities.VerifyThrowArgumentNull(key, nameof(key));

            if (!_single.HasValue && _dictionary == null)
            {
                _single = new KeyValuePair<TKey, TValue>(key, value);
                return;
            }

            if (_single.HasValue)
            {
                if (Comparer.Equals(_single.Value.Key, key))
                {
                    throw new ArgumentException("A value with the same key is already in the collection.");
                }

                _dictionary = new Dictionary<TKey, TValue>(Comparer)
                {
                    {_single.Value.Key, _single.Value.Value}, {key, value}
                };
                _single = null;
                return;
            }

            _dictionary.Add(key, value);
        }

        /// <summary>
        /// Returns true if the specified key is contained within the dictionary.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        /// <summary>
        /// Removes a key from the dictionary.
        /// </summary>
        public bool Remove(TKey key)
        {
            ErrorUtilities.VerifyThrowArgumentNull(key, nameof(key));

            if (!_single.HasValue && _dictionary == null)
            {
                return false;
            }

            if (_single.HasValue)
            {
                if (Comparer.Equals(_single.Value.Key, key))
                {
                    _single = null;
                    return true;
                }

                return false;
            }

            return _dictionary.Remove(key);
        }

        /// <summary>
        /// Returns true and the value for the specified key if it is present in the dictionary, false otherwise.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = null;

            if (!_single.HasValue && _dictionary == null)
            {
                return false;
            }

            if (_single.HasValue)
            {
                if (Comparer.Equals(_single.Value.Key, key))
                {
                    value = _single.Value.Value;
                    return true;
                }

                return false;
            }

            return _dictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds a key/value pair to the dictionary.
        /// </summary>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <summary>
        /// Clears the dictionary.
        /// </summary>
        public void Clear()
        {
            _single = null;
            _dictionary = null;
        }

        /// <summary>
        /// Returns true of the dictionary contains the key/value pair.
        /// </summary>
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out TValue value) && item.Value == value;

        /// <summary>
        /// Copies the contents of the dictionary to the specified array.
        /// </summary>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (KeyValuePair<TKey, TValue> entry in this)
            {
                array[i] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Removed the specified key/value pair from the dictionary.
        /// NOT IMPLEMENTED.
        /// </summary>
        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

        /// <summary>
        /// Gets an enumerator over the key/value pairs in the dictionary.
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (!_single.HasValue && _dictionary == null)
            {
                return ReadOnlyEmptyCollection<KeyValuePair<TKey, TValue>>.Instance.GetEnumerator();
            }

            if (_single.HasValue)
            {
                return new SingleEnumerator(_single.Value);
            }
            return _dictionary.GetEnumerator();
        }

        /// <summary>
        /// Gets an enumerator over the key/value pairs in the dictionary.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Copies the contents of the dictionary to the specified Array.
        /// </summary>
        public void CopyTo(Array array, int index)
        {
            int i = index;
            foreach (KeyValuePair<TKey, TValue> entry in this)
            {
                array.SetValue(new DictionaryEntry(entry.Key, entry.Value), i);
            }
        }

        /// <summary>
        /// Adds the specified key/value pair to the dictionary.
        /// </summary>
        public void Add(object key, object value) => Add((TKey)key, (TValue)value);

        /// <summary>
        /// Returns true if the dictionary contains the specified key.
        /// </summary>
        public bool Contains(object key) => ContainsKey((TKey)key);

        /// <summary>
        /// Returns an enumerator over the key/value pairs in the dictionary.
        /// </summary>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            if (!_single.HasValue && _dictionary == null)
            {
                return ((IDictionary)ReadOnlyEmptyDictionary<TKey, TValue>.Instance).GetEnumerator();
            }

            if (_single.HasValue)
            {
                return new SingleDictionaryEntryEnumerator(new DictionaryEntry(_single.Value.Key, _single.Value.Value));
                
            }

            return ((IDictionary)_dictionary).GetEnumerator();
        }

        /// <summary>
        /// Removes the specified key from the dictionary.
        /// </summary>
        public void Remove(object key) => Remove((TKey)key);


        /// <summary>
        /// An enumerator for when the dictionary has only a single entry in it.
        /// </summary>
        private struct SingleEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            /// <summary>
            /// The single value.
            /// </summary>
            private KeyValuePair<TKey, TValue> value;

            /// <summary>
            /// Flag indicating when we are at the end of the enumeration.
            /// </summary>
            private bool enumerationComplete;

            /// <summary>
            /// Constructor.
            /// </summary>
            public SingleEnumerator(KeyValuePair<TKey, TValue> value)
            {
                this.value = value;
                enumerationComplete = false;
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    if (enumerationComplete)
                    {
                        return value;
                    }

                    throw new InvalidOperationException("Past end of enumeration");
                }
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            object IEnumerator.Current => ((IEnumerator<KeyValuePair<TKey, TValue>>)this).Current;

            /// <summary>
            /// Disposer.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Moves to the next item.
            /// </summary>
            public bool MoveNext()
            {
                if (!enumerationComplete)
                {
                    enumerationComplete = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Resets the enumerator.
            /// </summary>
            public void Reset()
            {
                enumerationComplete = false;
            }
        }

        /// <summary>
        /// An enumerator for when the dictionary has only a single entry in it.
        /// Cannot find a way to make the SingleEntryEnumerator serve both purposes, as foreach preferentially
        /// casts to IEnumerable that returns the generic enumerator instead of an IDictionaryEnumerator.
        /// 
        /// Don't want to use the List enumerator below as a throwaway one-entry list would need to be allocated.
        /// </summary>
        private struct SingleDictionaryEntryEnumerator : IDictionaryEnumerator
        {
            /// <summary>
            /// The single value.
            /// </summary>
            private DictionaryEntry value;

            /// <summary>
            /// Flag indicating when we are at the end of the enumeration.
            /// </summary>
            private bool enumerationComplete;

            /// <summary>
            /// Constructor.
            /// </summary>
            public SingleDictionaryEntryEnumerator(DictionaryEntry value)
            {
                this.value = value;
                enumerationComplete = false;
            }

            /// <summary>
            /// Key
            /// </summary>
            public object Key => Entry.Key;

            /// <summary>
            /// Value
            /// </summary>
            public object Value => Entry.Value;

            /// <summary>
            /// Current
            /// </summary>
            public object Current => Entry;

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public DictionaryEntry Entry
            {
                get
                {
                    if (enumerationComplete)
                    {
                        return value;
                    }

                    throw new InvalidOperationException("Past end of enumeration");
                }
            }

            /// <summary>
            /// Disposer.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Moves to the next item.
            /// </summary>
            public bool MoveNext()
            {
                if (!enumerationComplete)
                {
                    enumerationComplete = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Resets the enumerator.
            /// </summary>
            public void Reset()
            {
                enumerationComplete = false;
            }
        }
    }
}
