using CommandLine;
using Newtonsoft.Json;
using SlackDumper.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SlackDumper
{
    class Program
    {
        private static Arguments _arguments;
        private static HttpClient _client;

        private static IReadOnlyDictionary<string, object>[] _members;
        private static IReadOnlyDictionary<string, object>[] _channels;
        private static ILookup<string, string> _channelMembers;

        private static DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static async Task Main(string[] args)
        {
            Console.WriteLine($"Start SlackDumper at {DateTimeOffset.Now:yyyy/MM/dd HH:mm:ss}");

            var parsed = Parser.Default.ParseArguments<Arguments>(args);

            if (parsed.Tag == ParserResultType.NotParsed)
            {
                return;
            }

            _arguments = ((Parsed<Arguments>)parsed).Value;
            _client = new HttpClient();

            if (string.IsNullOrEmpty(_arguments.OutputPath))
            {
                var basePath = Directory.GetCurrentDirectory();
                var outputPath = Path.Combine(basePath, DateTime.Now.ToString("yyyyMMddHHmmss"));

                _arguments.OutputPath = outputPath;
            }

            if (!Directory.Exists(_arguments.OutputPath))
            {
                Directory.CreateDirectory(_arguments.OutputPath);
            }

            Console.WriteLine($"Token:{_arguments.Token}");
            Console.WriteLine($"OutputPath:{_arguments.OutputPath}");

            if (_arguments.Channles.Any())
            {
                var channels = string.Join(",", _arguments.Channles);

                Console.WriteLine($"Target Channels:{channels}");
            }

            await FetchBaseInformations();

            await DumpUsers();
            await DumpChannels();

            await FetchChannels();

            Console.WriteLine($"End SlackDumper at {DateTimeOffset.Now:yyyy/MM/dd HH:mm:ss}");
            Console.ReadLine();
        }

        private static async Task FetchBaseInformations()
        {
            // Users
            {
                Console.WriteLine("Getting users.");

                var json = await _client.GetStringAsync($@"https://slack.com/api/users.list?token={_arguments.Token}");
                var usersList = JsonConvert.DeserializeObject<UsersList>(json);

                _members = usersList.members;
            }

            await Task.Delay(100);

            // Conversations
            {
                Console.WriteLine("Getting channels.");

                var json = await _client.GetStringAsync($@"https://slack.com/api/conversations.list?token={_arguments.Token}&types=public_channel,private_channel");
                var conversationsList = JsonConvert.DeserializeObject<ConversationsList>(json);
                
                _channels = _arguments.Channles.Any()
                    ? conversationsList.channels.Where(x => _arguments.Channles.Contains((string)x["name"])).ToArray()
                    : conversationsList.channels;
            }

            await Task.Delay(100);

            // Conversation Members
            {
                Console.WriteLine("Getting members of channels.");

                var channelMembers = new Dictionary<string, string[]>();

                foreach (var channel in _channels)
                {
                    var json = await _client.GetStringAsync($@"https://slack.com/api/conversations.members?token={_arguments.Token}&channel={(string)channel["id"]}");
                    var conversationMembers = JsonConvert.DeserializeObject<ConversationsMembers>(json);

                    channelMembers.Add((string)channel["id"], conversationMembers.members);

                    await Task.Delay(100);
                }

                _channelMembers = channelMembers
                    .SelectMany(x => x.Value.Select(y => (key:x.Key, value:y)))
                    .ToLookup(x => x.key, x => x.value);
            }
        }

        private static async Task DumpUsers()
        {
            Console.WriteLine($"Dumping users.");

            var json = JsonConvert.SerializeObject(_members, Formatting.Indented);

            using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            using (var file = File.Create(Path.Combine(_arguments.OutputPath, "users.json")))
            {
                await memory.CopyToAsync(file);
            }
        }

        private static async Task DumpChannels()
        {
            Console.WriteLine($"Dumping channels.");

            var json = JsonConvert.SerializeObject(_channels, Formatting.Indented);

            using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            using (var file = File.Create(Path.Combine(_arguments.OutputPath, "channels.json")))
            {
                await memory.CopyToAsync(file);
            }
        }

        private static async Task FetchChannels()
        {
            foreach (var channel in _channels)
            {
                Console.WriteLine($"Fetching {(string)channel["name"]}.");

                var messages = await GetHistory((string)channel["id"]);

                if (messages.Any())
                {
                    var outputPath = Path.Combine(_arguments.OutputPath, (string)channel["name"]);

                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                    }

                    await DumpMessages(outputPath, messages);
                }
            }
        }

        private static async Task<IReadOnlyDictionary<string, object>[]> GetHistory(string id)
        {
            var messages = new List<IReadOnlyDictionary<string, object>>();
            var cursor = "";

            while (true)
            {
                var json = string.IsNullOrEmpty(cursor)
                    ? await _client.GetStringAsync($@"https://slack.com/api/conversations.history?token={_arguments.Token}&channel={id}&limit=1000")
                    : await _client.GetStringAsync($@"https://slack.com/api/conversations.history?token={_arguments.Token}&channel={id}&cursor={cursor}&limit=1000");
                var conversationsHistory = JsonConvert.DeserializeObject<ConversationsHistory>(json);

                messages.AddRange(conversationsHistory.messages);

                if (!conversationsHistory.has_more)
                {
                    break;
                }

                await Task.Delay(1000);

                cursor = conversationsHistory.response_metadata.next_cursor;
            }

            return messages.ToArray();
        }

        private static async Task DumpMessages(string outputPath, IReadOnlyDictionary<string, object>[] messages)
        {
            async Task writeFile(string outputFilePath, IReadOnlyDictionary<string, object>[] targetMessages)
            {
                var json = JsonConvert.SerializeObject(targetMessages, Formatting.Indented);

                using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                using (var file = File.Create(outputFilePath))
                {
                    await memory.CopyToAsync(file);
                }
            }

            var currentFileDate = "";
            var currentMessages = new List<IReadOnlyDictionary<string, object>>();

            foreach (var message in messages)
            {
                var timestamp = _unixEpoch.Add(TimeSpan.FromSeconds(double.Parse((string)message["ts"])));
                var fileDate = timestamp.ToString("yyyy-MM-dd");

                if (string.IsNullOrEmpty(currentFileDate))
                {
                    currentFileDate = fileDate;
                }

                if (fileDate != currentFileDate)
                {
                    await writeFile(Path.Combine(outputPath, $"{currentFileDate}.json"), currentMessages.ToArray());

                    currentFileDate = "";
                    currentMessages.Clear();
                }

                currentMessages.Add(message);
            }

            await writeFile(Path.Combine(outputPath, $"{currentFileDate}.json"), currentMessages.ToArray());
        }
    }
}
