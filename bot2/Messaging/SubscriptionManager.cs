using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bot2.Logging;
using bot2.Tweeting;
using bot2.Yaml;
using YamlDotNet.Serialization;

namespace bot2.Messaging
{
    public class SubscriptionManager : IDisposable
    {
        public delegate void SubscriptionAddedHandler(Subscription subscription);
        public delegate void SubscriptionRemovedHandler(Subscription subscription);

        public event SubscriptionAddedHandler SubscriptionAdded;
        public event SubscriptionRemovedHandler SubscriptionRemoved;

        public readonly Dictionary<string, Subscription> AllSubscriptions = new Dictionary<string, Subscription>();
        public readonly Dictionary<long, Subscriber> AllSubscribers = new Dictionary<long, Subscriber>();

        public int AttemptTimes { get; set; } = 5;

        public ILogger Logger { get; private set; }

        public delegate Task PostCallback(long chatId, Tweet tweet);
        PostCallback _postCb;
        private string _dataFile;

        private bool _dirty = false;

        public SubscriptionManager(string dataFile, PostCallback postCb, ILogger logger)
        {
            _postCb = postCb;
            _dataFile = dataFile;
            Logger = logger;
            if (File.Exists(dataFile))
            {
                using var srData = new StreamReader(dataFile);
                var deserializer = new DeserializerBuilder().Build();
                var o = deserializer.Deserialize(srData);
                if (o != null)
                {
                    var data = (DictNode)NodeFactory.ObjectToNode(o);
                    foreach (var (subscriberIdStr, n) in data)
                    {
                        var subscriberId = long.Parse(subscriberIdStr);
                        var l = n as ListNode;
                        foreach (var subscriptionNode in l)
                        {
                            AddSubscription(subscriberId, subscriptionNode.ToString());
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_dataFile != null)
            {
                Save();
                _dataFile = null;
            }
        }

        ~SubscriptionManager()
        {
            Dispose();
        }

        public void Save()
        {
            if (!_dirty)
            {
                return;
            }
            var data = new Dictionary<long, List<string>>();
            foreach (var subscriber in AllSubscribers.Values)
            {
                var l = new List<string>();
                foreach (var subscription in subscriber.Subscriptions)
                {
                    l.Add(subscription.Name);
                }
                data[subscriber.ChatId] = l;
            }
            using var swData = new StreamWriter(_dataFile);
            var serializer = new SerializerBuilder().Build();
            var s = serializer.Serialize(data);
            swData.Write(s);
            _dirty = false;
        }

        private void ChangeCall(bool changed)
        {
            _dirty = _dirty || changed;
        }
        public void AddSubscription(long chatId, string subscriptionName)
        {
            if (!AllSubscriptions.TryGetValue(subscriptionName, out var subscription))
            {
                subscription = new Subscription(subscriptionName);
                AllSubscriptions[subscriptionName] = subscription;
                SubscriptionAdded?.Invoke(subscription);
                _dirty = true;
            }
            if (!AllSubscribers.TryGetValue(chatId, out var subscriber))
            {
                subscriber = new Subscriber(chatId);
                AllSubscribers[chatId] = subscriber;
                _dirty = true;
            }
            ChangeCall(subscriber.Subscriptions.Add(subscription));
            ChangeCall(subscription.Subscribers.Add(subscriber));
        }

        public void RemoveSubscription(long chatId, string subscriptionName)
        {
            var subscription = AllSubscriptions[subscriptionName];
            var subscriber = AllSubscribers[chatId];
            ChangeCall(subscriber.Subscriptions.Remove(subscription));
            ChangeCall(subscription.Subscribers.Remove(subscriber));
            if (subscription.Subscribers.Count == 0)
            {
                AllSubscriptions.Remove(subscriptionName);
                SubscriptionRemoved?.Invoke(subscription);
                _dirty = true;
            }
            if (subscriber.Subscriptions.Count == 0)
            {
                AllSubscribers.Remove(chatId);
                _dirty = true;
            }
        }

        public bool Subscribed(long chatId, string subscriptionName)
        {
            if (!AllSubscriptions.TryGetValue(subscriptionName, out var subscription))
            {
                return false;
            }
            if (!AllSubscribers.TryGetValue(chatId, out var subscriber))
            {
                return false;
            }
            if (!subscriber.Subscriptions.Contains(subscription))
            {
                return false;
            }
            return true;
        }

        public void RemoveSubscriber(long chatId)
        {
            if (AllSubscribers.ContainsKey(chatId))
            {
                var subscriber = AllSubscribers[chatId];
                foreach(var subscr in subscriber.Subscriptions)
                {
                    RemoveSubscription(chatId, subscr.Name);
                }
                AllSubscribers.Remove(chatId);
                _dirty = true;
            }
        }

        public HashSet<Subscription> GetSubscriptions(long chatId)
        {
            var subscribed = AllSubscribers.TryGetValue(chatId, out var subscriber);
            if (subscribed)
            {
                return subscriber.Subscriptions;
            }
            return null;
        }

        public async Task Post(Tweet tweet)
        {
            if (_postCb == null)
            {
                return;
            }
            var tweeterName = tweet.TweetInfo.Tweeter;
            if (!AllSubscriptions.TryGetValue(tweeterName, out var subscription))
            {
                return;
            }
            foreach (var subscriber in subscription.Subscribers)
            {
                var done = false;
                int att = 0;
                for (att=0; !done && att < AttemptTimes; ++att)
                {
                    try
                    {
                        await _postCb.Invoke(subscriber.ChatId, tweet);
                        done = true;
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException)
                    {
                    }
                }
                if (!done)
                {
                    Logger?.Log($"[Error]: sending to {subscriber.ChatId} after {att} attempts.");
                }
            }
        }
    }
}
