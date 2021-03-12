using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Args;
using YamlDotNet.Serialization;


namespace bot2
{
    class Program
    {
        class Node
        {
            object _o;
            Dictionary<string, Node> _cache;
            public Node(object o)
            {
                _o = o;
            }
            public bool IsDict => _o is Dictionary<object,object>;
            public IEnumerable<string> Keys => ((Dictionary<object,object>)_o).Keys.Cast<string>();

            public Node this[string key] 
            {
                get
                {
                    if (_cache == null)
                    {
                        _cache = new Dictionary<string, Node>();
                    }                   
                    if (!_cache.TryGetValue(key, out var node))
                    {
                        node = new Node(((Dictionary<object,object>)_o)[key]);
                        _cache[key] = node;
                    }
                    return node;
                }
            } 

            public override string ToString()
            {
                if (!IsDict)
                {
                    return _o.ToString();
                }
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
        }

        class Config
        {
            private Node _cfg;

            public readonly static Config Singleton = new Config(); 
            private Config()
            {
                var deserializer = new DeserializerBuilder().Build();
                var srCfg = new StreamReader("./data/config.yaml");
                _cfg = new Node(deserializer.Deserialize(srCfg));
            }

            public Node this[string key]
            {
                get
                {
                    return _cfg[key];
                }
            }

            public IEnumerable<string> Keys => _cfg.Keys;

            public override string ToString()
            {
                return _cfg.ToString();
            }
        }
        
        static void Main(string[] args)
        {
            var cfg = Config.Singleton;
            
            var botToken = cfg["telegram"]["token"].ToString();
            var bc = new TelegramBotClient(botToken);
            var me = bc.GetMeAsync().Result;
            Console.WriteLine($"The bot (id: {me.Id}, name: {me.FirstName} has started.");
            bc.OnMessage += BotOnMessage;
            bc.StartReceiving();
            Console.WriteLine("Press 'Q' to exit.");
            while (true)
            {
                var k = Console.ReadKey();
                if (k.Key == ConsoleKey.Q)
                {
                    break;
                }
            }
            bc.StopReceiving();
        }
        private static void BotOnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text != null)
            {
                Console.WriteLine($"Received a text message in chat {e.Message.Chat.Id}.");

                // await botClient.SendTextMessageAsync(
                //     chatId: e.Message.Chat,
                //     text:   "You said:\n" + e.Message.Text
                // );
            }
        }
    }
}
