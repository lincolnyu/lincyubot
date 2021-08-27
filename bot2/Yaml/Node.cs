using System.Collections.Generic;

namespace bot2.Yaml
{
    public abstract class Node
    {
        public abstract Node this[string key] { get; set; }
        public abstract IEnumerable<string> Keys { get; }
        public abstract object Serialize();

        public static implicit operator string(Node n)
        {
            return n.ToString();
        }
    }
}
