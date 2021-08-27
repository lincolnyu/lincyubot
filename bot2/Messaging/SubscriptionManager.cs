using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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


        public delegate Task PostCallback(long chatId, Tweet tweet);
        PostCallback _postCb;
        private string _dataFile;

        public SubscriptionManager(string dataFile, PostCallback postCb)
        {
            _postCb = postCb;
            _dataFile = dataFile;
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

        private void Save()
        {
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
        }

        public void AddSubscription(long chatId, string subscriptionName)
        {
            if (!AllSubscriptions.TryGetValue(subscriptionName, out var subscription))
            {
                subscription = new Subscription(subscriptionName);
                AllSubscriptions[subscriptionName] = subscription;
                SubscriptionAdded?.Invoke(subscription);
            }
            if (!AllSubscribers.TryGetValue(chatId, out var subscriber))
            {
                subscriber = new Subscriber(chatId);
                AllSubscribers[chatId] = subscriber;
            }
            subscriber.Subscriptions.Add(subscription);
            subscription.Subscribers.Add(subscriber);
        }

        public void RemoveSubscription(long chatId, string subscriptionName)
        {
            var subscription = AllSubscriptions[subscriptionName];
            var subscriber = AllSubscribers[chatId];
            subscriber.Subscriptions.Remove(subscription);
            subscription.Subscribers.Remove(subscriber);
            if (subscription.Subscribers.Count == 0)
            {
                AllSubscriptions.Remove(subscriptionName);
                SubscriptionRemoved?.Invoke(subscription);
            }
            if (subscriber.Subscriptions.Count == 0)
            {
                AllSubscribers.Remove(chatId);
            }
        }

        public void RemoveSubscriber(long chatId)
        {
            var subscriber = AllSubscribers[chatId];
            foreach(var subscr in subscriber.Subscriptions)
            {
                RemoveSubscription(chatId, subscr.Name);
            }
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
                await _postCb.Invoke(subscriber.ChatId, tweet);
            }
        }
    }
}
