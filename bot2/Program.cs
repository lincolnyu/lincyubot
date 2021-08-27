using System;
using System.Collections.Generic;
using bot2.Logging;
using bot2.Tweeting;
using bot2.Yaml;

namespace bot2
{
    class Program
    {
        static void TwitterTest()
        {
            var cfg = Config.Singleton;
            var twtCfg = cfg["twitter"];
            var twitter = new Tweeting.Twitter(
                twtCfg["consumer_key"],
                twtCfg["consumer_secret"],
                twtCfg["access_token"],
                twtCfg["access_token_secret"]
            );
            var tweets = twitter.EnumerateTweets("shermanchen002", 2);
            foreach (var t in tweets)
            {
                Console.WriteLine($"{t.Content}");
                Console.WriteLine("------------------------------");
            }
        }
        static void Main(string[] args)
        {
            //TwitterTest();
            using var myBot = MyBot.CreateFromConfig(new ConsoleLogger());
            Console.WriteLine("Press 'Q' to exit.");
            while (true)
            {
                var k = Console.ReadKey();
                if (k.Key == ConsoleKey.Q)
                {
                    break;
                }
            }
        }
    }
}
