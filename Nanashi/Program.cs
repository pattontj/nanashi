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


        private Dictionary<string, string> filterWords = new Dictionary<string, string>
            {
            // Add word filters here
            {"~", ":woozy_face:" },
            {"∼", ":woozy_face:" },
            {"simp", "chad" },
            };

        private List<ulong> Moderators = new List<ulong>
        {

        };

        public static void Main(string[] args)
         => new Program().MainAsync(args[0]).GetAwaiter().GetResult();

        public async Task MainAsync(string token)
        {
            if (!File.Exists("postcount.txt"))
                File.CreateText("postcount.txt");

            using (StreamReader sr = File.OpenText("postcount.txt"))
            {
                string s = sr.ReadLine();
                if (s == null)
                    throw new NotImplementedException("post count not found");
                else
                {
                    globalPostCount = UInt64.Parse(s);
                }

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
    }
}
