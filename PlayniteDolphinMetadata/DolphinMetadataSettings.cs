using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Markup;
using Newtonsoft.Json;
using Playnite.SDK;

namespace PlayniteDolphinMetadata
{
    public class DolphinMetadataSettings : ISettings, INotifyPropertyChanged
    {
        public const int CurrentVersion = 1;

        private static readonly ILogger Logger = LogManager.GetLogger();

        private static readonly BiOrderedDictionary<string, string> CoversToCodes = new BiOrderedDictionary<string, string>
        {
            {"Regular Cover (Small)", "cover"},
            {"3D Cover (Small)", "cover3D"},
            {"Disc Label", "disc"},
            {"HQ Boxart", "coverfullHQ"},
            {"HQ Boxart, cropped to cover only", "cropped_coverfullHQ"},
            {"Full Boxart", "coverfull"},
        };

        private static readonly BiOrderedDictionary<string, string> LanguagesToCodes = new BiOrderedDictionary<string, string>
        {
            {"English", "EN"},
            {"German", "DE"},
            {"French", "FR"},
            {"Spanish", "ES"},
            {"Italian", "IT"},
            {"Dutch", "NL"},
        };

        #region Properties for XAML display
        [JsonIgnore] public ICollection<string> LanguageNames => LanguagesToCodes.Keys;
        [JsonIgnore] public ICollection<string> CoverNames => CoversToCodes.Keys;

        [JsonIgnore]
        public string CoverDownloadPreferenceName
        {
            get => CoversToCodes.GetKey(CoverDownloadPreference);
            set => CoverDownloadPreference = CoversToCodes.GetValue(value);
        }

        [JsonIgnore]
        public string LanguagePreferenceName
        {
            get => LanguagesToCodes.GetKey(LanguagePreference);
            set => LanguagePreference = LanguagesToCodes.GetValue(value);
        }
        #endregion

        #region Properties for backing code
        private string languagePreference = "EN";
        public string LanguagePreference
        {
            get => languagePreference;
            private set => OnPropertySet(ref languagePreference, value, nameof(LanguagePreference), nameof(LanguagePreferenceName));
        }

        private string coverDownloadPreference = "cropped_coverfullHQ";
        public string CoverDownloadPreference
        {
            get => coverDownloadPreference;
            private set => OnPropertySet(ref coverDownloadPreference, value, nameof(CoverDownloadPreference), nameof(CoverDownloadPreferenceName));
        }

        public DateTime LastWiiTdbUpdate { get; set; }
        #endregion

        private readonly DolphinMetadataPlugin _plugin;

        // Constructor for deserialization
        public DolphinMetadataSettings()
        {
        }

        public DolphinMetadataSettings(DolphinMetadataPlugin plugin) : this()
        {
            _plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<DolphinMetadataSettings>();
            if (savedSettings == null) return;

            LanguagePreference = savedSettings.LanguagePreference;
            CoverDownloadPreference = savedSettings.CoverDownloadPreference;
            LastWiiTdbUpdate = savedSettings.LastWiiTdbUpdate;
            // X = savedSettings.X;
        }

        public void BeginEdit()
        {
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(this);
        }

        public void CancelEdit()
        {
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected void OnPropertySet<T>(ref T oldValue, T newValue, [CallerMemberName] string? property = null)
        {
            if (oldValue == null || newValue == null || !newValue.Equals(oldValue))
            {
                oldValue = newValue;
                OnPropertyChanged(property);
            }
        }

        protected void OnPropertySet<T>(ref T oldValue, T newValue, params string[] additionalProperties)
        {
            if (oldValue == null || newValue == null || !newValue.Equals(oldValue))
            {
                oldValue = newValue;
                foreach (var property in additionalProperties)
                {
                    OnPropertyChanged(property);
                }
            }
        }
    }

    public class BiOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly IDictionary<TKey, TValue> _firstToSecond = new OrderedDictionary<TKey, TValue>();
        private readonly IDictionary<TValue, TKey> _secondToFirst = new OrderedDictionary<TValue, TKey>();

        public ICollection<TKey> Keys => _firstToSecond.Keys;
        public ICollection<TValue> Values => _secondToFirst.Keys;

        public void Add(TKey first, TValue second)
        {
            if (_firstToSecond.ContainsKey(first) ||
                _secondToFirst.ContainsKey(second))
            {
                throw new ArgumentException("Duplicate first or second");
            }
            _firstToSecond.Add(first, second);
            _secondToFirst.Add(second, first);
        }

        public bool TryGetValue(TKey first, out TValue second) => _firstToSecond.TryGetValue(first, out second);
        public bool TryGetKey(TValue second, out TKey first) => _secondToFirst.TryGetValue(second, out first);

        public TValue GetValue(TKey first) => _firstToSecond[first];
        public TKey GetKey(TValue second) => _secondToFirst[second];

        public TValue this[TKey key]
        {
            get => _firstToSecond[key];
            set => _firstToSecond[key] = value;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _firstToSecond.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_firstToSecond).GetEnumerator();
    }

    public class OrderedDictionary<TK, TV> : IDictionary<TK, TV>
    {
        public OrderedDictionary UnderlyingCollection { get; } = new();

        public TV this[TK key]
        {
            get => (TV)UnderlyingCollection[key!];
            set => UnderlyingCollection[key!] = value;
        }

        public TV this[int index]
        {
            get => (TV)UnderlyingCollection[index];
            set => UnderlyingCollection[index] = value;
        }

        // IOrderedDictionary
        public ICollection<TK> Keys => UnderlyingCollection.Keys.OfType<TK>().ToList();
        public ICollection<TV> Values => UnderlyingCollection.Values.OfType<TV>().ToList();
        public bool IsReadOnly => UnderlyingCollection.IsReadOnly;
        public int Count => UnderlyingCollection.Count;
        public IDictionaryEnumerator GetEnumerator() => UnderlyingCollection.GetEnumerator();
        public void Insert(int index, TK key, TV value) => UnderlyingCollection.Insert(index, key, value);
        public void RemoveAt(int index) => UnderlyingCollection.RemoveAt(index);
        public bool Contains(TK key) => UnderlyingCollection.Contains(key);
        public void Add(TK key, TV value) => UnderlyingCollection.Add(key, value);
        public void Clear() => UnderlyingCollection.Clear();
        public void CopyTo(Array array, int index) => UnderlyingCollection.CopyTo(array, index);

        // IDictionary<K, V>
        public bool ContainsKey(TK key) => Contains(key);

        public bool Remove(TK key)
        {
            if (!Contains(key)) return false;
            Remove(key);
            return true;
        }

        public bool TryGetValue(TK key, out TV value)
        {
            if (!Contains(key))
            {
                value = default!;
                return false;
            }
            value = this[key];
            return true;
        }

        public void Add(KeyValuePair<TK, TV> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<TK, TV> item) => Contains(item.Key) && Equals(this[item.Key], item.Value);
        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex) => CopyTo((Array)array, arrayIndex);
        public bool Remove(KeyValuePair<TK, TV> item) => Contains(item) && Remove(item.Key);

        IEnumerator<KeyValuePair<TK, TV>> IEnumerable<KeyValuePair<TK, TV>>.GetEnumerator()
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (TK k in UnderlyingCollection.Keys)
            {
                yield return new KeyValuePair<TK, TV>(k, this[k]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}