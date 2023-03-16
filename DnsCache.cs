using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace ArashiDNS
{
    public class DnsCache
    {
        public static void Add(List<DnsQuestion> question, List<DnsRecordBase> answerRecords)
        {
            var ttl = answerRecords.First().TimeToLive;
            Add(new CacheItem(question.First().ToString(), answerRecords), ttl);
        }

        public static void Add(CacheItem cacheItem, int ttl)
        {
            if (!MemoryCache.Default.Contains(cacheItem.Key))
                MemoryCache.Default.Add(cacheItem,
                    new CacheItemPolicy
                    {
                        AbsoluteExpiration =
                            DateTimeOffset.Now + TimeSpan.FromSeconds(ttl)
                    });
        }

        public static bool Contains(List<DnsQuestion> question)
        {
            return MemoryCache.Default.Contains(question.First().ToString());
        }

        public static List<DnsRecordBase> Get(List<DnsQuestion> question)
        {
            return (List<DnsRecordBase>) MemoryCache.Default.Get(question.First().ToString()) ??
                   throw new InvalidOperationException();
        }
    }
}
