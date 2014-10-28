using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tsql2pgsql.collections
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Gets a value from the dictionary.  If the requested key does not exist, the default value is
        /// returned.  Caller can supply the default value.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns></returns>
        public static V Get<K, V>(this IDictionary<K, V> dictionary, K key, V defaultValue = default(V))
        {
            V value;

            if (dictionary.TryGetValue(key, out value))
                return value;

            return defaultValue;
        }
    }
}
