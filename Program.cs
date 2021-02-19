using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Chinese;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace CryptoMarketBot
{
    static class Program
    {
        private static TelegramBotClient BotClient;
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        private static readonly WebProxy MWebProxy = new WebProxy("127.0.0.1", 7890);

        static void Main(string[] args)
        {
            Console.WriteLine("mCryptoMarket Bot");
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

                        var date = JObject.Parse(GetQuotes(sym, "USD")).GetValue("data").FirstOrDefault().FirstOrDefault();
                        var quote = date["quote"].FirstOrDefault().FirstOrDefault();
                        var text = $"{date["name"]} #{date["cmc_rank"]} - {date["symbol"]}";
                        var price = quote["price"].ToObject<double>();
                        var priceStr = price > 1000 ? price.ToString("0") :
                            price > 10 ? price.ToString("0.0") : price.ToString("0.0000");
                        try
                        {
                            text += $" [{date["platform"]["name"]}]";
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        text += Environment.NewLine;
                        text += $"当前价格：${priceStr} USD" + Environment.NewLine;
                        text += "-----流通量-----" + Environment.NewLine;
                        try
                        {
                            text += $"供应总量：{GetCnNumber(date["max_supply"])}" + Environment.NewLine;
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                        if (date["total_supply"].GetCnNumber() != date["circulating_supply"].GetCnNumber())
                            text += $"发行总量：{date["total_supply"].GetCnNumber()}" + Environment.NewLine;
                        text += $"流通总量：{date["circulating_supply"].GetCnNumber()}" + Environment.NewLine;

                        text += "-----成交量-----" + Environment.NewLine;
                        text += $"当前市值：{quote["market_cap"].GetCnNumber()} 美金" + Environment.NewLine;
                        text += $"日成交额：{quote["volume_24h"].GetCnNumber()} 美金" + Environment.NewLine;

                        text += "-----涨跌幅-----" + Environment.NewLine;
                        text += $"1时：{quote["percent_change_1h"].GetEmojiNumber()}%" + Environment.NewLine;
                        text += $"1日：{quote["percent_change_24h"].GetEmojiNumber()}%" + Environment.NewLine;
                        text += $"1周：{quote["percent_change_7d"].GetEmojiNumber()}%" + Environment.NewLine;
                        text += $"1月：{quote["percent_change_30d"].GetEmojiNumber()}%" + Environment.NewLine;
                        text += $"更新时间：{quote["last_updated"].ToObject<DateTime>()}" + Environment.NewLine;

                        BotClient.SendTextMessageAsync(message.Chat.Id,text);
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
                GC.Collect();
            };

            BotClient.StartReceiving(Array.Empty<UpdateType>());
            while (true)
            {
                if (Console.ReadLine() != "exit") continue;
                BotClient.StopReceiving();
            }

            // ReSharper disable once FunctionNeverReturns
        }

        static string GetQuotes(string symbol, string convert) =>
            new WebClient().DownloadString("https://web-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest" +
                                           $"?symbol={symbol}&convert={convert}");

        static string GetCnNumber(this JToken token) =>
            ChineseNumber.GetString(token.ToObject<long>() > 100000000
                ? token.ToObject<long>() / 100000000 * 100000000
                : token.ToObject<long>() > 1000000
                    ? token.ToObject<long>() / 1000000 * 1000000
                    : token.ToObject<long>() / 10000 * 10000).TrimEnd('零');

        static string GetEmojiNumber(this JToken token)
        {
            var num = token.ToObject<double>().ToString("+#0.00;-#0.00;0");
            return $" {(num.StartsWith("+") ? "📈" : "📉")} {num}";
        }
    }
}

