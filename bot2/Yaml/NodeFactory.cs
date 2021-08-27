using System.Collections.Generic;

namespace bot2.Yaml
{
    public static class NodeFactory
    {
        public static Node ObjectToNode(object o)
        {
            if (o == null)
            {
                return new ValueNode(null);
            }
            else if (o is Node n)
            {
                return n;
            }
            else if (o is Dictionary<object, object> d)
            {
                return new DictNode(d);
            }
            else if (o is List<object> l)
            {
                return new ListNode(l);
            }
            return new ValueNode(o);
        }
    }
}