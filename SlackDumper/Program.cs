using CommandLine;
using Newtonsoft.Json;
using SlackDumper.Extensions;
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

        static async Task Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<Arguments>(args);

            if (parsed.Tag == ParserResultType.NotParsed)
            {
                return;
            }

            _arguments = ((Parsed<Arguments>)parsed).Value;
            _client = new HttpClient();

            if (string.IsNullOrEmpty(_arguments.OutputPath))
            {
                _arguments.OutputPath = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(_arguments.OutputPath))
            {
                Directory.CreateDirectory(_arguments.OutputPath);
            }

            var channels = await GetChannels();

            DumpChannels(channels);
        }

        private static async Task<Channel[]> GetChannels()
        {
            var channels = new List<Channel>();

            var queries = new Dictionary<string, object>
            {
                { "token", _arguments.Token }
            };

            while (true)
            {
                var query = queries
                    .Select(x => $"{x.Key}={x.Value}")
                    .Combine("&");

                var json = await _client.GetStringAsync($@"https://slack.com/api/conversations.list?{query}");

                var response = JsonConvert.DeserializeObject<Channels>(json);

                if (response.channels.Any())
                {
                    channels.AddRange(response.channels);
                }

                if (string.IsNullOrEmpty(response.response_metadata.next_cursor))
                {
                    break;
                }

                queries["cursor"] = response.response_metadata.next_cursor;
            }

            if (_arguments.Channles.Any())
            {
                var targetChannels = _arguments.Channles.ToHashSet();

                channels = channels
                    .Where(x => targetChannels.Contains(x.name))
                    .ToList();
            }

            return channels.ToArray();
        }

        private static async Task<string[]> GetMembers(Channel channel)
        {
            var members = new List<string>();

            var queries = new Dictionary<string, object>
            {
                { "token", _arguments.Token },
                { "channel", channel.id }
            };

            while (true)
            {
                var query = queries
                    .Select(x => $"{x.Key}={x.Value}")
                    .Combine("&");

                var json = await _client.GetStringAsync($@"https://slack.com/api/conversations.members?{query}");

                var response = JsonConvert.DeserializeObject<Members>(json);

                if (response.members.Any())
                {
                    members.AddRange(response.members);
                }

                if (string.IsNullOrEmpty(response.response_metadata.next_cursor))
                {
                    break;
                }

                queries["cursor"] = response.response_metadata.next_cursor;
            }

            return members.ToArray();
        }

        private static void DumpChannels(Channel[] channels)
        {
            var content = channels
                .Select(x => new
                {
                    x.id,
                    x.name,
                    x.created,
                    x.creator,
                    x.is_archived,
                    x.is_general,
                    members = GetMembers(x).Result,
                    x.topic,
                    x.purpose
                })
                .ToArray();

            var json = JsonConvert.SerializeObject(content, Formatting.Indented);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            using (var file = File.Create(Path.Combine(_arguments.OutputPath, "channels.json")))
            {
                stream.CopyTo(file);
            }
        }
    }
}
