﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Uragano.Abstractions;
using Uragano.Codec.MessagePack;

namespace Uragano.Caching.Redis
{
    public class RedisPartitionCaching : ICaching
    {
        private IDistributedCache Cache { get; }

        public RedisPartitionCaching(IDistributedCache distributedCache)
        {
            Cache = distributedCache;
        }

        public async Task Set<TValue>(string key, TValue value, TimeSpan? expire)
        {
            await Cache.SetAsync(key, SerializerHelper.Serialize(value), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expire
            });
        }

        public async Task<(object value, bool hasKey)> Get(string key, Type type)
        {
            var bytes = await Cache.GetAsync(key);
            if (bytes == null || bytes.LongLength == 0)
                return (null, false);
            return (SerializerHelper.Deserialize(bytes), true);
        }

        public async Task<(TValue value, bool hasKey)> Get<TValue>(string key)
        {
            var (value, hasKey) = await Get(key, typeof(TValue));
            return (value == null ? default : (TValue)value, hasKey);
        }

        public async Task Remove(params string[] keys)
        {
            await Cache.RemoveAsync(string.Join("|", keys));
        }
    }
}
