using CommandLine;
using System.Collections.Generic;

namespace SlackDumper.Models
{
    public class Arguments
    {
        [Option('t', "token", Required = true, HelpText = "Slack API Legacy Token.")]
        public string Token { get; set; }

        [Option('c', "channles", Required = false, Separator = ',', HelpText = "Output target channels.")]
        public IEnumerable<string> Channles { get; set; }

        [Option('o', "output", Required = false, Default = "/")]
        public string OutputPath { get; set; }
    }
}
