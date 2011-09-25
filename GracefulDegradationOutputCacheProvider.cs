using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Caching;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Web;
using System.Threading;


namespace GroupCommerce.Web
{
    internal class GracefulDegradationOutputCacheProvider : OutputCacheProvider
    {
        public const string KEY_CURRENT_REQUEST_CACHE_KEY = "_InMemoryCacheProvider_CurrentCacheItemKey";
        
        [Serializable]
        internal class CacheItem
        {
            public DateTime Expires;
            public object Item;
        }

        // static dictionary to hold our cached items
        private static ConcurrentDictionary<string, CacheItem> _items = new ConcurrentDictionary<string, CacheItem>();

        public override object Add(string key, object entry, DateTime utcExpiry)
        {
            Debug.WriteLine("Cache.Add(" + key + ", " + entry + ", " + utcExpiry + ")");

            CacheItem existing;
            if (_items.TryGetValue(key, out existing))
                return existing.Item;

            _items.TryAdd(key, new CacheItem() { Item = entry, Expires = utcExpiry });
            return entry;
        }

        public override object Get(string key)
        {
            // stick the key inside HttpContext.Items so we
            // can read it in the HttpModule in case an error
            // occurs
            HttpContext.Current.Items[KEY_CURRENT_REQUEST_CACHE_KEY] = key;
            return Get(key, true);
        }

        /// <summary>
        /// Gets an item from the cache by key
        /// </summary>
        /// <param name="key">Key to fine</param>
        /// <param name="respectExpiration">If true, don't return stale results.  This parameter should be true
        /// in regular caching cases and false if we're retrieving from cache when an exception occurred</param>
        /// <returns></returns>
        internal object Get(string key, bool respectExpiration)
        {
            Debug.WriteLine("Cache.Get(" + key + ")");

            CacheItem existing;
            if (!_items.TryGetValue(key, out existing))
                return null;

            if (!respectExpiration)
                return existing.Item;

            if (existing.Expires > DateTime.UtcNow)
                return existing.Item;

            return null;
        }

        public override void Remove(string key)
        {
            Debug.WriteLine("Cache.Remove(" + key + ")");

            if (_items.ContainsKey(key))
            {
                CacheItem _;
                _items.TryRemove(key, out _);
            }
        }

        public override void Set(string key, object entry, DateTime utcExpiry)
        {
            Debug.WriteLine("Cache.Set(" + key + ", " + entry + ", " + utcExpiry + ")");

            var item = new CacheItem { Expires = utcExpiry, Item = entry };
            _items[key] = item;
        }
    }
}
