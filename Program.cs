using System.Net;
using System.Net.NetworkInformation;
using ARSoft.Tools.Net.Dns;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Utilities.Net;
using IPAddress = System.Net.IPAddress;

// ReSharper disable FunctionNeverReturns

namespace ArashiDNS.C
{
    static class Program
    {
        public static IServiceProvider ServiceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        public static IHttpClientFactory? ClientFactory = ServiceProvider.GetService<IHttpClientFactory>();
        public static string DohUrl = "https://dns.cloudflare.com/dns-query";
        public static TimeSpan Timeout = TimeSpan.FromMilliseconds(3000);
        public static Version MyHttpVersion = new(3,0);
        public static IPAddress EcsAddress = IPAddress.Any;
        public static bool UseCache = true;
        public static bool UseEcs = true;

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
            var urlArgument = cmd.Argument("url", isZh ? "目标 DNS over HTTPS 服务器 URL。" : "Target DNS over HTTPS service URL");
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>",
                isZh ? "监听的地址与端口。" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>",
                isZh ? "等待回复的超时时间(毫秒)。" : "Timeout time to wait for reply", CommandOptionType.SingleValue);
            var nOption = cmd.Option("-n", isZh ? "不使用内置缓存。" : "Do not use embedded cache",
                CommandOptionType.NoValue);
            var eOption = cmd.Option("-e", isZh ? "不添加 EDNS Client Subnet。" : "Do not add EDNS Client Subnet",
                CommandOptionType.NoValue);

            cmd.OnExecute(() =>
            {
                var listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 15353);
                var listenerCount = Environment.ProcessorCount * 2;

                if (nOption.HasValue()) UseCache = false;
                if (eOption.HasValue()) UseEcs = false;
                if (urlArgument.HasValue) DohUrl = urlArgument.Value!;
                if (wOption.HasValue()) Timeout = TimeSpan.FromMilliseconds(double.Parse(wOption.Value()!));
                if (ipOption.HasValue()) listenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);
                else if (!PortIsUse(53)) listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 53);

                if (UseEcs)
                {
                    using var httpClient = new HttpClient {DefaultRequestHeaders = {{"User-Agent", "ArashiDNS.C/0.1"}}};
                    try
                    {
                        EcsAddress = IPAddress.Parse(httpClient.GetStringAsync("https://ip.mili.one/").Result);
                    }
                    catch (Exception)
                    {
                        EcsAddress = IPAddress.Parse(httpClient.GetStringAsync("http://whatismyip.akamai.com/").Result);
                    }
                }

                var dnsServer = new DnsServer(listenerEndPoint.Address, listenerCount, listenerCount, listenerEndPoint.Port);
                dnsServer.QueryReceived += ServerOnQueryReceived;
                dnsServer.Start();
                Console.WriteLine("The forwarded upstream is: " + DohUrl);
                Console.WriteLine("Now listening on: " + listenerEndPoint);
                Console.WriteLine("Application started. Press Ctrl+C / q to shut down.");
                if (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    while (true)
                        if (Console.ReadKey().KeyChar == 'q')
                            Environment.Exit(0);
                }
                EventWaitHandle wait = new AutoResetEvent(false);
                while(true) wait.WaitOne();
            });
            
            cmd.Execute(args);
        }

        private static async Task ServerOnQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            if (e.Query is not DnsMessage query) return;
            try
            {
                var response = query.CreateResponseInstance();

                if (UseCache && DnsCache.Contains(query.Questions))
                {
                    response.AnswerRecords.AddRange(DnsCache.Get(query.Questions));
                    e.Response = response;
                    return;
                }
                if (UseEcs && !DnsEcs.IsEcsEnable(query)) query = DnsEcs.AddEcs(query, EcsAddress);

                query.Encode(false, out var queryData);
                var dnsStr = Convert.ToBase64String(queryData).TrimEnd('=')
                    .Replace('+', '-').Replace('/', '_');

                var client = ClientFactory!.CreateClient("doh");
                client.DefaultRequestVersion = MyHttpVersion;
                client.Timeout = Timeout;
                client.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.C/0.1");

                var httpResponse = await client.GetAsync($"{DohUrl}?dns={dnsStr}");
                var dnsResponse = DnsMessage.Parse(await httpResponse.Content.ReadAsByteArrayAsync());
                response.AnswerRecords.AddRange(dnsResponse.AnswerRecords);

                if (UseCache && httpResponse.IsSuccessStatusCode && dnsResponse.ReturnCode == ReturnCode.NoError)
                    DnsCache.Add(query.Questions, dnsResponse.AnswerRecords);

                e.Response = response;
            }
            catch (Exception exception)
            {
                if (exception.InnerException is HttpProtocolException)
                    MyHttpVersion = new Version(2, 0);
                else
                    Console.WriteLine(exception);

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
    }
}