using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using com.rhfung.P2PDictionary.Peers;
using CommandLine;

namespace com.rhfung.P2PDictionary
{
    class Program
    {
        class Options
        {
            [Option('m', "description", Default = "", HelpText = "Description for the server")]
            public String Description { get; set; }

            [Option('p', "port", Default = 8765, HelpText = "Bind to port")]
            public int Port { get; set; }

            [Option('s', "namespace", Required = true, HelpText = "Namespace for the server")]
            public string Namespace { get; set; }

            [Option('t', "timespan", Default = 1500, HelpText = "Search interval for clients")]
            public int Timespan { get; set; }

            [Option('d', "discovery", HelpText = "Specify a backend discovery mechanism")]
            public string Discovery { get; set;  }

            [Option(HelpText = "Monitors no patterns")]
            public bool NoPattern { get; set; }

            [Option("pattern", HelpText = "Monitors a specific pattern using wildcard (*), single character (?), and number (#) placeholders; default to *")]
            public IEnumerable<string> Pattern { get; set; }

            [Option('n', HelpText = "Provide clients in the form host:port")]
            public IEnumerable<string> Node { get; set; }

            [Option()]
            public bool Debug { get; set; }

            [Option()]
            public bool FullDebug { get; set; }
        }

        static void Main(string[] args)
        {
            var parser = new Parser(config => {
                config.EnableDashDash = true;
                config.HelpWriter = Console.Out;
            });
            var result = parser.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndExit(opts));
        }

        static void RunOptionsAndExit(Options opts)
        {
            P2PDictionary dict = new P2PDictionary(
                opts.Description,
                opts.Port,
                opts.Namespace,
                searchForClients: opts.Timespan,
                peerDiscovery: new NoDiscovery());
            
            if (opts.FullDebug)
            {
                dict.SetDebugBuffer(Console.Out, 0, true);
            }
            else if (opts.Debug)
            {
                dict.SetDebugBuffer(Console.Out, 1, true);
            }

            if (!opts.NoPattern)
            {
                bool hasAddedPattern = false;
                foreach (var pattern in opts.Pattern)
                {
                    hasAddedPattern = true;
                    dict.AddSubscription(pattern);
                }

                if (!hasAddedPattern)
                {
                    dict.AddSubscription("*");
                }
            }

            foreach(var node in opts.Node)
            {
                var splitStr = node.Split(':');
                var address = IPAddress.Parse(splitStr[0]);
                dict.OpenClient(address, Int32.Parse(splitStr[1]));
            }

            bool cancelled = false;

            Console.Out.WriteLine("Server started");
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => {
                Console.Out.WriteLine("Cancel pressed");
                cancelled = true;
                e.Cancel = true;
            };
            
            while (!cancelled)
            {
                try
                {
                    Thread.Sleep(1000);
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
            }

            dict.Close();

            Console.Out.WriteLine("Server finished");
        }
    }
}
