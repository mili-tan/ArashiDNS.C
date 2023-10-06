using System.Runtime.Caching;
using ARSoft.Tools.Net.Dns;

namespace ArashiDNS
{
    public class DnsCache
    {
        public static void Add(DnsMessage qMessage, DnsMessage aMessage)
        {
            var question = qMessage.Questions;
            var answerRecords = aMessage.AnswerRecords;
            var ttl = answerRecords.First().TimeToLive;
            Add(new CacheItem(question.First().ToString(), (answerRecords, aMessage.ReturnCode)), ttl);
        }

        public static void Add(List<DnsQuestion> question, List<DnsRecordBase> answerRecords, ReturnCode rCode)
        {
            var ttl = answerRecords.First().TimeToLive;
            Add(new CacheItem(question.First().ToString(), (answerRecords, rCode)), ttl);
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

        public static (ReturnCode, List<DnsRecordBase>) Get(List<DnsQuestion> question)
        {
            return ((ReturnCode, List<DnsRecordBase>)) MemoryCache.Default.Get(question.First().ToString());
        }

        public static bool TryGet(DnsMessage query, out DnsMessage message)
        {
            var contains = Contains(query.Questions);
            message = query.CreateResponseInstance();
            if (!contains) return contains;
            var get = Get(query.Questions);
            message.ReturnCode = get.Item1;
            message.AnswerRecords.AddRange(get.Item2);
            return contains;
        }
    }
}
