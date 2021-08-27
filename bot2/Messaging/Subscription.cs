using System.Collections.Generic;

namespace bot2.Messaging
{
    public class Subscription
    {
        public string Name { get; }
        public readonly HashSet<Subscriber> Subscribers = new HashSet<Subscriber>();
        public Subscription(string name)
        {
            Name = name;
        }
    }
}
