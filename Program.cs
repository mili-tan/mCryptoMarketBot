using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace CryptoMarketBot
{
    class Program
    {
        private static TelegramBotClient BotClient;
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        private static readonly WebProxy MWebProxy = new WebProxy("127.0.0.1", 7890);

        static void Main(string[] args)
        {
            Console.WriteLine("CryptoMarket Bot");
            string tokenStr;
            if (File.Exists(SetupBasePath + "token.text"))
                tokenStr = File.ReadAllText(SetupBasePath + "token.text");
            else if (!string.IsNullOrWhiteSpace(string.Join("", args)))
                tokenStr = string.Join("http_proxy", MWebProxy.Address.DnsSafeHost);
            else
            {
                Console.WriteLine("Token:");
                tokenStr = Console.ReadLine();
            }

            BotClient = new TelegramBotClient(tokenStr, MWebProxy);

            Console.Title = "Bot:@" + BotClient.GetMeAsync().Result.Username;
            Console.WriteLine($"@{BotClient.GetMeAsync().Result.Username} : Connected");

            BotClient.OnMessage += (sender, eventArgs) =>
            {
                var message = eventArgs.Message;
                if (message == null || message.Type != MessageType.Text || message.Text.Contains("/start")) return;
                Console.WriteLine($"@{message.From.Username}: " + message.Text);

                Task.Run(() =>
                {
                    var waitMessage = BotClient.SendTextMessageAsync(message.Chat.Id, "请稍等…",
                        replyToMessageId: message.MessageId).Result;
                    try
                    {
                        var sym = message.Text.Split(' ', '-', ':').LastOrDefault();
                        if (sym.Contains("/") || sym.Contains("\\")) return;
                        BotClient.SendTextMessageAsync(message.Chat.Id,
                            JObject.Parse(GetQuotes(sym, "USD")).ToString());
                        BotClient.DeleteMessageAsync(message.Chat.Id, waitMessage.MessageId);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        BotClient.SendTextMessageAsync(message.Chat.Id, "获取数据失败，请稍后重试",
                            replyToMessageId: message.MessageId);
                        BotClient.DeleteMessageAsync(message.Chat.Id, waitMessage.MessageId);
                    }
                });
            };

            BotClient.StartReceiving(Array.Empty<UpdateType>());
            while (true)
            {
                if (Console.ReadLine() != "exit") continue;
                BotClient.StopReceiving();
            }

            // ReSharper disable once FunctionNeverReturns
        }

        static string GetQuotes(string symbol, string convert)
        {
            var client = new WebClient();
            return client.DownloadString("https://web-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest" +
                                         $"?symbol={symbol}&convert={convert}");
        }
    }
}

