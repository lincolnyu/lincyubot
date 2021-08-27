
using System.Collections.Generic;

namespace bot2.Yaml
{
    public class ValueNode: Node
    {
        object _o;
        public ValueNode(object o)
        {
            _o = o;
        }

        public override Node this[string key]
        {
            get => throw new System.NotSupportedException();
            set => throw new System.NotSupportedException();
        }
        public override IEnumerable<string> Keys => throw new System.NotSupportedException();
        public override object Serialize()
        {
            return _o;
        }

        public override string ToString()
        {
            return _o.ToString();
        }

        public static implicit operator int(ValueNode n)
        {
            return (int)n._o;
        }
    }
}