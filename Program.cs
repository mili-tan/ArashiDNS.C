using System.Net;
using ARSoft.Tools.Net.Dns;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable FunctionNeverReturns

namespace ArashiDNS.C
{
    static class Program
    {
        public static IServiceProvider ServiceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        public static IHttpClientFactory? ClientFactory = ServiceProvider.GetService<IHttpClientFactory>();
        public static string DohUrl = "https://arashi.eu.org/dns-query";
        public static TimeSpan Timeout = TimeSpan.FromMilliseconds(3000);
        public static Version MyHttpVersion = new(3,0);

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
                isZh ? "监听的地址与端口" : "Set server listening address and port",
                CommandOptionType.SingleValue);
            var wOption = cmd.Option<int>("-w <timeout>",
                isZh ? "等待回复的超时时间(毫秒)。" : "Timeout time to wait for reply", CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                var listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 15353);
                var listenerCount = Environment.ProcessorCount * 2;

                if (urlArgument.HasValue) DohUrl = urlArgument.Value!;
                if (ipOption.HasValue()) listenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);
                if (wOption.HasValue()) Timeout = TimeSpan.FromMilliseconds(double.Parse(wOption.Value()!));

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
                query.Encode(false, out var queryData);
                var dnsStr = Convert.ToBase64String(queryData).TrimEnd('=')
                    .Replace('+', '-').Replace('/', '_');

                var client = ClientFactory!.CreateClient("doh");
                client.DefaultRequestVersion = MyHttpVersion;
                client.Timeout = Timeout;
                client.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.C/0.1");

                var httpResponse = await client.GetAsync($"{DohUrl}?dns={dnsStr}");
                response.AnswerRecords.AddRange(DnsMessage
                    .Parse(await httpResponse.Content.ReadAsByteArrayAsync())
                    .AnswerRecords);

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
    }
}