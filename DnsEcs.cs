using System.Net;
using ARSoft.Tools.Net.Dns;

namespace ArashiDNS.C
{
    internal class DnsEcs
    {
        public static bool IsEcsEnable(DnsMessage msg)
        {
            return msg.IsEDnsEnabled && msg.EDnsOptions.Options.ToArray().OfType<ClientSubnetOption>().Any();
        }

        public static DnsMessage AddEcs(DnsMessage msg, IPAddress ipAddress)
        {
            var ipByte = ipAddress.GetAddressBytes();
            ipByte[3] = 0;
            if (!msg.IsEDnsEnabled) msg.IsEDnsEnabled = true;
            msg.EDnsOptions.Options.Add(new ClientSubnetOption(24, new IPAddress(ipByte)));
            return msg;
        }
    }
}
