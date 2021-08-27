using System.IO;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using bot2.Yaml;

namespace bot2
{
    class Config
    {
        private Node _cfg;
        public readonly static Config Singleton = new Config(); 
        private Config()
        {
            var deserializer = new DeserializerBuilder().Build();
            var srCfg = new StreamReader("./data/config.yaml");
            var o = deserializer.Deserialize(srCfg);
            _cfg = NodeFactory.ObjectToNode(o);
        }

        public Node this[string key]
        {
            get
            {
                return _cfg[key];
            }
        }

        public IEnumerable<string> Keys => (_cfg is DictNode intnode)? intnode.Keys : new string[]{};

        public override string ToString()
        {
            return _cfg.ToString();
        }
    }
}
