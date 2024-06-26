﻿using PLang.Interfaces;
using System.Runtime.Caching;

namespace PLang.Services.CachingService
{
    public class InMemoryCaching : IAppCache
    {

        public async Task<object?> Get(string key)
        {
			return MemoryCache.Default.Get(key);
        }

        public async Task Set(string key, object value, TimeSpan slidingExpiration)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.SlidingExpiration = slidingExpiration;
            MemoryCache.Default.Set(key, value, policy);
        }
        public async Task Set(string key, object value, DateTimeOffset absoluteExpiration)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = absoluteExpiration;
            MemoryCache.Default.Set(key, value, policy);
        }

		public async Task Remove(string key)
		{
			MemoryCache.Default.Remove(key);
		}
	}
}
