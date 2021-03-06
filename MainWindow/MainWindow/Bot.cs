﻿using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json;
using System.Data.SQLite;
using System.Data.Common;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using Telegram.Bot.Types.InputFiles;

namespace MainWindow
{
    class Bot
    {
        public string _key { get; private set; }
        public TelegramBotClient _bot { get; private set; }
        public Telegram.Bot.Types.User Data { get; private set; }
        public Dictionary<long, Chat> chats { get; private set; }
        public Dictionary<long, ChatUser> users { get; private set; }
        private static Dictionary<List<string>, Dictionary<string, Action>> commands = new Dictionary<List<string>, Dictionary<string, Action>>() { };
        private static List<string> param = null;
        private static Telegram.Bot.Types.User from = null;
        private static Telegram.Bot.Types.Chat chat = null;
        private static ChatUser user = null;
        Message message = null;
        private static CommandController cController = null;
        private static Dictionary<long, decimal> threshold = new Dictionary<long, decimal>();

        private static Random rnd = new Random();

        public Bot(string key)
        {
            InitCommands();
            Start(key);
            Roulette.Init(this);
            //new SteamFree(239192404, this); //Insert your Channel-ID
            //new Thread(() => { CheckHOT(); }).Start();
        }

        private void CheckHOT()
        {
            while (true)
            {
                if (chats != null)
                {
                    Dictionary<string, decimal> value = GetDailyPrice("HOT", "BTC", true);
                    foreach (long tmpChatID in chats.Keys)
                    {
                        if (threshold.ContainsKey(tmpChatID))
                        {
                            if (value["HOT"] >= threshold[tmpChatID])
                            {
                                SendMessage(tmpChatID, value["HOT"].ToString() + " >= Threshold (" + threshold[tmpChatID] + ")");
                            }
                        }
                        else
                        {
                            SendMessage(tmpChatID, "No Threshold: " + value["HOT"].ToString());
                        }
                    }
                    Thread.Sleep(5000);
                }
            }
        }
        private void SetThreshold()
        {
            long chatID = chat.Id;
            if (param.Count > 0)
            {
                decimal newThreshold = decimal.Parse(param[0]);
                if (threshold.ContainsKey(chatID))
                {
                    threshold[chatID] = newThreshold;
                }
                else
                {
                    threshold.Add(chatID, newThreshold);
                }
            }
        }
        public void Init()
        {
            //RefreshChats();
            //RefreshUser();
        }
        private long LongRandom(long min, long max)
        {
            Random rand = new Random();
            long result = rand.Next((Int32)(min >> 32), (Int32)(max >> 32));
            result = (result << 32);
            result = result | (long)rand.Next((Int32)min, (Int32)max);
            return result;
        }
        internal async void RemoveKeyboard(long chatID, string msg, bool disableNotification)
        {
            var removeKeyboard = new ReplyKeyboardRemove();
            await _bot.SendTextMessageAsync(chatID, msg, disableNotification: disableNotification, replyMarkup: removeKeyboard);
        }
        private void InitCommands()
        {
            InitSingleCommand(new string[] { "threshold" }, "Admin only", SetThreshold);

            InitSingleCommand(new string[] { "invest" }, "Fügt der deinem User die gegebene Anzahl (in €) als Invest hinzu.", AddInvest);
            InitSingleCommand(new string[] { "xrp" }, "Beispiel: /xrp buy 90, /xrp sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an Ripple mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/xrp' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an Ripple aus ihrem Bestand entfernt.", () => ManageCoins("XRP"));
            InitSingleCommand(new string[] { "trx" }, "Beispiel: /trx buy 90, /trx sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an TRON mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/trx' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an TRON aus ihrem Bestand entfernt.", () => ManageCoins("TRX"));
            InitSingleCommand(new string[] { "ada" }, "Beispiel: /ada buy 90, /ada sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an Cardano mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/ada' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an Cardano aus ihrem Bestand entfernt.", () => ManageCoins("ADA"));
            InitSingleCommand(new string[] { "iota" }, "Beispiel: /iota buy 90, /iota sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an IOTA mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/iota' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an IOTA aus ihrem Bestand entfernt.", () => ManageCoins("IOTA"));
            InitSingleCommand(new string[] { "xlm" }, "Beispiel: /xlm buy 90, /xlm sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an Stellar mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/xlm' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an Stellar aus ihrem Bestand entfernt.", () => ManageCoins("XLM"));
            InitSingleCommand(new string[] { "npxs" }, "Beispiel: /npxs buy 90, /npxs sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an Stellar mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/npxs' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an Pundi X aus ihrem Bestand entfernt.", () => ManageCoins("NPXS"));
            InitSingleCommand(new string[] { "hot" }, "Beispiel: /hot buy 90, /npxs sell 90" + Environment.NewLine + "Mit 'buy' wird die angegebene Anzahl an Stellar mit dem aktuellen Marktkurs in eine Datenbank eingetragen, damit diese per '/hot' angezeigt werden können." + Environment.NewLine + "Mit 'sell' wird die angegebene Anzahl an HOLO aus ihrem Bestand entfernt.", () => ManageCoins("HOT"));
            InitSingleCommand(new string[] { "coins", "coin", "profit", "balance" }, "Gibt den Profit für alle vom Bot unterstützten Coins aus.", GetAllProfit);
            InitSingleCommand(new string[] { "supported", "supportedcoin", "supportedcoins" }, "Zeigt eine Liste aller vom Bot unterstützten Cryptocoins an.", ShowSupportedCoins);

            InitSingleCommand(new string[] { "roulette" }, "Eröffnet bzw. nimmt an einem neuen Roulettespiel teil.", RouletteHandler);
            InitSingleCommand(new string[] { "shoot", "shot" }, "Wenn du in einem Roulettespiel bist, kannst du hiermit deinen Schuss tätigen.", ShootHandler);
            InitSingleCommand(new string[] { "abort", "cancel", "bittestophabibi" }, "Stopt eine vorhandene Rouletterunde (Nur für den Spielersteller)", StopRoulette);

            InitSingleCommand(new string[] { "dhl" }, "DHL Paketverfolgung durch eingabe der Tracking-ID." + Environment.NewLine + "Beispiel: /dhl JJ123456789005456" + Environment.NewLine + "Beschreibung: Gibt den letzten Status des DHL-Paket zurück.", DHLTrack);
            InitSingleCommand(new string[] { "hermes" }, "Hermes Paketverfolgung durch eingabe der Tracking-ID." + Environment.NewLine + "Beispiel: /hermes JJ123456789005456" + Environment.NewLine + "Beschreibung: Gibt den letzten Status des Hermes-Paket zurück.", HermesTrack);

            InitSingleCommand(new string[] { "qwertee" }, "Zeigt dir die aktuellen 3 Tees an.", ShowTees);
            InitSingleCommand(new string[] { "fakt" }, "Zeigt dir die Wahrheit an.", ShowFact);

            InitSingleCommand(new string[] { "rtd", "dice", "rool", "random" }, "Gibt eine zufällige Zahl zwischen 2 angegebenen Zahlen zurück." + Environment.NewLine + "Beispiel: /random 1 500" + Environment.NewLine + "Beschreibung: Gibt eine Zahl zwischen '1' und '500' zurück.", Random);
            InitSingleCommand(new string[] { "kawaii" }, "Lass den Bot entscheiden wie Kawaii du wirklich bist." + Environment.NewLine + "Beispiel: /kawaii", KawaiiMeter);
            InitSingleCommand(new string[] { "bent" }, "Jeder kennt es.. Man hasst Bent so sehr, dass man sich fragt:" + Environment.NewLine + "Wie sehr hasse ich ihn heute eigentlich?" + Environment.NewLine + Environment.NewLine + "Damit ist ab heute Schluss! Lass dir jetzt vom Bot ausgeben wie sehr du Bent heute hasst.", BentHate);
            InitSingleCommand(new string[] { "bryan" }, "Jeder kennt es.. Man hasst Bryan so sehr, dass man sich fragt:" + Environment.NewLine + "Wie sehr hasse ich ihn heute eigentlich?" + Environment.NewLine + Environment.NewLine + "Damit ist ab heute Schluss! Lass dir jetzt vom Bot ausgeben wie sehr du Bryan heute hasst.", BryanHate);
            InitSingleCommand(new string[] { "simon" }, "Jeder kennt es.. Man hasst Simon so sehr, dass man sich fragt:" + Environment.NewLine + "Wie sehr hasse ich ihn heute eigentlich?" + Environment.NewLine + Environment.NewLine + "Damit ist ab heute Schluss! Lass dir jetzt vom Bot ausgeben wie sehr du Simon heute hasst.", SimonHate);
            InitSingleCommand(new string[] { "arne" }, "Jeder kennt es.. Man hasst Simon so sehr, dass man sich fragt:" + Environment.NewLine + "Wie sehr hasse ich ihn heute eigentlich?" + Environment.NewLine + Environment.NewLine + "Damit ist ab heute Schluss! Lass dir jetzt vom Bot ausgeben wie sehr du Simon heute hasst.", ArneHate);
            InitSingleCommand(new string[] { "pascal" }, "Jeder kennt es.. Man hasst Simon so sehr, dass man sich fragt:" + Environment.NewLine + "Wie sehr hasse ich ihn heute eigentlich?" + Environment.NewLine + Environment.NewLine + "Damit ist ab heute Schluss! Lass dir jetzt vom Bot ausgeben wie sehr du Simon heute hasst.", PascalHate);
            InitSingleCommand(new string[] { "hate", "hateall", "allhate" }, "Jeder kennt es.. Man hasst die gesamte Gruppe so sehr, dass man sich fragt:" + Environment.NewLine + "Wie sehr hasse ich euch heute eigentlich?" + Environment.NewLine + Environment.NewLine + "Damit ist ab heute Schluss! Lass dir jetzt vom Bot ausgeben wie sehr du deine Gruppe heute hasst.", AllHate);

            InitSingleCommand(new string[] { "jing" }, "Zeigt den heutigen Mittagstisch von Jing-Jai.", GetFoodJingJai);
            InitSingleCommand(new string[] { "police", "polizei", "pol" }, "Zeigt aktuelle Presseinformationen der gewünschten Stadt an." + Environment.NewLine + "Beispiel: /police bremen" + Environment.NewLine + "Beschreibung: Gibt die aktuellste Nachricht der Polizeipresse für die Stadt 'bremen' zurück.", GetPoliceNews);
            InitSingleCommand(new string[] { "register" }, "Registriert einen Chat permanent beim Bot.", RegisterChat);

            InitSingleCommand(new string[] { "nsa", "spy" }, "Geheime Funktionen", ExportAllMessages);
            cController = new CommandController(ref commands);
        }

        private void ExportAllMessages()
        {
            if (!isAdmin(user._user.Id))
            {
                return;
            }


        }

        private void InitSingleCommand(string[] cmd, string description, Action method)
        {
            List<string> cmds = new List<string>();
            cmds.AddRange(cmd);

            Dictionary<string, Action> body = new Dictionary<string, Action>() { { description, method } };

            commands.Add(cmds, body);
        }
        private void ShowFact()
        {
            long chatID = chat.Id;
            SendMessage(chatID, "Pasquale ist 350€ mehr Wert als Yanniv!");
        }
        private dynamic GetHateFor(string name, ChatUser user)
        {
            dynamic ret = new System.Dynamic.ExpandoObject();
            ret.hatePercent = rnd.Next(0, 101);
            ret.hateName = name;
            ret.output = user.Username() + " hasst " + ret.hateName + " heute zu " + ret.hatePercent + "%";
            return ret;
        }

        private void AllHate()
        {
            StringBuilder strBuild = new StringBuilder();
            List<dynamic> hates = new List<dynamic>();
            hates.Add(GetHateFor("Arne", user));
            hates.Add(GetHateFor("Bent", user));
            hates.Add(GetHateFor("Bryan", user));
            hates.Add(GetHateFor("Pascal", user));
            hates.Add(GetHateFor("Simon", user));
            foreach (dynamic hate in hates.OrderByDescending(h => h.hatePercent))
            {
                strBuild.AppendLine(hate.output);
            }

            SendMessage(chat.Id, strBuild.ToString());
        }

        private void BentHate()
        {
            SendMessage(chat.Id, GetHateFor("Bent", user));
        }
        private void BryanHate()
        {
            SendMessage(chat.Id, GetHateFor("Bryan", user));
        }
        private void SimonHate()
        {
            SendMessage(chat.Id, GetHateFor("Simon", user));
        }
        private void ArneHate()
        {
            SendMessage(chat.Id, GetHateFor("Arne", user));
        }
        private void PascalHate()
        {
            SendMessage(chat.Id, GetHateFor("Pascal", user));
        }

        private void ShowTees()
        {
            long chatID = chat.Id;

            List<Tee> tees = Qwertee.LatestTees();
            foreach (Tee tee in tees)
            {
                _bot.SendPhotoAsync(chatID, new InputOnlineFile(tee.img), tee.title + " (" + tee.price + "€) - https://qwertee.com/", ParseMode.Default, true);
                System.Threading.Thread.Sleep(1000);
            }
        }

        private void GetAllProfit()
        {
            long chatID = chat.Id;
            int userID = user._user.Id;


            StringBuilder strBuild = new StringBuilder();
            decimal allProfits = 0;


            foreach (string coin in Settings.supportedCoins)
            {
                Dictionary<int, decimal> coinStats = SumCoin(coin, userID);
                decimal coinProfit = 0;
                if (coinStats != null)
                {
                    coinProfit = coinStats.Values.First();
                    int coinAmount = coinStats.Keys.First();
                    Dictionary<string, decimal> profits = GetDailyPrice(coin);
                    decimal usdDailyPrice = profits["USD"];
                    decimal eurProfit = Core.USD2EUR(coinProfit);
                    strBuild.AppendLine("<code>1 " + coin.ToUpper() + " = " + usdDailyPrice + "$ (" + coinAmount + " " + coin.ToUpper() + " = " + Math.Round(coinProfit, 2) + "$/" + eurProfit + "€)</code>");
                }
                allProfits += coinProfit;
            }

            strBuild.AppendLine("");
            decimal invest = 0;
            if (DBController.EntryExist("SELECT * FROM invest WHERE userID = '" + userID + "' LIMIT 1"))
            {
                SQLiteDataReader reader = DBController.ReturnQuery("SELECT * FROM invest WHERE userID = '" + userID + "'");
                foreach (DbDataRecord row in reader)
                {
                    invest += decimal.Parse(row["amount"].ToString());
                }

                strBuild.AppendLine("<code>Total Balance: " + Math.Round(allProfits, 2) + "$ (" + Core.USD2EUR(allProfits) + "€)</code>");
                allProfits -= Core.EUR2USD(invest);
                strBuild.AppendLine("<code>Total Invest: " + Math.Round(Core.EUR2USD(invest), 2) + "$ (" + invest + "€)</code>");
                strBuild.AppendLine("<code>Total Profits: " + Math.Round(allProfits, 2) + "$ (" + Core.USD2EUR(allProfits) + "€)</code>");

                SendMessageHTML(chatID, strBuild.ToString());
            }
            else
            {
                SendMessageHTML(chatID, "Missing invest (Add invest by typing '/invest AMOUNT') in €");
            }
        }
        private void AddInvest()
        {

            long chatID = chat.Id;
            int userID = user._user.Id;
            try
            {
                if (param.Count > 0)
                {
                    decimal amount = decimal.Parse(param[0]);
                    if (DBController.EntryExist("SELECT * FROM invest WHERE userID = '" + userID + "' LIMIT 1"))
                    {
                        SQLiteDataReader reader = DBController.ReturnQuery("SELECT * FROM invest WHERE userID = '" + userID + "'");
                        foreach (DbDataRecord row in reader)
                        {
                            amount += decimal.Parse(row["amount"].ToString());
                        }

                        DBController.ExecuteQuery("UPDATE invest SET amount = '" + Math.Round(amount, 2) + "' WHERE userID = '" + userID + "'");
                    }
                    else
                    {
                        DBController.ExecuteQuery("INSERT INTO invest (userID, amount) VALUES ('" + userID + "', '" + Math.Round(amount, 2) + "')");
                    }
                    SendMessageHTML(chatID, "Added investment of " + Math.Round(amount, 2) + "€");

                }
                else
                {
                    SendMessageHTML(chatID, "Missing amount (Add invest by typing '/invest AMOUNT' in €)");
                }
            }
            catch
            {
                SendMessageHTML(chatID, "Correct use (Add invest by typing '/invest AMOUNT' in €)");
            }

        }
        private Dictionary<int, decimal> SumCoin(string symbol, int userID)
        {
            decimal coinPrice = GetETHPrice(symbol);
            decimal ethUSD = GetETHUSD();

            if (DBController.EntryExist("SELECT * FROM " + symbol + " WHERE userID = '" + userID + "' LIMIT 1"))
            {
                SQLiteDataReader reader = DBController.ReturnQuery("SELECT * FROM " + symbol + " WHERE userID = '" + userID + "'");
                decimal sumUSD = 0;
                int amount = 0;
                foreach (DbDataRecord row in reader)
                {
                    sumUSD += (decimal.Parse(row["amount"].ToString()) * coinPrice) * ethUSD;
                    amount += int.Parse(row["amount"].ToString());
                }

                return new Dictionary<int, decimal>() { { amount, sumUSD } };
            }
            return null;
        }
        private void ShowSupportedCoins()
        {
            long chatID = chat.Id;
            int userID = user._user.Id;
            try
            {
                SendMessageHTML(chat.Id, "<code>Unterstützte Kryptowährungen" + Environment.NewLine + string.Join(", ", Settings.supportedCoins.ToArray()) + "</code>");

            }
            catch
            {
                SendMessageHTML(chat.Id, "Versuche es bitte später erneut.");
            }
        }
        private void ManageCoins(string symbol)
        {
            long chatID = chat.Id;
            int userID = user._user.Id;
            try
            {
                if (param.Count > 1)
                {
                    int coinAmount = int.Parse(param[1]);

                    switch (param[0])
                    {
                        case "add":
                        case "buy":
                            if (!DBController.EntryExist("SELECT * FROM " + symbol + " WHERE userID = '" + userID + "' LIMIT 1"))
                            {
                                DBController.ExecuteQuery("INSERT INTO " + symbol.ToLower() + "(userID, amount) VALUES ('" + userID + "', '" + coinAmount + "')");
                            }
                            else
                            {
                                SQLiteDataReader readerAdd = DBController.ReturnQuery("SELECT * FROM " + symbol + " WHERE userID = '" + userID + "'");
                                int sumCoinsAdd = 0;
                                foreach (DbDataRecord row in readerAdd)
                                {
                                    if (sumCoinsAdd != 0)
                                    {
                                        break;
                                    }
                                    sumCoinsAdd += int.Parse(row["amount"].ToString());
                                }
                                coinAmount += sumCoinsAdd;
                                DBController.ExecuteQuery("UPDATE " + symbol.ToLower() + " SET amount = '" + coinAmount + "' WHERE userID = '" + userID + "'");
                            }
                            SendMessageHTML(chatID, "Done.");
                            break;
                        case "remove":
                        case "sell":
                            SQLiteDataReader readerDel = DBController.ReturnQuery("SELECT * FROM " + symbol + " WHERE userID = '" + userID + "'");
                            int sumCoins = 0;
                            foreach (DbDataRecord row in readerDel)
                            {
                                if (sumCoins != 0)
                                {
                                    break;
                                }
                                sumCoins += int.Parse(row["amount"].ToString());
                            }
                            coinAmount = sumCoins - coinAmount;
                            if (coinAmount > 0)
                            {
                                DBController.ExecuteQuery("UPDATE " + symbol.ToLower() + " SET amount = '" + coinAmount + "' WHERE userID = '" + userID + "'");
                            }
                            else
                            {
                                DBController.ExecuteQuery("DELETE FROM " + symbol.ToLower() + " WHERE userID = '" + userID + "'");
                            }
                            SendMessageHTML(chat.Id, "Done.");
                            break;
                    }
                }
                else
                {
                    SendMessageHTML(chat.Id, "<code>Missing method, please use one of the following commands:" + Environment.NewLine + "/" + symbol.ToLower() + " buy AMOUNT" + Environment.NewLine + "/" + symbol.ToLower() + " sell AMOUNT</code>");
                }
            }
            catch
            {
                SendMessageHTML(chat.Id, "Versuche es bitte später erneut.");
            }
        }
        private Dictionary<string, decimal> GetDailyPrice(string symbol, string oppositeCoin = "ETH", bool onlyCoinValue = false)
        {
            try
            {
                string jsonChartPlain = HTTPRequester.SimpleRequest("https://www.binance.com/api/v1/ticker/allPrices");
                List<BinancePair> jsonChart = JsonConvert.DeserializeObject<List<BinancePair>>(jsonChartPlain);
                BinancePair coinETH = jsonChart.Where((s) => s.symbol == symbol.ToUpper() + oppositeCoin).First();
                if (onlyCoinValue)
                {
                    return new Dictionary<string, decimal>() { { "HOT", decimal.Parse(coinETH.price.Replace(".", ",")) } };
                }
                BinancePair ethusdt = jsonChart.Where((s) => s.symbol == oppositeCoin + "USDT").First();
                decimal usdTicker = Math.Round((decimal.Parse(coinETH.price.Replace(".", ",")) * decimal.Parse(ethusdt.price.Replace(".", ","))), 5);
                return new Dictionary<string, decimal>() { { "USD", usdTicker }, { "EUR", Core.USD2EUR(usdTicker) } };
            }
            catch
            {
                SendMessageHTML(chat.Id, "Fehler beim der Serverkommunikation.");
                return null;
            }
        }
        private decimal GetETHPrice(string symbol)
        {
            try
            {
                string jsonChartPlain = HTTPRequester.SimpleRequest("https://www.binance.com/api/v1/ticker/allPrices");
                List<BinancePair> jsonChart = JsonConvert.DeserializeObject<List<BinancePair>>(jsonChartPlain);
                BinancePair coinETH = jsonChart.Where((s) => s.symbol == symbol.ToUpper() + "ETH").First();
                return decimal.Parse(coinETH.price.Replace(".", ","));
            }
            catch
            {
                SendMessageHTML(chat.Id, "Fehler beim der Serverkommunikation.");
                return 0;
            }
        }
        private decimal GetETHUSD()
        {
            try
            {
                string jsonChartPlain = HTTPRequester.SimpleRequest("https://www.binance.com/api/v1/ticker/allPrices");
                List<BinancePair> jsonChart = JsonConvert.DeserializeObject<List<BinancePair>>(jsonChartPlain);
                BinancePair ethusdt = jsonChart.Where((s) => s.symbol == "ETHUSDT").First();
                return decimal.Parse(ethusdt.price.Replace(".", ","));
            }
            catch
            {
                SendMessageHTML(chat.Id, "Fehler beim der Serverkommunikation.");
                return 0;
            }
        }
        private void GetPoliceNews()
        {
            if (param.Count > 0)
            {
                try
                {
                    Police police = new Police(param[0].ToLower());
                    if (param.Count > 1)
                    {
                        SendMessageHTML(chat.Id, police.PrintArticle(int.Parse(param[1])));
                    }
                    else
                    {
                        SendMessageHTML(chat.Id, police.PrintArticle(0));
                    }

                }
                catch
                {
                    SendMessageHTML(chat.Id, "<code>Presseinformationen konnte nicht geladen werden!</code>");
                }
            }
        }
        private void GetFoodJingJai()
        {
            if (param.Count > 0)
            {
                try
                {
                    string chosenDay = param[0].ToLower();
                    switch (chosenDay.ToLower())
                    {
                        case "gestern":
                            chosenDay = DateTime.Now.AddDays(-2).ToString("dddd").ToLower();
                            break;
                        case "vorgestern":
                            chosenDay = DateTime.Now.AddDays(-1).ToString("dddd").ToLower();
                            break;
                        case "heute":
                            chosenDay = DateTime.Now.ToString("dddd").ToLower();
                            break;

                        case "morgen":

                            chosenDay = DateTime.Now.AddDays(1).ToString("dddd").ToLower();
                            break;

                        case "übermorgen":

                            chosenDay = DateTime.Now.AddDays(2).ToString("dddd").ToLower();
                            break;
                    }
                    Console.WriteLine(chosenDay);
                    if (!Core.isInnerWeek(chosenDay))
                    {
                        return;
                    }
                    string response = HTTPRequester.SimpleRequest("https://www.jing-jai-bremen.de/mittagstisch/");
                    string mainContent = TextHelper.StringBetweenStrings(response, @"<div id=""content_area"">", @"</p> </div>").Replace("  ", "").Replace(" ", "");
                    string[] lines = mainContent.Split(new string[] { "</p>" }, StringSplitOptions.None);

                    bool innerDay = false;
                    JingJai curDay = null;
                    List<JingJai> allDays = new List<JingJai>();
                    foreach (string line in lines)
                    {
                        if (!innerDay)
                        {
                            if (line.TrimStart().StartsWith(@"<p><spanstyle=""background:aqua;"">"))
                            {
                                string day = TextHelper.StringBetweenStrings(line, @"<spanstyle=""font-size:10.0pt;"">", "</span>");

                                switch (day.ToLower())
                                {
                                    case "mo.":
                                        day = "Montag";
                                        break;
                                    case "di.":
                                        day = "Dienstag";
                                        break;
                                    case "mi.":
                                        day = "Mittwoch";
                                        break;
                                    case "do.":
                                        day = "Donnerstag";
                                        break;
                                    case "fr.":
                                        day = "Freitag";
                                        break;
                                }
                                if (day.ToLower() == chosenDay.ToLower())
                                {
                                    innerDay = true;
                                    string title = TextHelper.StringBetweenStrings(line, @"<spanstyle=""background:yellow;"">", "</span>").Replace("`", "").Replace("´", "");
                                    curDay = new JingJai(day, title);
                                }

                            }
                        }
                        else if (line.TrimStart().StartsWith(@"<p><spanstyle=""font-family:erasbolditc,sans-serif;""><spanstyle=""font-size:10.0pt;"">"))
                        {

                            string desc = TextHelper.StringBetweenStrings(line, @"<p><spanstyle=""font-family:erasbolditc,sans-serif;""><spanstyle=""font-size:10.0pt;"">", "").Replace("  ", "");
                            desc = desc.Replace(Environment.NewLine, String.Empty);
                            desc = Regex.Replace(desc, @"<spanstyle=""color:red;""><sup>[a-z]</sup>", String.Empty);
                            desc = Regex.Replace(desc, @"<sup>[a-z]", String.Empty);


                            desc = Regex.Replace(desc, @"<sup><span style=""color:red;"">[a-z]</span></sup>", String.Empty);
                            desc = Regex.Replace(desc, @"<spanstyle=""color:red;"">[a-z]</span></sup>", String.Empty);
                            desc = Regex.Replace(desc, @"<spanstyle=""color:red;"">[a-z]</span>", String.Empty);
                            desc = Regex.Replace(desc, @"<spanstyle=""color:red;"">[a-z]", String.Empty);
                            desc = Regex.Replace(desc, @"<[a-z]*>(.*?)<\/[a-z]*>", String.Empty);
                            desc = Regex.Replace(desc, @"<[a-z]*>", String.Empty);
                            desc = Regex.Replace(desc, @"<\/[a-z]*>", String.Empty);
                            desc = desc.Replace("€", String.Empty);

                            if (desc.Contains("color:red;"))
                            {
                                desc = Regex.Replace(desc, @"<spanstyle=""color:red;"">.*", String.Empty);
                                curDay.AddDesc(desc);
                                innerDay = false;
                                allDays.Add(curDay);
                            }
                            else
                            {
                                curDay.AddDesc(desc);
                            }

                        }
                    }

                    foreach (JingJai day in allDays)
                    {
                        if (day.day.ToLower() == chosenDay.ToLower())
                        {
                            SendMessageHTML(chat.Id, "<code>Am " + day.day + " gibt es bei Jing-Jai</code>" + Environment.NewLine + "<b>" + day.title.TrimStart() + "</b>" + Environment.NewLine + Environment.NewLine + "Zutaten: " + Environment.NewLine + day.desc.Replace(" ", "").TrimStart());
                            return;
                        }
                    }
                    SendMessageHTML(chat.Id, "<code>Jing-Jai bietet diesen " + chosenDay + " kein Essen an!</code>");
                }
                catch { }
            }
        }

        private void Random()
        {
            if (param.Count > 1)
            {
                try
                {
                    long min = long.Parse(param[0]);
                    long max = long.Parse(param[1]);

                    if (max > min)
                    {
                        long result = Core.LongRandom(min, max + 1);
                        SendMessageHTML(chat.Id, "<i>" + user.Username() + " hat eine " + result + " gewürfelt.</i>");
                    }
                }
                catch { }
            }
        }

        private void DHLTrack()
        {
            if (param.Count > 0)
            {
                string trackingID = param[0];
                string response = HTTPRequester.SimpleRequest("http://nolp.dhl.de/nextt-online-public/set_identcodes.do?lang=de&idc=" + trackingID);
                string status = TextHelper.StringBetweenStrings(response, @"<div>Status: ", "</div>");

                if (status == "")
                {
                    SendMessage(chat.Id, "Dein Paket kann zurzeit nicht gefunden werden.");
                }
                else
                {
                    SendMessage(chat.Id, status);
                }

            }
        }
        private void HermesTrack()
        {
            if (param.Count > 0)
            {
                string trackingID = param[0];
                CookieContainer cookieCon = new CookieContainer();
                string responseFirst = HTTPRequester.SimpleRequest("https://www.myhermes.de/wps/portal/paket/Home/privatkunden/privatkunden", cookieCon);

                string actionURL = TextHelper.StringBetweenStrings(responseFirst, @"<form name=""mhStatusForm"" id=""mhStatusForm"" action=""", @""" onsubmit=");
                string responseFinal = HTTPRequester.SimpleRequest("https://www.myhermes.de" + actionURL + "?action=trace&shipmentID=" + trackingID + "&receiptID=", cookieCon);
                if (responseFinal.Contains("content_table table_shipmentDetails"))
                {
                    string cutFirst = TextHelper.StringBetweenStrings(responseFinal, @"<th class=""stateCol""><span>Status</span></th>", "</tbody>");
                    string cutSecond = TextHelper.StringBetweenStrings(cutFirst, @"</tr>", "</tr>");
                    string[] infos = new string[3];
                    int i = 0;
                    foreach (string line in cutSecond.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                    {
                        if (line.Contains("<td>"))
                        {
                            infos[i] = line.Replace("<td>", "").Replace("</td>", "").TrimStart();
                            i++;
                        }
                    }

                    SendMessage(chat.Id, String.Join(Environment.NewLine, infos));
                    return;
                }
                SendMessage(chat.Id, "Dein Paket kann zurzeit nicht gefunden werden.");
                Console.WriteLine();
                return;
            }
        }
        private void RegisterChat()
        {
            if (isAdmin(from.Id))
            {
                if (RegisterChat(chat))
                {
                    SendMessage(chat.Id, "Chat with ID " + chat.Id + " successfully registered...");
                }
            }
        }
        private void KawaiiMeter()
        {
            Random rnd = new Random();
            SendMessage(chat.Id, user.Username() + " ist zu " + rnd.Next(0, 101) + "% Kawaii");
        }
        private void RouletteHandler()
        {
            if (Roulette.GetGame(chat.Id) == null)
            {
                if (message.Text.Contains(' '))
                {
                    try
                    {
                        int maxMember = int.Parse(message.Text.Split(' ')[1]);
                        if (maxMember <= 5000)
                        {
                            Roulette.StartGame(chat.Id, user, maxMember);
                        }
                    }
                    catch { }
                }

            }
            else
            {
                Roulette.GetGame(chat.Id).AddMember(user);
            }
        }
        private void ShootHandler()
        {
            if (Roulette.GetGame(chat.Id) != null)
            {
                Roulette gameTable = Roulette.GetGame(chat.Id);
                if (!gameTable.isOpen())
                {
                    gameTable.Shoot(user);
                }
            }
        }
        private void StopRoulette()
        {
            if (Roulette.GetGame(chat.Id) != null)
            {
                Roulette gameTable = Roulette.GetGame(chat.Id);
                gameTable.Abort(user);
            }
        }
        private void OnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {

            try
            {

                if (Settings.ignoreInput)
                {
                    return;
                }
                message = messageEventArgs.Message;


                if (message == null || message.Type != MessageType.Text) return;

                from = message.From;
                chat = message.Chat;


                if (message.Text.StartsWith("/"))
                {
                    user = ChatUser.GetUser(from);
                    if (user.OnMessageReceived(message.Text))
                    {
                        RegisterUser(user);
                        string parseCommand = "";

                        if (message.Text.Contains("@"))
                        {
                            string toUser = message.Text.Split('@')[1].Contains(" ") ? message.Text.Split('@')[1].Split(' ')[0].ToLower() : message.Text.Split('@')[1].ToLower();
                            if (toUser == Data.Username.ToLower())
                            {
                                parseCommand = message.Text.Split('@')[0];
                            }
                            else
                            {
                                parseCommand = message.Text.Contains(' ') ? message.Text.Split(' ')[0] : message.Text;
                            }

                        }
                        else
                        {
                            parseCommand = message.Text.Contains(" ") ? message.Text.Split(' ')[0] : message.Text;
                        }

                        param = message.Text.ToString().Split(' ').ToList();
                        param.RemoveAt(0);
                        cController.HandleCommand(parseCommand.Remove(0, 1));
                    }
                }
            }
            catch
            {
                Console.WriteLine("Error OnMessageReceived");
            }
        }


        internal void RefreshChats()
        {
            if (chats != null)
            {
                chats.Clear();
            }
            SQLiteDataReader chatReader = DBController.ReturnQuery("SELECT * FROM chat");
            foreach (DbDataRecord chatRow in chatReader)
            {
                Chat curChat = JsonConvert.DeserializeObject<Chat>(chatRow["chatDATA"].ToString());
                RegisterChat(curChat, true);
            }
        }
        internal void RefreshUser()
        {
            if (users != null)
            {
                users.Clear();
            }
            SQLiteDataReader userReader = DBController.ReturnQuery("SELECT * FROM user");
            foreach (DbDataRecord userRow in userReader)
            {
                ChatUser curUser = JsonConvert.DeserializeObject<ChatUser>(userRow["userDATA"].ToString());
                RegisterUser(curUser, true);
            }
        }
        public Chat GetChatByID(long id)
        {
            foreach (long chatID in chats.Keys)
            {
                if (chatID == id)
                {
                    return chats[chatID];
                }
            }
            return null;
        }
        public Chat GetChatByName(string s)
        {
            foreach (long chatID in chats.Keys)
            {
                Chat tempChat = chats[chatID];

                string chatName = tempChat.Type == ChatType.Private ? tempChat.Username : tempChat.Title;

                if (chatName.ToLower() == s.ToLower())
                {
                    return tempChat;
                }
            }
            return null;
        }
        public bool RegisterChat(Chat chat, bool dbLoad = false)
        {
            if (!chats.ContainsKey(chat.Id))
            {
                chats.Add(chat.Id, chat);
                if (!dbLoad)
                {
                    DBController.AddChat(chat);
                }
                return true;
            }
            return false;
        }
        public bool RegisterUser(ChatUser user, bool dbLoad = false)
        {
            if (!users.ContainsKey(user._user.Id))
            {
                users.Add(user._user.Id, user);
                if (!dbLoad)
                {
                    DBController.AddUser(user);
                }
                return true;
            }
            if (!dbLoad)
            {
                DBController.AddUser(user);
            }
            return false;
        }

        private async void Start(string key)
        {
            try
            {
                _key = key;
                var bot = new TelegramBotClient(Settings.API_KEY);
                _bot = bot;
                Data = await bot.GetMeAsync();
                Console.WriteLine("Initialise successfully....");
                chats = new Dictionary<long, Chat>();
                users = new Dictionary<long, ChatUser>();
                Console.WriteLine("Setting up events...");
                _bot.OnCallbackQuery += OnCallbackQueryReceived;
                _bot.OnMessage += OnMessageReceived;
                _bot.OnMessageEdited += OnMessageReceived;
                _bot.OnInlineQuery += OnInlineQueryReceived;
                _bot.OnInlineResultChosen += OnChosenInlineResultReceived;
                _bot.OnReceiveError += OnReceiveError;
                _bot.StartReceiving();
                Console.WriteLine("Events up...");
            }
            catch
            {
                Console.WriteLine("Error while initialising...");
            }
        }
        private static bool isAdmin(int id)
        {
            foreach (int admin in Settings.ADMINS)
            {
                if (admin == id)
                {
                    return true;
                }
            }
            return false;
        }
        private void OnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Error");
        }

        private void OnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResultEventArgs.ChosenInlineResult.ResultId}");
        }

        private async void OnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            try
            {
                string url = "http://pr0gramm.com/api/items/get?flags=11&tags=" + inlineQueryEventArgs.InlineQuery.Query;
                string pr0List = HTTPRequester.SimpleRequest(url);

                Pr0List list = JsonConvert.DeserializeObject<Pr0List>(pr0List);
                List<InlineQueryResultMpeg4Gif> results = new List<InlineQueryResultMpeg4Gif>();
                int i = 1;
                foreach (Pr0Element itm in list.items)
                {
                    if (i < 20)
                    {
                        //Console.WriteLine(itm.GetUrl());
                        InlineQueryResultMpeg4Gif res = new InlineQueryResultMpeg4Gif
                        (
                            itm.id.ToString(),
                            itm.GetUrl(),
                           itm.GetUrl()
                        );
                        results.Add(res);
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }
                await _bot.AnswerInlineQueryAsync(inlineQueryEventArgs.InlineQuery.Id, results.ToArray(), isPersonal: true, cacheTime: 0);
            }
            catch
            {
                //Console.WriteLine(ex.Message);
                //await _bot.AnswerInlineQueryAsync(inlineQueryEventArgs.InlineQuery.Id, null, isPersonal: true, cacheTime: 0);
            }

        }

        internal async void SendMessageHTML(long chatID, string msg, bool disableNotification = true)
        {
            await _bot.SendTextMessageAsync(chatID, msg, ParseMode.Html, disableNotification: disableNotification);
        }

        internal async void SendMessage(long chatID, string msg, bool disableNotification = true)
        {
            try
            {
                await _bot.SendTextMessageAsync(chatID, msg, disableNotification: disableNotification);
            }
            catch { Console.WriteLine("Error sending message to: " + msg); }
        }

        private async void OnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            await _bot.AnswerCallbackQueryAsync(callbackQueryEventArgs.CallbackQuery.Id, $"Received {callbackQueryEventArgs.CallbackQuery.Data}");
        }
    }
}
