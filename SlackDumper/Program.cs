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

        private static Member[] _members;
        private static Channel[] _channels;
        private static ILookup<string, string> _channelMembers;
        private static IReadOnlyDictionary<string, string> _userNamesById;
        private static IReadOnlyDictionary<string, string> _userIdsByName;

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
                    ? conversationsList.channels.Where(x => _arguments.Channles.Contains(x.name)).ToArray()
                    : conversationsList.channels;
            }

            await Task.Delay(100);

            // Conversation Members
            {
                Console.WriteLine("Getting members of channels.");

                var channelMembers = new Dictionary<string, string[]>();

                foreach (var channel in _channels)
                {
                    var json = await _client.GetStringAsync($@"https://slack.com/api/conversations.members?token={_arguments.Token}&channel={channel.id}");
                    var conversationMembers = JsonConvert.DeserializeObject<ConversationsMembers>(json);

                    channelMembers.Add(channel.id, conversationMembers.members);

                    await Task.Delay(100);
                }

                _channelMembers = channelMembers
                    .SelectMany(x => x.Value.Select(y => (key:x.Key, value:y)))
                    .ToLookup(x => x.key, x => x.value);
            }

            _userNamesById = _members.ToDictionary(x => x.id, x => x.name);
            _userIdsByName = _members.ToDictionary(x => x.name, x => x.id);
        }

        private static async Task DumpUsers()
        {
            Console.WriteLine($"Dumping users.");

            var targets = _members
                .Select(x => new
                {
                    x.id,
                    x.team_id,
                    x.name,
                    x.deleted,
                    x.profile,
                    x.is_admin,
                    x.is_owner,
                    x.is_primary_owner,
                    x.is_restricted,
                    x.is_ultra_restricted,
                    x.is_bot,
                    x.is_app_user,
                    x.updated
                })
                .ToArray();

            var json = JsonConvert.SerializeObject(targets, Formatting.Indented);

            using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            using (var file = File.Create(Path.Combine(_arguments.OutputPath, "users.json")))
            {
                await memory.CopyToAsync(file);
            }
        }

        private static async Task DumpChannels()
        {
            Console.WriteLine($"Dumping channels.");

            var targets = _channels
                .Select(x => new
                {
                    x.id,
                    x.name,
                    x.created,
                    x.creator,
                    x.is_archived,
                    x.is_general,
                    members = _channelMembers[x.id],
                    x.topic,
                    x.purpose
                })
                .ToArray();

            var json = JsonConvert.SerializeObject(targets, Formatting.Indented);

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
                Console.WriteLine($"Fetching {channel.name}.");

                var messages = await GetHistory(channel.id);

                if (messages.Any())
                {
                    var outputPath = Path.Combine(_arguments.OutputPath, channel.name);

                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                    }

                    await DumpMessages(outputPath, messages);
                }
            }
        }

        private static async Task<Message[]> GetHistory(string id)
        {
            var messages = new List<Message>();
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

        private static async Task DumpMessages(string outputPath, Message[] messages)
        {
            async Task writeFile(string outputFilePath, Message[] targetMessages)
            {
                var json = JsonConvert.SerializeObject(targetMessages, Formatting.Indented);

                using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                using (var file = File.Create(outputFilePath))
                {
                    await memory.CopyToAsync(file);
                }
            }

            var currentFileDate = "";
            var currentMessages = new List<Message>();

            foreach (var message in messages)
            {
                var timestamp = _unixEpoch.Add(TimeSpan.FromSeconds(double.Parse(message.ts)));
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
