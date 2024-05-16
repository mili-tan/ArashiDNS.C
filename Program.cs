using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using IPAddress = System.Net.IPAddress;

// ReSharper disable FunctionNeverReturns

namespace ArashiDNS.C
{
    static class Program
    {
        public static IServiceProvider ServiceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        public static IHttpClientFactory? ClientFactory = ServiceProvider.GetService<IHttpClientFactory>();
        public static Uri ServerUrl = new("https://1.0.0.1/dns-query");
        public static Uri BackupServerUrl = new("https://dns.quad9.net/dns-query");
        public static TimeSpan Timeout = TimeSpan.FromMilliseconds(3000);
        public static Version TargetHttpVersion = new(3,0);
        public static HttpVersionPolicy TargetVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        public static IPAddress EcsAddress = IPAddress.Any;
        public static bool UseCache = true;
        public static bool UseEcs = true;
        public static bool UseLog;
        public static DomainName ServerDomain = DomainName.Parse("1.0.0.1");
        public static DomainName BackupServerDomain = DomainName.Parse("dns.quad9.net");
        public static IPAddress StartupDnsAddress = IPAddress.Parse("8.8.8.8");
        public static IPAddress LanDnsAddress = IPAddress.Parse("8.8.8.8");
        public static IPEndPoint ListenerEndPoint = new(IPAddress.Loopback, 15353);

        public static List<DomainName> ReverseLanDomains = new()
        {
            DomainName.Parse("in-addr.arpa"),
            DomainName.Parse("lan"), DomainName.Parse("home"),
            DomainName.Parse("corp"), DomainName.Parse("local"),
            DomainName.Parse("private"), DomainName.Parse("intranet"),
            DomainName.Parse("localhost"), DomainName.Parse("internal")
        };

        private static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "ArashiDNS.C",
                Description = "ArashiDNS.C - The super easy way DNS over HTTPS Client" +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MPL License"
            };
            cmd.HelpOption("-?|-h|--help");
            var isZh = Thread.CurrentThread.CurrentCulture.Name.Contains("zh");
            var urlArgument = cmd.Argument("target",
                isZh ? "目标 DNS over HTTPS 服务器 URL。" : "Target DNS over HTTPS service URL");
            var urlBackupArgument = cmd.Argument("backup",
                isZh ? "备份 DNS over HTTPS 服务器 URL。" : "Backup DNS over HTTPS service URL");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>",
                isZh ? "监听的地址与端口。" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>",
                isZh ? "等待回复的超时时间(毫秒)。" : "Timeout time to wait for reply", CommandOptionType.SingleValue);
            var nOption = cmd.Option("-n", isZh ? "不使用内置缓存。" : "Do not use embedded cache",
                CommandOptionType.NoValue);
            var eOption = cmd.Option("-e", isZh ? "不添加 EDNS Client Subnet。" : "Do not add EDNS Client Subnet",
                CommandOptionType.NoValue);
            var h2Option = cmd.Option("-h2", isZh ? "强制使用 HTTP/2。" : "Force HTTP/2",
                CommandOptionType.NoValue);
            var h3Option = cmd.Option("-h3",
                isZh ? "强制使用 HTTP/3。(需要 libmsquic 库)" : "Force HTTP/3 (requires libmsquic)",
                CommandOptionType.NoValue);
            var logOption = cmd.Option("-log", isZh ? "打印查询与响应日志。" : "Print query and response logs",
                CommandOptionType.NoValue);
            var ecsIpOption = cmd.Option<string>("--ecs-address <IPAddress>",
                isZh ? "强制覆盖 EDNS Client Subnet 地址。" : "Force override EDNS client subnet address", CommandOptionType.SingleValue);
            var startupDnsOption = cmd.Option<string>("--startup-dns <IPAddress>",
                isZh ? "用于解析 DoH 服务器地址的 Startup DNS 地址。" : "The startup dns address for resolving the DoH server address", CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var portUsed = PortIsUse(53);
                if (isZh) ServerUrl = new Uri("https://dns.pub/dns-query");
                if (nOption.HasValue()) UseCache = false;
                if (eOption.HasValue()) UseEcs = false;
                if (logOption.HasValue()) UseLog = true;
                if (urlArgument.HasValue) ServerUrl = new Uri(urlArgument.Value!);
                if (urlBackupArgument.HasValue) BackupServerUrl = new Uri(urlBackupArgument.Value!);
                if (wOption.HasValue()) Timeout = TimeSpan.FromMilliseconds(double.Parse(wOption.Value()!));
                if (ipOption.HasValue()) ListenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);
                else if (!portUsed) ListenerEndPoint = new IPEndPoint(IPAddress.Loopback, 53);
                else if (File.Exists("/.dockerenv") ||
                         Environment.GetEnvironmentVariables().Contains("ARASHI_RUNNING_IN_CONTAINER"))
                    ListenerEndPoint = new IPEndPoint(IPAddress.Any, 53);
                if (ListenerEndPoint.Port == 0) ListenerEndPoint.Port = portUsed ? 15353 : 53;
                if (startupDnsOption.HasValue())
                    StartupDnsAddress = IPAddress.Parse(startupDnsOption.Value() ?? "8.8.8.8");
                if (ecsIpOption.HasValue())
                    EcsAddress = IPAddress.Parse(ecsIpOption.Value() ?? "0.0.0.0");

                if (h3Option.HasValue())
                {
                    TargetVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    TargetHttpVersion = new Version(3, 0);
                }
                else if (h2Option.HasValue())
                {
                    TargetVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    TargetHttpVersion = new Version(2, 0);
                }

                if (!ecsIpOption.HasValue() && UseEcs)
                {
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.C/0.1");
                    try
                    {
                        EcsAddress = IPAddress.Parse(httpClient
                            .GetStringAsync(isZh
                                ? "https://www.cloudflare-cn.com/cdn-cgi/trace"
                                : "https://www.cloudflare.com/cdn-cgi/trace")
                            .Result.Split('\n').First(i => i.StartsWith("ip=")).Split("=").LastOrDefault()
                            ?.Trim() ?? string.Empty);
                    }
                    catch (Exception)
                    {
                        EcsAddress = IPAddress.Parse(httpClient.GetStringAsync("http://whatismyip.akamai.com/").Result);
                    }
                }

                ServerDomain = DomainName.Parse(ServerUrl.Host);
                BackupServerDomain = DomainName.Parse(BackupServerUrl.Host);
                LanDnsAddress = GetDefaultGateway() ?? IPAddress.Parse("8.8.8.8");

                var dnsServer = new DnsServer(new UdpServerTransport(ListenerEndPoint),
                    new TcpServerTransport(ListenerEndPoint));
                dnsServer.QueryReceived += ServerOnQueryReceived;
                dnsServer.Start();
                Console.WriteLine("The forwarded upstream is: " + ServerUrl);
                Console.WriteLine("The backup upstream is: " + BackupServerUrl);
                Console.WriteLine("The EDNS client subnet is: " + EcsAddress);
                Console.WriteLine("Now listening on: " + ListenerEndPoint);
                Console.WriteLine("Application started. Press Ctrl+C / q to shut down.");
                if (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    while (true)
                        if (Console.ReadKey().KeyChar == 'q')
                            Environment.Exit(0);
                }

                EventWaitHandle wait = new AutoResetEvent(false);
                while (true) wait.WaitOne();
            });

            cmd.Execute(args);
        }

        private static async Task ServerOnQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            if (e.Query is not DnsMessage query) return;
            try
            {
                if (!query.Questions.Any())
                {
                    var msg = query.CreateResponseInstance();
                    msg.ReturnCode = ReturnCode.ServerFailure;
                    e.Response = msg;
                    return;
                }

                var quest = query.Questions.First();

                if (quest.RecordType == RecordType.Any &&
                    (Equals(ListenerEndPoint.Address, IPAddress.Any) ||
                     Equals(ListenerEndPoint.Address, IPAddress.IPv6Any)))
                {
                    var msg = query.CreateResponseInstance();
                    msg.AnswerRecords.Add(
                        new HInfoRecord(quest.Name, 3600, "ANY Obsoleted", "RFC8482"));
                    e.Response = msg;
                    return;
                }

                if (quest.Name.IsEqualOrSubDomainOf(ServerDomain) ||
                    quest.Name.IsEqualOrSubDomainOf(BackupServerDomain) ||
                    quest.Name.IsEqualOrSubDomainOf(DomainName.Parse("cloudflare.com")) ||
                    quest.Name.IsEqualOrSubDomainOf(DomainName.Parse("cloudflare-cn.com")))
                {
                    e.Response = await new DnsClient(StartupDnsAddress, 1000).SendMessageAsync(query);
                    return;
                }

                if (ReverseLanDomains.Any(item => quest.Name.IsEqualOrSubDomainOf(item)))
                {
                    e.Response = await new DnsClient(LanDnsAddress, 500).SendMessageAsync(query);
                    return;
                }

                if (UseCache && DnsCache.TryGet(query, out var cacheMessage))
                {
                    e.Response = cacheMessage;
                    return;
                }

                if (UseEcs && !DnsEcs.IsEcsEnable(query))
                    query = DnsEcs.AddEcs(query, EcsAddress);

                var myResponse = await DnsOverHttpsQuery(query);

                if (UseCache) DnsCache.Add(query, myResponse);
                if (UseLog) await Task.Run(() => PrintDnsMessage(myResponse));

                e.Response = myResponse;
            }
            catch (Exception exception)
            {
                Console.Write(exception);

                var response = query.CreateResponseInstance();
                response.ReturnCode = ReturnCode.ServerFailure;
                e.Response = response;
            }
        }

        public static bool PortIsUse(int port)
        {
            try
            {
                var ipEndPointsTcp = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                var ipEndPointsUdp = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();

                return ipEndPointsTcp.Any(endPoint => endPoint.Port == port)
                       || ipEndPointsUdp.Any(endPoint => endPoint.Port == port);
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static HttpClient CreateHttpClient(string name = "doh")
        {
            var client = ClientFactory!.CreateClient(name);
            client.DefaultRequestVersion = TargetHttpVersion;
            client.DefaultVersionPolicy = TargetVersionPolicy;
            client.Timeout = Timeout;
            client.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.C/0.1");
            client.DefaultRequestHeaders.Add("Accept", "application/dns-message");
            return client;
        }

        public static void PrintDnsMessage(DnsMessage message)
        {
            Console.Write($"Q: {message.Questions.FirstOrDefault()} ");
            Console.Write($"R: {message.ReturnCode} ");
            foreach (var item in message.AnswerRecords) Console.Write($" A:{item} ");
            Console.Write(Environment.NewLine);
        }

        public static async Task<DnsMessage> DnsOverHttpsQuery(DnsMessage query)
        {
            try
            {
                var queryData = query.Encode().ToArraySegment(false).ToArray();
                var dnsStr = Convert.ToBase64String(queryData).TrimEnd('=')
                    .Replace('+', '-').Replace('/', '_');

                DnsMessage dohResponse;
                try
                {
                    dohResponse = DnsMessage.Parse(
                        await CreateHttpClient().GetByteArrayAsync($"{ServerUrl.ToString()}?ct=application/dns-message&dns={dnsStr}"));
                    if (dohResponse.ReturnCode is not (ReturnCode.NoError or ReturnCode.NxDomain))
                        throw new Exception("ReturnCode Exception " + dohResponse.ReturnCode);
                }
                catch (Exception e)
                {
                    Console.WriteLine("E:" + e.Message);
                    dohResponse = DnsMessage.Parse(
                        await CreateHttpClient().GetByteArrayAsync($"{BackupServerUrl.ToString()}?ct=application/dns-message&dns={dnsStr}"));
                }

                var response = query.CreateResponseInstance();
                response.AnswerRecords.AddRange(dohResponse.AnswerRecords);
                response.ReturnCode = dohResponse.ReturnCode;
                response.IsRecursionDesired = true;
                response.IsRecursionDesired = true;
                return response;
            }
            catch (HttpRequestException requestException)
            {
                if (requestException.InnerException is HttpProtocolException)
                {
                    TargetHttpVersion = new Version(2, 0);
                    TargetVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                }
                else Console.WriteLine(requestException);

                var response = query.CreateResponseInstance();
                response.ReturnCode = ReturnCode.ServerFailure;
                return response;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                var response = query.CreateResponseInstance();
                response.ReturnCode = ReturnCode.ServerFailure;
                return response;
            }
        }

        public static IPAddress? GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address)
                .Where(a => a is {AddressFamily: AddressFamily.InterNetwork})
                // .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
                .FirstOrDefault(_ => true);
        }
    }
}