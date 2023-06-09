﻿using System.Net;
using System.Net.NetworkInformation;
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
        public static string DohUrl = "https://dns.cloudflare.com/dns-query";
        public static string BackupDohUrl = "https://dns.quad9.net/dns-query";
        public static TimeSpan Timeout = TimeSpan.FromMilliseconds(3000);
        public static Version TargetHttpVersion = new(3,0);
        public static HttpVersionPolicy TargetVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        public static IPAddress EcsAddress = IPAddress.Any;
        public static bool UseCache = true;
        public static bool UseEcs = true;
        public static bool UseLog = false;

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

            cmd.OnExecute(() =>
            {
                var listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 15353);
                var listenerCount = Environment.ProcessorCount * 2;

                if (nOption.HasValue()) UseCache = false;
                if (eOption.HasValue()) UseEcs = false;
                if (logOption.HasValue()) UseLog = true;
                if (urlArgument.HasValue) DohUrl = urlArgument.Value!;
                if (urlBackupArgument.HasValue) BackupDohUrl = urlBackupArgument.Value!;
                if (wOption.HasValue()) Timeout = TimeSpan.FromMilliseconds(double.Parse(wOption.Value()!));
                if (ipOption.HasValue()) listenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);
                else if (!PortIsUse(53)) listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 53);
                else if (File.Exists("/.dockerenv") ||
                         Environment.GetEnvironmentVariables().Contains("ARASHI_RUNNING_IN_CONTAINER"))
                    listenerEndPoint = new IPEndPoint(IPAddress.Any, 53);

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

                var dnsServer = new DnsServer(listenerEndPoint.Address, listenerCount, listenerCount,
                    listenerEndPoint.Port);
                dnsServer.QueryReceived += ServerOnQueryReceived;
                dnsServer.Start();
                Console.WriteLine("The forwarded upstream is: " + DohUrl);
                Console.WriteLine("The backup upstream is: " + BackupDohUrl);
                Console.WriteLine("Now listening on: " + listenerEndPoint);
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
                if (UseCache && DnsCacheMatch(query, out var cacheMessage))
                {
                    e.Response = cacheMessage;
                    return;
                }

                if (UseEcs && !DnsEcs.IsEcsEnable(query))
                    query = DnsEcs.AddEcs(query, EcsAddress);

                var dohResponse = await DnsMessageQuery(query);

                if (UseCache && dohResponse.ReturnCode == ReturnCode.NoError)
                    DnsCache.Add(query.Questions, dohResponse.AnswerRecords);
                if (UseLog) await Task.Run(() => PrintDnsMessage(dohResponse));

                e.Response = dohResponse;
            }
            catch (Exception exception)
            {
                Console.Write(Environment.NewLine);

                if (exception.InnerException is HttpProtocolException)
                    TargetHttpVersion = new Version(2, 0);
                else Console.WriteLine(exception);

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
            return client;
        }

        public static void PrintDnsMessage(DnsMessage message)
        {
            Console.Write($"Q: {message.Questions.FirstOrDefault()} ");
            Console.Write($"R: {message.ReturnCode} ");
            foreach (var item in message.AnswerRecords) Console.Write($" A:{item} ");
            Console.Write(Environment.NewLine);
        }

        public static bool DnsCacheMatch(DnsMessage query, out DnsMessage message)
        {
            var contains = DnsCache.Contains(query.Questions);
            message = query.CreateResponseInstance();
            if (contains) message.AnswerRecords.AddRange(DnsCache.Get(query.Questions));
            return contains;
        }

        public static async Task<DnsMessage> DnsMessageQuery(DnsMessage query)
        {
            query.Encode(false, out var queryData);
            var dnsStr = Convert.ToBase64String(queryData).TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');

            var response = query.CreateResponseInstance();
            DnsMessage dohResponse;
            try
            {
                dohResponse = DnsMessage.Parse(
                    await CreateHttpClient().GetByteArrayAsync($"{DohUrl}?ct=application/dns-message&dns={dnsStr}"));
                if (dohResponse.ReturnCode is not (ReturnCode.NoError or ReturnCode.NxDomain))
                    throw new Exception("ReturnCode Exception " + response.ReturnCode);
            }
            catch (Exception e)
            {
                Console.WriteLine("E:" + e.Message);
                dohResponse = DnsMessage.Parse(
                    await CreateHttpClient().GetByteArrayAsync($"{BackupDohUrl}?ct=application/dns-message&dns={dnsStr}"));
            }

            response.AnswerRecords.AddRange(dohResponse.AnswerRecords);
            response.ReturnCode = dohResponse.ReturnCode;
            return response;
        }
    }
}