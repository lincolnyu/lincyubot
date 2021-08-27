using System.Collections.Generic;
using System.Text;

namespace bot2.Tweeting
{
    public static class Helper
    {
        public static IEnumerable<Tweet> EnumerateTweets(this Twitter twitter, string tweeter,
            int count, bool truncated=false)
        {
            var r = twitter.GetTweets(tweeter, count, truncated);
            var jdTimeline = System.Text.Json.JsonDocument.Parse(r);
            var arrayLen = jdTimeline.RootElement.GetArrayLength();
            var txtProperty = truncated? "text" : "full_text";
            for (var i = 0; i < arrayLen; i++)
            {
                var twtElem = jdTimeline.RootElement[i];
                TweetInfo retweetInfo = null;
                // foreach (var e in twtElem.EnumerateObject())
                // {
                //     System.Console.WriteLine($"{e.Name}:{e.Value}");
                // }
                var tweetInfo = new TweetInfo
                {
                    Id = twtElem.GetProperty("id").ToString(),
                    CreatedAt = twtElem.GetProperty("created_at").ToString(),
                    Tweeter = tweeter
                };
                if (truncated)
                {
                    // TODO review this (in relation to retweeting)...
                    tweetInfo.Content = twtElem.GetProperty("text").ToString();
                }
                else
                {
                    bool isQuote = false;
                    if (twtElem.TryGetProperty("is_quote_status", out var isq))
                    {
                        isQuote = isq.ToString() == "true";
                    }
                    var isRt = twtElem.TryGetProperty("retweeted_status", out var rt);
                    if (isRt)
                    {
                        retweetInfo = new TweetInfo
                        {
                            Id = rt.GetProperty("id").ToString(),
                            CreatedAt = rt.GetProperty("created_at").ToString(),
                            Content = rt.GetProperty("full_text").ToString(),
                            Tweeter = rt.GetProperty("user").GetProperty("screen_name").ToString()
                        };
                        if (isQuote)
                        {
                            tweetInfo.Content = twtElem.GetProperty("full_text").ToString();
                        }
                    }
                    else
                    {
                        tweetInfo.Content = twtElem.GetProperty("full_text").ToString();
                    }
                }
                yield return new Tweet
                {
                    TweetInfo = tweetInfo,
                    RetweetInfo = retweetInfo                    
                };
            }
        }

        public static string TweetToBotMessage(this Tweet tweet)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{tweet.TweetInfo.Tweeter}:");
            //sb.AppendLine(tweet.Content);
            sb.Append($"https://twitter.com/minds/status/{tweet.TweetInfo.Id}");
            return sb.ToString();
        }
    }
}