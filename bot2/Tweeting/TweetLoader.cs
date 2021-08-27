using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using bot2.Messaging;
using bot2.Yaml;
using YamlDotNet.Serialization;

namespace bot2.Tweeting
{
    // https://www.youtube.com/watch?v=ifZXSzwff4E
    class TweetLoader : IDisposable
    {
        public delegate Task DisplayTweet(Tweet tweet);
        private Node _data;
        private SubscriptionManager _subscriptionManager;

        private Twitter _twitter;
        private string _dataFile;
        private Thread _workingThread;
        private int _intervalSecs;
        private bool _running = false;
        private ManualResetEvent _quitEvent = new ManualResetEvent(false);

        public TweetLoader(Twitter twitter, string dataFile, SubscriptionManager subscriptionManager, int intervalSecs = 15, bool start=false)
        {
            _twitter = twitter;
            _dataFile = dataFile;
            _intervalSecs = intervalSecs;
            
            _subscriptionManager = subscriptionManager;

            if (File.Exists(dataFile))
            {
                using var srData = new StreamReader(dataFile);
                var deserializer = new DeserializerBuilder().Build();
                var o = deserializer.Deserialize(srData);
                if (o != null)
                {
                    _data = NodeFactory.ObjectToNode(o);
                }
            }
            
            if (_data == null)
            {
                _data = new DictNode();
            }

            if (_data["tweeters"] == null)
            {
                _data["tweeters"] = new DictNode();
            }

            var tweetersData = (DictNode)_data["tweeters"];
            foreach (var (key, subscription) in subscriptionManager.AllSubscriptions)
            {
                if (tweetersData[key] == null)
                {
                    tweetersData[key] = new DictNode();
                }
            }
            subscriptionManager.SubscriptionAdded += subscription =>
            {
                lock(_data)
                {
                    if (tweetersData[subscription.Name] == null)
                    {
                        tweetersData[subscription.Name] = new DictNode();
                    }
                }
            };
            subscriptionManager.SubscriptionRemoved += subscription =>
            {
                lock(_data)
                {
                    ((DictNode)tweetersData).Remove(subscription.Name);
                }
            };

            _workingThread = new Thread(WorkingThreadProc);
            if (start)
            {
                Start();
            }
        }

        ~TweetLoader()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_workingThread != null)
            {
                _running = false;
                _quitEvent.Set();
                if (_workingThread.IsAlive)
                {
                    _workingThread.Join();
                }
                _workingThread = null;
            }
            if (_dataFile != null)
            {
                Save();
                _dataFile = null;
            }
        }

        public void Start()
        {
            _workingThread.Start();
        }

        public void AddSubscription(Subscription subscription)
        {
            lock(_data)
            {
                var tweetersData = _data["tweeters"];
                if (tweetersData == null)
                {
                    tweetersData = new DictNode();
                    _data["tweeters"] = tweetersData;
                }
                var tweeter = subscription.Name;
                if (tweetersData[tweeter] == null)
                {
                    tweetersData[tweeter] = new DictNode();
                }
            }
        }

        public void Save()
        {
            using var swData = new StreamWriter(_dataFile);
            var serializer = new SerializerBuilder().Build();
            var s = serializer.Serialize(_data.Serialize());
            swData.Write(s);
        }
        private void WorkingThreadProc(object obj)
        {
            var intervalMs = _intervalSecs*1000;
            _running = true;
            while (_running)
            {
                var task = ProcessTweets();
                task.Wait();
                _quitEvent.WaitOne(intervalMs);
            }
        }

        private async Task ProcessTweets()
        {
            var tweets = YieldTweets().ToArray();
            foreach (var tweet in tweets)
            {
                await _subscriptionManager.Post(tweet);
            }
        }

        private IEnumerable<Tweet> YieldTweets()
        {
            // TODO: Proper implementation message queue ...
            var tweeters = _data["tweeters"] as DictNode;
            if (tweeters != null)
            { 
                // sorted first by tweeters and then chronolgical order (since last displayed tweet)               
                foreach (var (tweeter, tdata) in tweeters)
                {
                    string new_last_tweet_id = null;
                    var lti = tdata["last_tweet_id"];
                    if (lti != null)
                    {
                        IEnumerable<Tweet> tweets;
                        try
                        {
                            tweets = GetTweets(tweeter, 20, DefaultGetTweetsResultHandler);
                        }
                        catch (WebException)
                        {
                            //TODO automatically remove?
                            Console.WriteLine($"Error getting tweets from {tweeter}");
                            continue;
                        }

                        var yieldCount = 0;
                        var last_tweet_id = lti.ToString();
                        // Assuming tweets are in chronological order
                        foreach (var tweet in tweets)
                        {
                            if (tweet.TweetInfo.Id == last_tweet_id)
                            {
                                break;
                            }
                            if (new_last_tweet_id == null)
                            {
                                new_last_tweet_id = tweet.TweetInfo.Id;
                            }              
                            yield return tweet;
                            yieldCount++;
                        }
                        Console.WriteLine($"Obtained {yieldCount} new tweets from '{tweeter}'");
                    }
                    else
                    {
                        var tweets = GetTweets(tweeter, 1, DefaultGetTweetsResultHandler).ToArray();
                        if (tweets.Length > 0)
                        {
                            var tweet = tweets[0];
                            new_last_tweet_id = tweet.TweetInfo.Id;
                            Console.WriteLine($"Obtained 1 new tweets from '{tweeter}'");
                            yield return tweet;
                        }
                    }

                    if (new_last_tweet_id != null)
                    {
                        tdata["last_tweet_id"] = new ValueNode(new_last_tweet_id);
                    }
                }
            }
        }

        public IEnumerable<Tweet> YieldCurrentTweets()
        {
            var tweeters = _data["tweeters"] as DictNode;
            if (tweeters != null)
            { 
                // sorted first by tweeters and then chronolgical order (since last displayed tweet)               
                foreach (var (tweeter, tdata) in tweeters)
                {
                    Tweet[] tweets;
                    try
                    {
                        tweets = GetTweets(tweeter, 1).ToArray();
                    }
                    catch (WebException)
                    {
                        //TODO automatically remove?
                        Console.WriteLine($"Error getting tweets from {tweeter}");
                        continue;
                    }
                    if (tweets.Length == 1) 
                    {
                        yield return tweets[0];
                    }
                }
            }
        }

        public delegate void GetTweetsResultCallback(string tweeter, bool succeeded);
        public void DefaultGetTweetsResultHandler(string tweeter, bool succeeded)
        {
            if (!succeeded)
            {
                Console.WriteLine($"Error: Failed to get tweets from {tweeter}");
            }
        }
        public IEnumerable<Tweet> GetTweets(string tweeter, int count, GetTweetsResultCallback resultCb=null)
        {
            try
            {
                resultCb?.Invoke(tweeter, true);
                return _twitter.EnumerateTweets(tweeter, count);
            }
            catch (WebException)
            {
                resultCb?.Invoke(tweeter, false);
                return Enumerable.Empty<Tweet>();
            }
        }
    }
}