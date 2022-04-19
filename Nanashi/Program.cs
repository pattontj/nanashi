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

        private readonly List<ulong> Moderators = new List<ulong>
        {

        };

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

            using (StreamReader sr = File.OpenText("Token.txt")) {
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
                                        message = message.Substring(0, begin) + kvp.Value + message.Substring(end+kvp.Key.Length);
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

                                await chan.SendMessageAsync(null, false, embed.Build(), null);
                            }
                        }
                    }
                }
            }
        }

        public void HandleModeratorAction(SocketMessage msg) 
        {

            Regex timeExpression = new Regex(@"(\d{0,}d)(\d{0,}h)(\d{0,}m)*");

          

            //   " /(ban\s)\d{1,}\g "

            if (msg.Content.Contains("ban")) {
                Console.WriteLine("ban");
            }

            //  Timeout regex    " /(\d{0,}d)*:(\d{0,}h)*:(\d{0,}m)*/g  "

            if (msg.Content.Contains("timeout")) {
                var matches = timeExpression.Match(msg.Content);
                Console.WriteLine("timeout test");

                
                var timelist = Regex.Split(msg.Content, @"\D+");

                foreach (var t in timelist)
                {
                    Console.WriteLine(t);
                }

              


            }
        }

    }
}
