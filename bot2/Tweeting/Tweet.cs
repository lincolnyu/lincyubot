namespace bot2.Tweeting
{
    public class TweetInfo
    {
        public string Tweeter;
        public string Id;
        public string CreatedAt;
        public string Content = null;
    }
    public class Tweet
    {
        public TweetInfo TweetInfo; // Content==null if retweeting without comment
        public TweetInfo RetweetInfo; // Original Tweet

        // The primary content
        public string Content => TweetInfo.Content?? RetweetInfo?.Content;  
    }
}