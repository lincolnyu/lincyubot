using System.Collections.Generic;

namespace bot2.Messaging
{
    public class Subscriber
    {
        public long ChatId {get;}
        public readonly HashSet<Subscription> Subscriptions = new HashSet<Subscription>();
        public Subscriber(long chatId)
        {
            ChatId = chatId;
        }
    }
}
