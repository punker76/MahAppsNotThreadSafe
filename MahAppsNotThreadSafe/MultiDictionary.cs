using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


namespace MahAppsNotThreadSafe
{
	/// <summary>
	/// The multi dictionary is a dictionary that contains more than one value per key.
	/// An enumeration over the dictionary yields every value for every key: the
	/// enumerator returns <see cref="KeyValuePair{TKey,TValue}"/>, where the Key
	/// is each key in the dictionary in sequence, and for each key, the enumeration
	/// will yield each value, and then for the next key, each value, etc.
	/// </summary>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	/// <typeparam name="TValue">The type of the list contents.</typeparam>
	public class MultiDictionary<TKey, TValue>
			: IEnumerable<KeyValuePair<TKey, TValue>>, IEquatable<MultiDictionary<TKey, TValue>>
	{
		/// <summary>
		/// Holds the values.
		/// </summary>
		protected Dictionary<TKey, List<TValue>> Dictionary;

		protected int DefaultListInitialCapacity;


		/// <summary>
		/// Constructor. creates a new underlying Dictionary with the default initial capacity.
		/// The default <see cref="IEqualityComparer{T}"/> is used for values.
		/// </summary>
		public MultiDictionary() {
			Dictionary = new Dictionary<TKey, List<TValue>>();
			DefaultListInitialCapacity = 8;
		}

		/// <summary>
		/// Constructor. Creates a new underlying Dictionary with the default initial capacity.
		/// The given <see cref="IEqualityComparer{T}"/> is used for keys.
		/// </summary>
		/// <param name="comparer">Not null.</param>
		public MultiDictionary(IEqualityComparer<TKey> comparer) {
			if (comparer == null)
				throw new ArgumentNullException(nameof(comparer));
			Dictionary = new Dictionary<TKey, List<TValue>>(comparer);
			DefaultListInitialCapacity = 8;
		}

		/// <summary>
		/// Constructor. creates a new underlying Dictionary with the specified initial capacity.
		/// The default <see cref="IEqualityComparer{T}"/> is used for keys.
		/// </summary>
		/// <param name="initialCapacity">&gt;= 0.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public MultiDictionary(int initialCapacity) {
			Dictionary = new Dictionary<TKey, List<TValue>>(initialCapacity);
			DefaultListInitialCapacity = Math.Max(2, Math.Min(32, initialCapacity / 2));
		}

		/// <summary>
		/// Constructor. creates a new underlying Dictionary with the specified initial capacity.
		/// The given <see cref="IEqualityComparer{T}"/> is used for keys.
		/// </summary>
		/// <param name="initialCapacity">&gt;= 0.</param>
		/// <param name="comparer">Not null.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public MultiDictionary(int initialCapacity, IEqualityComparer<TKey> comparer) {
			if (comparer == null)
				throw new ArgumentNullException(nameof(comparer));
			Dictionary = new Dictionary<TKey, List<TValue>>(initialCapacity, comparer);
			DefaultListInitialCapacity = Math.Max(2, Math.Min(32, initialCapacity / 2));
		}


		/// <summary>
		/// Ensures the key is present and returns the list.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected List<TValue> EnsureKey(TKey key) {
			if (!Dictionary.ContainsKey(key))
				return (Dictionary[key] = new List<TValue>(DefaultListInitialCapacity));
			List<TValue> result = Dictionary[key];
			return result
					?? (Dictionary[key] = new List<TValue>(DefaultListInitialCapacity));
		}


		/// <summary>
		/// Returns the Count of Keys.
		/// </summary>
		public int Count
			=> Dictionary.Count;

		/// <summary>
		/// Returns the Count of Values for the Key.
		/// </summary>
		public int GetCount(TKey key)
			// ReSharper disable once CollectionNeverUpdated.Local
			=> (Dictionary.TryGetValue(key, out List<TValue> list))
				? list.Count
				: 0;


		/// <summary>
		/// Returns a new list containing all keys.
		/// </summary>
		/// <returns>Not null; may be empty.</returns>
		public List<TKey> Keys
			=> new List<TKey>(Dictionary.Keys);

		/// <summary>
		/// Returns true if the key is present.
		/// The default Equals method is used.
		/// </summary>
		/// <param name="key">The value to find.</param>
		/// <returns>True if found.</returns>
		public bool ContainsKey(TKey key)
			=> Dictionary.ContainsKey(key);

		/// <summary>
		/// Returns true if the key is present.
		/// The search predicate is used.
		/// </summary>
		/// <param name="match">The search predicate. Not null.</param>
		/// <returns>True if found.</returns>
		public bool ContainsKey(Func<TKey, bool> match)
			=> Dictionary.Keys.Any(match);


		/// <summary>
		/// Returns true if the value is present under any key.
		/// The default Equals method is used.
		/// </summary>
		/// <param name="value">The value to find.</param>
		/// <returns>True if found under any key.</returns>
		public bool ContainsValue(TValue value)
			=> Dictionary.Values.Any(list => list.Contains(value));

		/// <summary>
		/// Returns true if the value is present under any key.
		/// The search predicate is used.
		/// </summary>
		/// <param name="match">The search predicate. Not null.</param>
		/// <returns>True if found under any key.</returns>
		public bool ContainsValue(Func<TValue, bool> match)
			=> Dictionary.Values.Any(list => list.Any(match));

		/// <summary>
		/// Returns true if the value is present under the given key.
		/// The default Equals method is used.
		/// </summary>
		/// <param name="key">The key to search.</param>
		/// <param name="value">The value to find.</param>
		/// <returns>True if found under the given key.</returns>
		public bool ContainsValue(TKey key, TValue value)
			// ReSharper disable once CollectionNeverUpdated.Local
			=> Dictionary.TryGetValue(key, out List<TValue> list) && list.Contains(value);

		/// <summary>
		/// Returns true if the value is present under the given key.
		/// The search predicate is used.
		/// </summary>
		/// <param name="key">The key to search.</param>
		/// <param name="match">The search predicate. Not null.</param>
		/// <returns>True if found under the given key.</returns>
		public bool ContainsValue(TKey key, Func<TValue, bool> match)
			// ReSharper disable once CollectionNeverUpdated.Local
			=> Dictionary.TryGetValue(key, out List<TValue> list) && list.Any(match);


		/// <summary>
		/// Returns the first match if it is present under any key.
		/// </summary>
		/// <param name="match">The search predicate. Not null.</param>
		/// <param name="value">Set if found.</param>
		/// <returns>True if found under any key.</returns>
		public bool TryGetValue(Func<TValue, bool> match, out KeyValuePair<TKey, TValue> value) {
			foreach (KeyValuePair<TKey, List<TValue>> kv in Dictionary) {
				foreach (TValue v in kv.Value) {
					if (!match(v))
						continue;
					value = new KeyValuePair<TKey, TValue>(kv.Key, v);
					return true;
				}
			}
			value = default(KeyValuePair<TKey, TValue>);
			return false;
		}

		/// <summary>
		/// Returns true if the value is present under the given key.
		/// </summary>
		/// <param name="key">The key to search.</param>
		/// <param name="match">The search predicate. Not null.</param>
		/// <param name="value">Set if found.</param>
		/// <returns>True if found under the given key.</returns>
		public bool TryGetValue(TKey key, Func<TValue, bool> match, out TValue value) {
			// ReSharper disable once CollectionNeverUpdated.Local
			if (Dictionary.TryGetValue(key, out List<TValue> list)) {
				foreach (TValue v in list) {
					if (!match(v))
						continue;
					value = v;
					return true;
				}
			}
			value = default(TValue);
			return false;
		}

		/// <summary>
		/// Returns a new list containing all values for all keys.
		/// </summary>
		/// <returns>Not null; may be empty.</returns>
		public List<TValue> AllValues {
			get {
				List<TValue> result = new List<TValue>();
				foreach (List<TValue> value in Dictionary.Values) {
					result.AddRange(value);
				}
				return result;
			}
		}

		/// <summary>
		/// Returns a new list containing all values for the key. Returns an empty
		/// list if the key is not found.
		/// </summary>
		/// <param name="key">The key to search for.</param>
		/// <returns>Not null; may be empty.</returns>
		public List<TValue> GetAllValues(TKey key)
			// ReSharper disable once CollectionNeverUpdated.Local
			=> Dictionary.TryGetValue(key, out List<TValue> list)
				? new List<TValue>(list)
				: new List<TValue>(0);


		/// <summary>
		/// Returns a new <see cref="IDictionary{TKey,TValue}"/> with all
		/// keys and their values.
		/// </summary>
		/// <returns>Not null; may be empty.</returns>
		public Dictionary<TKey, List<TValue>> GetAll() {
			Dictionary<TKey, List<TValue>> result = new Dictionary<TKey, List<TValue>>();
			foreach (KeyValuePair<TKey, List<TValue>> kv in Dictionary) {
				result[kv.Key] = new List<TValue>(kv.Value);
			}
			return result;
		}


		/// <summary>
		/// Adds a new value in the Values collection.
		/// </summary>
		/// <param name="key">The key where to place the item in the value list.</param>
		/// <param name="newItem">The new item to add.</param>
		public void AddValue(TKey key, TValue newItem)
			=> EnsureKey(key).Add(newItem);

		/// <summary>
		/// Adds a new value in the Values collection only if the value does
		/// not already exist.
		/// The default Equals method is used.
		/// </summary>
		/// <param name="key">The key where to place the item in the value list.</param>
		/// <param name="newItem">The new item to add.</param>
		/// <returns>True if added.</returns>
		public bool TryAddValue(TKey key, TValue newItem) {
			List<TValue> list = EnsureKey(key);
			if (list.Contains(newItem))
				return false;
			list.Add(newItem);
			return true;
		}

		/// <summary>
		/// Adds a new value in the Values collection only if the value does
		/// not already exist.
		/// The search predicate is used.
		/// </summary>
		/// <param name="key">The key where to place the item in the value list.</param>
		/// <param name="newItem">The new item to add.</param>
		/// <param name="match">The search predicate. Not null.</param>
		/// <returns>True if added.</returns>
		public bool TryAddValue(TKey key, TValue newItem, Func<TValue, bool> match) {
			List<TValue> list = EnsureKey(key);
			if (list.Any(match))
				return false;
			list.Add(newItem);
			return true;
		}

		/// <summary>
		/// Adds a list of values to append to the value collection.
		/// </summary>
		/// <param name="key">The key where to place the item in the value list.</param>
		/// <param name="newItems">The new items to add.</param>
		public void AddValues(TKey key, IEnumerable<TValue> newItems) {
			List<TValue> list = EnsureKey(key);
			list.AddRange(newItems);
			if (list.Count == 0)
				Dictionary.Remove(key);
		}

		/// <summary>
		/// Adds each new value in the Values collection only if the value does
		/// not already exist.
		/// The default Equals method is used.
		/// </summary>
		/// <param name="key">The key where to place the item in the value list.</param>
		/// <param name="newItems">The new items to add.</param>
		/// <returns>A count of all items added.</returns>
		public int TryAddValues(TKey key, IEnumerable<TValue> newItems) {
			List<TValue> list = EnsureKey(key);
			int count = list.Count;
			foreach (TValue newItem in newItems) {
				if (!list.Contains(newItem))
					list.Add(newItem);
			}
			if (list.Count == 0)
				Dictionary.Remove(key);
			return list.Count - count;
		}

		/// <summary>
		/// Adds each new value in the Values collection only if the value does
		/// not already exist.
		/// The search predicate is used.
		/// </summary>
		/// <param name="key">The key where to place the item in the value list.</param>
		/// <param name="newItems">The new items to add.</param>
		/// <param name="match">The search predicate. Not null.</param>
		/// <returns>A count of all items added.</returns>
		public int TryAddValues(TKey key, IEnumerable<TValue> newItems, Func<TValue, bool> match) {
			List<TValue> list = EnsureKey(key);
			int count = list.Count;
			foreach (TValue newItem in newItems) {
				if (!list.Any(match))
					list.Add(newItem);
			}
			if (list.Count == 0)
				Dictionary.Remove(key);
			return list.Count - count;
		}


		/// <summary>
		/// Removes a specific element from the dict.
		/// If the value list is empty the key is removed from the dict.
		/// </summary>
		/// <param name="key">The key from where to remove the value.</param>
		/// <param name="value">The value to remove.</param>
		/// <returns>Returns false if the key or the value was not found.</returns>
		public bool RemoveValue(TKey key, TValue value) {
			// ReSharper disable once CollectionNeverUpdated.Local
			if (!Dictionary.TryGetValue(key, out List<TValue> list))
				return false;
			bool result = list.Remove(value);
			if (list.Count == 0)
				Dictionary.Remove(key);
			return result;
		}

		/// <summary>
		/// Removes a key from the dict.
		/// </summary>
		/// <param name="key">The key to remove.</param>
		/// <returns>Returns false if the key was not found.</returns>
		public bool RemoveKey(TKey key)
			=> Dictionary.Remove(key);

		/// <summary>
		/// Removes all items that match the predicate under any key.
		/// If the value list is empty the key is removed from the dict.
		/// </summary>
		/// <param name="match">The predicate to match the items</param>
		/// <returns>Returns 0 if the predicate had no matches.</returns>
		public int RemoveAllValues(Predicate<TValue> match) {
			int result = 0;
			foreach (TKey key in Keys) {
				List<TValue> list = Dictionary[key];
				result += list.RemoveAll(match);
				if (list.Count == 0)
					Dictionary.Remove(key);
			}
			return result;
		}

		/// <summary>
		/// Removes all items that match the predicate.
		/// If the value list is empty the key is removed from the dict.
		/// </summary>
		/// <param name="key">The key from where to remove the value.</param>
		/// <param name="match">The predicate to match the items</param>
		/// <returns>Returns 0 if the key was not found or if the predicate
		/// had no matches.</returns>
		public int RemoveAllValues(TKey key, Predicate<TValue> match) {
			if (!Dictionary.TryGetValue(key, out List<TValue> list))
				return 0;
			int count = list.RemoveAll(match);
			if (list.Count == 0)
				Dictionary.Remove(key);
			return count;
		}


		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
			foreach (KeyValuePair<TKey, List<TValue>> kv in Dictionary) {
				foreach (TValue value in kv.Value) {
					yield return new KeyValuePair<TKey, TValue>(kv.Key, value);
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();


		public void Clear()
			=> Dictionary.Clear();


		public override int GetHashCode()
			=> Dictionary.GetHashCode();

		public override bool Equals(object obj)
			=> Equals(obj as MultiDictionary<TKey, TValue>);

		public virtual bool Equals(MultiDictionary<TKey, TValue> obj)
			=> (obj != null) && Dictionary.Equals(obj.Dictionary);

		public override string ToString()
			=> $"{GetType().Name}[{Dictionary}]";
	}
}
