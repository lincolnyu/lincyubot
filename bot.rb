require 'telegram/bot'
require 'twitter'
require 'yaml'

$subscribed = Set[]

$data_folder = './'

$recommended_tweeters = ["thealicesmith", "Anya_jebiga", "EraseState", "jordanbpeterson", "prageru", "roadtoserfdom3"]
$recommended_tweeters_latest_tweets = {}

def load_userdata
    $subscribed = Set[]
    userdata_filepath = File.join($data_folder, 'userdata.yaml')
    if File.exists?(userdata_filepath)
        userdata_file = File.open(userdata_filepath)
        userdata_str = userdata_file.read
        userdata = YAML.load(userdata_str)
        if userdata.key?('subscribed')
            $subscribed = userdata['subscribed']
        end
    end
end

def save_userdata
    userdata = {'subscribed' => $subscribed}
    File.write('userdata.yaml', userdata.to_yaml())
end

def load_commondata
    $recommended_tweeters_latest_tweets = {}
    commondata_filepath = File.join($data_folder, "commondata.yaml")
    if File.exists?(commondata_filepath)
        data_file = File.open(commondata_filepath)
        data_str = data_file.read
        data = YAML.load(data_str)
        if data && data.key?('recommended_tweeters_latest_tweets')
            $recommended_tweeters_latest_tweets = data['recommended_tweeters_latest_tweets']
        end
    end
end

def save_commondata
    commondata_filepath = File.join($data_folder, "commondata.yaml")
    data = {'recommended_tweeters_latest_tweets' => $recommended_tweeters_latest_tweets}
    File.write(commondata_filepath, data.to_yaml())
end

def get_tweet_url(tweeter, tweet_id)
    return "twitter.com/" + tweeter + "/status/" + tweet_id.to_s
end

def launder(s)
    #return s
    return s.gsub('*', '\\*').gsub('_', '\\_')
end

def guard_input_msg(s)
   if s.length() <= 32
	return s
   end
   return s[0, 20] + "...total msglen #{s.length()}"
end

def try_send_message(bot, chat_id, text, parse_mode)
    bot.api.send_message(chat_id: chat_id, text: text, parse_mode: parse_mode)
rescue Exception => err
    print "Some error occurred when sending message on chat #{chat_id}: #{err}"
end

# Load config
begin
    cfg_file = File.open('config.yaml')
    cfg_str  = cfg_file.read
    $cfg_data = YAML.load(cfg_str)
    if $cfg_data.key?('datafolder')
        $data_folder = $cfg_data['data_folder']
    end
end

$help_msg = "Hello. I'm a bot by @lincyu, currently writh limited functionality:\n" \
          + "/about: Intro and latest news.\n" \
          + "/help: Show this help.\n" \
          + "/tweed [tweeter_id [tweet_count]]: Give me the latest tweet(s).\n" \
          + "/sub, /unsub: Subscribe or unsubscribe for updates.\n" \
          + "/status: Subscription status."

$start_msg = $help_msg + "\n" \
           + "Or check out:\n" \
           + "minds.com/lincyu\n" \
           + "t.me/philosophy_individualism\n" \
           + "t.me/finestclassic\n" \

load_commondata
load_userdata

# https://core.telegram.org/bots/api
begin
    # Twitter client
    tw_client = Twitter::REST::Client.new do |config|
        config.consumer_key        = $cfg_data['twitter']['consumer_key']
        config.consumer_secret     = $cfg_data['twitter']['consumer_secret']
        config.access_token        = $cfg_data['twitter']['access_token']
        config.access_token_secret = $cfg_data['twitter']['access_token_secret']
    end

    token = $cfg_data['telegram']['token']
    Telegram::Bot::Client.run(token) do |bot|
        puts "#{Time.now.inspect}: Setting up timer."
        timer = Thread.new do
            while true
                reply_texts = []
                if !$subscribed.empty?()
                    $recommended_tweeters.each do |tweeter|
                        tweets = tw_client.user_timeline(tweeter, exclude_replies: true, count: 1)
                        tweets.each do |tweet|
                            if !$recommended_tweeters_latest_tweets.has_key?(tweeter) || $recommended_tweeters_latest_tweets[tweeter] != tweet.id
                                puts("#{Time.now.inspect}: tweeter @#{tweeter} has new update")
                                $recommended_tweeters_latest_tweets[tweeter] = tweet.id
                                save_commondata
                                #TODO use markdown
                                tw_text = "🔔\n" + launder(tweet.full_text) + "\n -- [" + tweeter + "](" + get_tweet_url(tweeter, tweet.id) + ")"
                                reply_texts.push(tw_text)
                            end
                        end
                    rescue Exception=>err
                        print "Some error occurred when trying to retrieve timeline: #{err}."
                    end
    
                    $subscribed.each do |chat_id|
                        reply_texts.each do |reply_text|
                            try_send_message(bot, chat_id, reply_text, "Markdown")
                        end
                    end
                end
                sleep 60
            end
        end
    
        bot.listen do |message|
            reply_texts = []
            case message.text
            when '/start', '/help'
                reply_texts.push(launder($start_msg))
            when '/about', '/intro'
                reply_texts.push(launder("Feel free to check out:\n" \
                        + "minds.com/lincyu\n" \
                        + "t.me/philosophy_individualism\n" \
                        + "t.me/finestclassic\n" \
                        + "(inactive) lincyu on mastodon/mewe/safechat/gab"))
            when /tweed/i, /twitter/i
                textl = message.text
                textl.downcase!
                m = textl.match /tweed[ ]+(?<name>[^ ]+)([ ]+(?<count>\d+)|.*)/
                twcount = 1
                if m
                    tw = m[:name]
                    tw.strip!
                    tweeters = [tw]
                    if m[:count]
                        twcount = m[:count]
                    end
                else
                    tweeters = $recommended_tweeters
                end
                res = ""
                tweeters.each do |tweeter|
                    tweets = tw_client.user_timeline(tweeter, tweet_mode:"extended", exclude_replies: true, count: twcount)
                    tweets.each do |tweet|
                        t = launder(tweet.full_text)
                        t += "\n -- [" + tweeter + "](" + get_tweet_url(tweeter, tweet.id) + ")"
                        reply_texts.push(t)
                    end
                end
            when '/sub'
                $subscribed.add(message.chat.id)
                #TODO Performance review point 
                save_userdata
                puts "#{Time.now.inspect}: Chat id #{message.chat.id} subscribed"
                reply_texts.push(launder("Subscribed.\n"))
            when '/unsub'
                $subscribed.delete(message.chat.id)
                #TODO Performance review point 
                save_userdata
                puts "#{Time.now.inspect}: Chat id #{message.chat.id} unsubscribed"
                reply_texts.push(launder("Unsubscribed.\n"))
            when '/status'
                if $subscribed.include?(message.chat.id)
                    reply_texts.push(launder("Subscribed.\n"))
                else
                    reply_texts.push(launder("Unsubscribed.\n"))
                end
            else
                if message.from && message.chat.type=="private"
                    reply_texts.push(launder("Sorry, I have no idea what '#{message.text}' means.\n" + $help_msg))
                end
            end
            reply_texts.each do |reply_text|
                try_send_message(bot, message.chat.id, reply_text, "Markdown")
            end
            if message.from
                log_str = "#{Time.now.inspect}: Messages sent to @#{message.from.username} in response to '#{guard_input_msg(message.text)}'."
            else
                log_str = "#{Time.now.inspect}: Messages sent to anonymous in response to '#{guard_input_msg(message.text)}'."
            end
            puts log_str
        end
    end
rescue Telegram::Bot::Exceptions::ResponseError => err
    print "ResponseError occurred: #{err}"
end
