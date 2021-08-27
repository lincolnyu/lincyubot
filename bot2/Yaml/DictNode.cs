
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace bot2.Yaml
{
    public class DictNode : Node, IEnumerable<KeyValuePair<string, Node>>
    {
        Dictionary<string, object> _subnodes = new Dictionary<string, object>();
        public DictNode(Dictionary<object, object> dict)
        {
            foreach (var (k,v) in dict)
            {
                if (k is string s)
                {
                    _subnodes[s] = v;
                }
            }
        }
        
        public DictNode() : this(new Dictionary<object, object>{})
        {
        }

        public static implicit operator DictNode(Dictionary<object, object> dict)
        {
            return new DictNode(dict);
        }

        public override IEnumerable<string> Keys => _subnodes.Keys;

        public override Node this[string key] 
        {
            get
            {
                if (!_subnodes.TryGetValue(key, out var o))
                {
                    // null always indicates not available
                    return null;
                }
                var n = o as Node;
                if (n == null)
                {
                    n = NodeFactory.ObjectToNode(o);
                    _subnodes[key] = n; // box it and keep it for fast and consistent access later
                }
                return n;
            }
            set
            {
                _subnodes[key] = value;
            }
        }

        public bool Remove(string key) => _subnodes.Remove(key);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            foreach (var key in Keys)
            {
                sb.Append(key);
                sb.Append(":");
                sb.Append(this[key]);
            }
            sb.Append("}");
            return sb.ToString();
        }

        public override object Serialize()
        {
            var dict = new Dictionary<object, object>();
            foreach (var (k,v) in _subnodes)
            {
                if (v is Node n)
                {
                    dict.Add(k, n.Serialize());
                }
                else
                {
                    dict.Add(k, v);
                }
            }
            return dict;
        }

        public IEnumerator<KeyValuePair<string, Node>> GetEnumerator()
        {
            foreach (var (k,v) in _subnodes)
            {
                yield return new KeyValuePair<string, Node>(k, NodeFactory.ObjectToNode(v));
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}