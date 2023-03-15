using System.Reflection;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace ArashiDNS
{
    public static class DnsEncoder
    {
        private static MethodInfo? info;

        public static void Init()
        {
            var methods = new DnsMessage().GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            info = methods[5].ToString() == "Int32 Encode(Boolean, Byte[] ByRef)"
                ? methods[5]
                : methods.FirstOrDefault(i => i.ToString()!.Equals("Int32 Encode(Boolean, Byte[] ByRef)"));
        }

        public static byte[] Encode(DnsMessage dnsQMsg)
        {
            if (info == null) Init();
            dnsQMsg.IsRecursionAllowed = true;
            dnsQMsg.IsRecursionDesired = true;
            dnsQMsg.TransactionID = Convert.ToUInt16(new Random(DateTime.Now.Millisecond).Next(1, 10));
            var args = new object[] { false, null };
            info.Invoke(dnsQMsg, args);
            return args[1] as byte[];
        }
    }
}
