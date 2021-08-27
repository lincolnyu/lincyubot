using System.Collections;
using System.Collections.Generic;

namespace bot2.Yaml
{
    public class ListNode : Node, IEnumerable<Node>
    {
        private List<object> _subnodes = new List<object>();
        public ListNode(List<object> l)
        {
            _subnodes = l;
        }

        public static implicit operator ListNode(List<object> l)
        {
            return new ListNode(l);
        }

        public override Node this[string key]
        { 
            get => throw new System.NotImplementedException(); 
            set => throw new System.NotImplementedException(); 
        }

        public override IEnumerable<string> Keys => throw new System.NotSupportedException();

        public Node this[int index]
        {
            get
            {
                var o = _subnodes[index];
                Node n = o as Node;
                if (n == null)
                {
                    n = NodeFactory.ObjectToNode(o);
                    _subnodes[index] = n; // box it and keep it for fast and consistent access later
                }
                return n;
            }
            set
            {
                _subnodes[index] = value;
            }
        }
        public int Count => _subnodes.Count;


        public void Add(object o)
        {
            _subnodes.Add(NodeFactory.ObjectToNode(o));
        }

        public void Insert(int index, object o)
        {
            _subnodes.Insert(index, NodeFactory.ObjectToNode(o));
        }

        public IEnumerator<Node> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override object Serialize()
        {
            var l = new List<object>();
            foreach (var j in _subnodes)
            {
                if (j is Node n)
                {
                    l.Add(n.Serialize());
                }
                else
                {
                    l.Add(j);
                }
            }
            return l;
        }
    }
}