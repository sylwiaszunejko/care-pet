using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.CommandLine;
using System.CommandLine.Invocation;
using Cassandra; // DataStax Cassandra C# driver
using System.Reflection;

namespace CarePet
{
    public class Config
    {
        public const string Keyspace = "carepet";
        private const string ApplicationName = "care-pet";
        private static readonly Guid ClientId = Guid.NewGuid();
        private const int DefaultPort = 9042;

        // Command-line options
        public string[] Hosts { get; set; }
        public string Datacenter { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Help { get; set; }

        /// <summary>
        /// Parses arguments into a new instance of Config.
        /// </summary>
        public static T Parse<T>(T command, string[] args) where T : class
        {
            var rootCommand = new RootCommand
            {
                new Option<string[]>(
                    "--hosts",
                    description: "Database contact points"),
                new Option<string>(
                    new[] {"-dc", "--datacenter"},
                    description: "Local datacenter name for default profile"),
                new Option<string>(
                    new[] {"-u", "--username"},
                    description: "Password based authentication username"),
                new Option<string>(
                    new[] {"-p", "--password"},
                    description: "Password based authentication password"),
                new Option<bool>(
                    new[] {"-h", "--help"},
                    description: "Display a help message")
            };

            rootCommand.Handler = CommandHandler.Create<string[], string, string, string, bool>(
                (hosts, datacenter, username, password, help) =>
                {
                    if (command is Config cfg)
                    {
                        cfg.Hosts = hosts;
                        cfg.Datacenter = datacenter;
                        cfg.Username = username;
                        cfg.Password = password;
                        cfg.Help = help;
                    }
                });

            rootCommand.Invoke(args);

            if ((command as Config)?.Help == true)
            {
                rootCommand.Invoke("-h");
                Environment.Exit(1);
            }

            return command;
        }

        /// <summary>
        /// Transforms an address of the form host:port into an IPEndPoint.
        /// </summary>
        public static IPEndPoint Resolve(string addr)
        {
            var addressWithPort = WithPort(addr, DefaultPort);
            var parts = addressWithPort.Split(':');

            if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
            {
                throw new UriFormatException("URI must have host and port");
            }

            return new IPEndPoint(Dns.GetHostAddresses(parts[0]).First(), port);
        }

        /// <summary>
        /// Ensures an address has port provided.
        /// </summary>
        private static string WithPort(string addr, int port)
        {
            if (!addr.Contains(":"))
            {
                return $"{addr}:{port}";
            }
            return addr;
        }

        /// <summary>
        /// Check if string is null or empty.
        /// </summary>
        public static bool IsNullOrEmpty(string s)
        {
            return string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// Loads a resource content.
        /// </summary>
        public static string GetResource(string name)
        {
            return GetResourceFileAsString(name);
        }

        /// <summary>
        /// Reads given resource file as a string.
        /// </summary>
        private static string GetResourceFileAsString(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(r => r.EndsWith(name, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null) return null;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Builds configured Cassandra Cluster builder to acquire a new session.
        /// </summary>
        public Cluster.Builder Builder(string keyspace = null)
        {
            var builder = Cluster.Builder();

            if (Hosts != null && Hosts.Length > 0)
            {
                builder = builder.AddContactPoints(Hosts)
                                 .WithLoadBalancingPolicy(new DCAwareRoundRobinPolicy(Datacenter));
            }

            if (!IsNullOrEmpty(Username))
            {
                builder = builder.WithCredentials(Username, Password);
            }

            // Note: C# driver sets keyspace when creating session
            return builder;
        }
    }
}
