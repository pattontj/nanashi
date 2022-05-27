using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using System.Xml;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Timers;
using System.Threading;
using Microsoft.VisualBasic;
using Discord.Rest;

namespace Nanashi
{
    [Serializable]
    [XmlRoot(ElementName = "post")]
    public class Post
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }
        [XmlAttribute("PostID")]
        public ulong PostID { get; set; }
        [XmlAttribute("DiscordID")]
        public ulong DiscordUID { get; set; }
    };

    public class Program
    {
        private DiscordSocketClient client;

        private ulong globalPostCount = 0;


        private readonly Dictionary<string, string> filterWords = new Dictionary<string, string>
            {
            // Add word filters here
            {"~", ":woozy_face:" },
            {"∼", ":woozy_face:" },
            {"simp", "chad" },
            };

        // TODO: load this from a file or something
        private readonly List<ulong> Moderators = new List<ulong>
        {

        };


        // only hold users in runtime memory (no logging)
        //                          msgID, userID
        private readonly Dictionary<ulong, ulong> posters = new Dictionary<ulong, ulong> {
            
        };


        private readonly Dictionary<ulong, System.Timers.Timer> Timeouts = new Dictionary<ulong, System.Timers.Timer> { };
        private readonly List<ulong> Bans = new List<ulong> { };


        public static void Main()
         => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {

            /* if (!File.Exists("postcount.txt"))
                Console.WriteLine("No postcounts?");
                File.CreateText("postcount.txt");

    */
            using (StreamReader sr = File.OpenText("postcount.txt"))
            {
                string s = sr.ReadLine();
                if (s == null)
                    throw new NotImplementedException("post count not found");
                else
                {
                    globalPostCount = UInt64.Parse(s);
                }
                sr.Close();
            }


            // Get token from file

            String token;

            using (StreamReader sr = File.OpenText("Token.txt"))
            {
                string tk = sr.ReadLine();



                if (tk == null)
                    throw new NotImplementedException("Token not found");
                else
                {
                    token = tk;
                }
                sr.Close();
            }

            client = new DiscordSocketClient();

            await client.LoginAsync(TokenType.Bot,
                token,
                true);
            Console.WriteLine("Successfully connected as {client.CurrentUser.Username}");

            client.MessageReceived += MessageReceived;

            Console.WriteLine("Starting async...");
            await client.StartAsync();
            Console.WriteLine("Everything is operational");

            await Task.Delay(-1);
        }

        public async Task MessageReceived(SocketMessage msg)
        {
            // put everything inside this or big spam occurs
            if (msg.Author.Id != client.CurrentUser.Id)
            {

                if (msg.Content[0] == '!')
                {
                    HandleModeratorAction(msg);
                    return;
                }

                if (msg.Channel.GetType().Equals(typeof(SocketDMChannel)))
                {

                    if (Bans.Contains(msg.Author.Id) || Timeouts.ContainsKey(msg.Author.Id))
                    {
                        return;
                    }


                    globalPostCount += 1;
                    // await msg.Channel.SendMessageAsync("Got DM");
                    // Console.WriteLine("Post No. " + globalPostCount + ", user: " + msg.Author.Username);


                    XmlSerializer Serializer = new XmlSerializer(typeof(Post));
                    StringBuilder sb = new StringBuilder();
                    StringWriter sww = new StringWriter(sb);
                    XmlSerializerNamespaces ns = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
                    ns.Add("", "");

                    Serializer.Serialize(sww, new Post
                    {
                        Name = msg.Author.Username,
                        PostID = globalPostCount,
                        DiscordUID = msg.Author.Id
                    },
                    ns
                    );
                    string xmlResult = sww.GetStringBuilder().ToString().Replace("<? xml version = \"1.0\" encoding = \"utf-16\"?>", " ");



                    File.AppendAllText("posts.db", xmlResult);


                    foreach (var guild in client.Guilds)
                    {
                        foreach (var channel in guild.Channels)
                        {
                            var chan = channel as ITextChannel;

                            if (chan?.Name == "anonymous")
                            {
                                var embed = new EmbedBuilder();
                                Attachment image;
                                var message = msg.Content;

                                if (msg.Attachments.Count > 0)
                                {
                                    image = msg.Attachments.ElementAt(0);
                                    embed.WithThumbnailUrl(image.Url);


                                    //message += "\n" + image.Url;
                                }
                                else
                                    image = null;

                                foreach (var kvp in filterWords)
                                {
                                    var lowerMsg = message.ToLower();

                                    if (lowerMsg.Contains(kvp.Key))
                                    {
                                        var begin = lowerMsg.IndexOf(kvp.Key);
                                        var end = lowerMsg.LastIndexOf(kvp.Key);
                                        message = message.Substring(0, begin) + kvp.Value + message.Substring(end + kvp.Key.Length);
                                    }
                                    // message = message.Replace(kvp.Key, kvp.Value);
                                }


                                // 1 7 1 4 5 1 8

                                if (message == "")
                                    message = ".";


                                embed.WithDescription("No. " + (globalPostCount));
                                embed.AddField("anonymous", message, false);

                                using (StreamWriter sw = File.CreateText("postcount.txt"))
                                {
                                    await sw.WriteAsync(globalPostCount.ToString());
                                    sw.Close();
                                }

                                var sentMsg = await chan.SendMessageAsync(null, false, embed.Build(), null);

                                posters.Add(sentMsg.Id, msg.Author.Id);
                            }
                        }
                    }
                }
            }
        }

        public void HandleModeratorAction(SocketMessage msg)
        {
            Regex timeExpression = new Regex(@"^(?:\d+(?::[0-5][0-9]:[0-5][0-9])?|[0-5]?[0-9]:[0-5][0-9])$");

            if ( !Moderators.Contains(msg.Author.Id) )
            {
                return;
            }

            if (msg.Content.Contains("ban"))
            {
                Console.WriteLine("ban");
                BanUser(0);
            }

            //  Timeout regex    " /(\d{0,}d)*:(\d{0,}h)*:(\d{0,}m)*/g  "

            if (msg.Content.Contains("timeout"))
            {
                string[] cmdArgs = msg.Content.Split(' ');

                if (cmdArgs.Length != 3)
                {
                    msg.Channel.SendMessageAsync("command failed, must use format [postID] [hh:mm:ss]");
                    return;
                }

                var msgID = ulong.Parse(cmdArgs[1]);
                string timestamp = cmdArgs[2];
                Console.WriteLine(msgID);
                Console.WriteLine(timestamp);

                ulong pstr;
                try
                {
                    pstr = posters[msgID];
                }
                catch
                {
                    msg.Channel.SendMessageAsync("Poster not found");
                    return;
                }


                var timelist = Array.ConvertAll<string, int>(
                        Regex.Split(timestamp, @"\D+"),
                        item => Int32.Parse(item)
                    );

                var timeSeconds = 0;

                for (int i = 0, e = 3600; i < timelist.Length; i++)
                {
                    timeSeconds += timelist[i] * e;
                    e /= 60;
                }

                var timeoutTimer = new System.Timers.Timer(timeSeconds*1000);
                timeoutTimer.Elapsed += new System.Timers.ElapsedEventHandler(
                    (object source, ElapsedEventArgs e) =>
                    {
                        timeoutTimer.Stop();
                        Timeouts.Remove(pstr);
                    });
                timeoutTimer.Enabled = true;

                Timeouts.Add(pstr, timeoutTimer);
                msg.Channel.SendMessageAsync("message ID " + msgID + " has been timed out for " + timeSeconds + " seconds");

                EditMessageWithTimeout(msgID, timestamp);

            }
        }


        async public void EditMessageWithTimeout(ulong postID, string timestamp)
        {
            foreach (var guild in client.Guilds)
            {
                foreach (var channel in guild.Channels)
                {
                    var chan = channel as ITextChannel;

                    if (chan?.Name == "anonymous")
                    {
                        var msg = await chan.GetMessageAsync(id: postID);

                        var iMsg = (RestUserMessage)msg;

                        if (msg.Author.Id == client.CurrentUser.Id)
                        {
                            var embed = new EmbedBuilder();

                            embed.WithDescription(msg.Embeds.First().Description);
                            if (msg.Embeds.First().Fields.Length != 0)
                            {
                                var nam = msg.Embeds.First().Fields.First().Name;
                                nam = (nam == null) ? nam : "Punished Guca";
                                var val = msg.Embeds.First().Fields.First().Value;
                                val = (val == null) ? val : "Press F";

                                embed.AddField(nam, val, false);
                            }
                            embed.AddField("USER WAS TIMED OUT FOR THIS POST", timestamp, false);

                            await iMsg.ModifyAsync(m => { m.Embed = embed.Build(); });
                        }
                    }

                }
            }
        }



        public void BanUser(int postCode)
        {
            // XML stuff here
            Console.WriteLine("test");

            XmlDocument posts = new XmlDocument();
            posts.Load("posts.db");

            XmlNode root = posts.ChildNodes[0];

            Console.WriteLine("test 2");


            foreach (var post in posts)
            {
                Console.WriteLine(post);
            }

        }

        public void SerializePost(Post post)
        {

            XmlDocument doc = new XmlDocument();
            doc.Load("posts.db");

            XmlNode root = doc.ChildNodes[0];

        }

    }
}


