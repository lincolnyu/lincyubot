using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using bot2.Logging;
using bot2.Messaging;
using bot2.Tweeting;
using bot2.Yaml;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace bot2
{
    class MyBot : IDisposable
    {
        const int DefaultIntervalSecs = 15;
        private TweetLoader _tweetLoader;

        public TelegramBotClient BotClient { get; private set; }
        public User Me { get; private set; }
        public ILogger Logger { get; private set; }
        public List<string> RecommendedTweeters { get; }

        private SubscriptionManager _subscriptionManager;

        public struct TwitterParams
        {
            public string ConsumerKey; 
            public string ConsumerKeySecret;
            public string AccessToken;
            public string AccessTokenSecret;
        }

        public static MyBot CreateFromConfig(ILogger logger=null)
        {
            var cfg = Config.Singleton;
            var botToken = cfg["telegram"]["token"].ToString();
            var recommendedTweeters = new List<string>();
            var rtNode = cfg["recommended_tweeters"] as ListNode;
            var dataFolder = ((ValueNode)cfg["data_folder"]).ToString();
            if (rtNode != null)
            {
                foreach (var n in rtNode)
                {
                    var v = n as ValueNode;
                    recommendedTweeters.Add(v.ToString());
                }
            }
            var intv = cfg["update_interval_secs"] as ValueNode;
            int updateIntervalSecs = intv?? DefaultIntervalSecs;
            var twtCfg = cfg["twitter"];
            var twitterParams = new TwitterParams {
                ConsumerKey = twtCfg["consumer_key"],
                ConsumerKeySecret = twtCfg["consumer_secret"],
                AccessToken = twtCfg["access_token"],
                AccessTokenSecret = twtCfg["access_token_secret"]
            };
            return new MyBot(botToken, twitterParams, recommendedTweeters, dataFolder, updateIntervalSecs, logger);
        }

        public MyBot(string token, TwitterParams twitterParams, List<string> recommendedTweeters, 
            string dataFolder, int updateIntervalSecs, ILogger logger = null)
        {
            RecommendedTweeters = recommendedTweeters;
            Logger = logger;
            BotClient = new TelegramBotClient(token);
            Me = BotClient.GetMeAsync().Result;
            Logger?.Log($"The bot (id: {Me.Id}, name: {Me.FirstName}) has started.");
            BotClient.OnMessage += BotOnMessage;
            BotClient.StartReceiving();

            var subscriptionDataFile = Path.Combine(dataFolder, "subscriptions.yaml");
            _subscriptionManager = new SubscriptionManager(subscriptionDataFile, PostMessage);

            var tweeterFile = Path.Combine(dataFolder, "tweeters.yaml");
            var twitter = new Twitter(twitterParams.ConsumerKey,
                twitterParams.ConsumerKeySecret,
                twitterParams.AccessToken,
                twitterParams.AccessTokenSecret);
            _tweetLoader = new TweetLoader(twitter, tweeterFile, _subscriptionManager, updateIntervalSecs);
            foreach (var tweeter in recommendedTweeters)
            {
                _tweetLoader.AddSubscription(new Subscription(name:tweeter));
            }
            _tweetLoader.Start();
        }

        ~MyBot()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (BotClient != null)
            {
                BotClient.StopReceiving();
                BotClient = null;
            }
            if (_tweetLoader != null)
            {
                _tweetLoader.Dispose();
                _tweetLoader = null;
            }
            if (_subscriptionManager != null)
            {
                _subscriptionManager.Dispose();
                _subscriptionManager = null;
            }
        }

        private async void BotOnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text != null)
            {
                var chatId = e.Message.Chat.Id;
                Logger?.Log($"Received a text message in chat {chatId}");
                // await BotClient.SendTextMessageAsync(
                //     chatId: e.Message.Chat,
                //     text:   "You said:\n" + e.Message.Text
                // );
                var msg = e.Message.Text; 
                switch (msg)
                {
                case "/start":
                case "/help":
                    await ShowHelp(chatId);
                    break;
                case "/about":
                case "/inxo":
                    await ShowAbout(chatId);
                    break;
                case "/sub":
                    Subscribe(chatId);
                    break;
                case "/unsub":
                    Unsubscribe(chatId);
                    break;
                case "/status":
                    await ShowSubscriptionStatus(chatId);
                    break;
                case "/refresh":
                    await RefreshTweets(chatId);
                    break;
                default:
                    if (msg.Contains("fetch"))
                    {
                        var rexTweet = new Regex(@"fetch[ ]+(?<name>[^ ]+)([ ]+(?<count>\d+)|.*)");
                        var m = rexTweet.Match(msg);
                        var count = 1;
                        List<string> tweeters;
                        if (m.Success)
                        {
                            var name = m.Groups["name"].Value;
                            count = int.Parse(m.Groups["count"].Value);
                            tweeters = new List<string>{name};
                        }
                        else
                        {
                            tweeters = RecommendedTweeters;
                        }
                        await ShowRecentTweetsOfUsers(chatId, tweeters, count);
                    }
                    break;
                }
            }
        }

        private async Task ShowHelp(long chatId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hello. I'm a bot by @lincyu, currently writh limited functionality:");
            sb.AppendLine("/about: Intro and latest news.");
            sb.AppendLine("/help: Show this help.");
            sb.AppendLine("/fetch [tweeter_id [tweet_count]]: Give me the latest tweet(s).");
            sb.AppendLine("/refresh: Get the latest of the current subscriptions.");
            sb.AppendLine("/sub, /unsub: Subscribe or unsubscribe for updates.");
            sb.AppendLine("/status: Subscription status.");
            await BotClient.SendTextMessageAsync(
                chatId: chatId,
                text:   sb.ToString()
            );
        }
        private async Task ShowAbout(long chatId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Feel free to check out:");
            sb.AppendLine("minds.com/lincyu");
            sb.AppendLine("t.me/philosophy_individualism");
            sb.AppendLine("t.me/finestclassic");
            await BotClient.SendTextMessageAsync(
                chatId: chatId,
                text:   sb.ToString()
            );
        }

        private void Subscribe(long chatId)
        {
            foreach (var r in RecommendedTweeters)
            {
                _subscriptionManager.AddSubscription(chatId, r);
            }
        }
        private void Unsubscribe(long chatId)
        {
            _subscriptionManager.RemoveSubscriber(chatId);
        }

        private async Task ShowSubscriptionStatus(long chatId)
        {
            var subscrstr = _subscriptionManager.AllSubscribers.ContainsKey(chatId)?
                "subscribed" : "not subscribed";
            await BotClient.SendTextMessageAsync(
                chatId: chatId,
                text:   $"You are {subscrstr}."
            );
        }

        private async Task RefreshTweets(long chatId)
        {
            var currTweets = _tweetLoader.YieldCurrentTweets();
            foreach (var tweet in currTweets)
            {
                await BotClient.SendTextMessageAsync(
                    chatId: chatId,
                    text:   tweet.TweetToBotMessage()
                );
            }
        }

        private async Task ShowRecentTweetsOfUsers(long chatId, List<string> tweeters, int count)
        {
            foreach (var tweeter in tweeters)
            {
                var tweets = _tweetLoader.GetTweets(tweeter, count);
                foreach (var tweet in tweets)
                {
                    await BotClient.SendTextMessageAsync(
                        chatId: chatId,
                        text:   tweet.TweetToBotMessage()
                    );
                }
            }
        }


        private async Task PostMessage(long chatId, Tweet tweet)
        {
            var message = tweet.TweetToBotMessage();
            await BotClient.SendTextMessageAsync(
                chatId: chatId,
                text:   message
            );
        }
    }
}
