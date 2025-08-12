using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cassandra; // DataStax Cassandra C# driver
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace CarePet
{
    public class Sensor
    {
        private static readonly ILogger<Sensor> LOG;
        private readonly SensorConfig _config;
        private readonly Owner _owner;
        private readonly Pet _pet;
        private readonly CarePet.Model.Sensor[] _sensors;

        static Sensor()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            LOG = loggerFactory.CreateLogger<Sensor>();
        }

        public Sensor(SensorConfig config)
        {
            _config = config;
            _owner = Owner.Random();
            _pet = Pet.Random(_owner.OwnerId);
            _sensors = new CarePet.Model.Sensor[Enum.GetValues(typeof(SensorType)).Length];

            for (int i = 0; i < _sensors.Length; i++)
            {
                _sensors[i] = CarePet.Model.Sensor.Random(_pet.PetId);
            }
        }

        public static void Main(string[] args)
        {
            var config = Config.Parse(new SensorConfig(), args);
            var client = new Sensor(config);
            client.Save();
            client.Run();
        }

        /// <summary>
        /// Initiates a connection with the configured keyspace.
        /// </summary>
        public ISession Keyspace()
        {
            return _config.Builder(Config.Keyspace).Build().Connect(Config.Keyspace);
        }

        /// <summary>
        /// Save owner, pet, and sensors to the database.
        /// </summary>
        private void Save()
        {
            using (var session = Keyspace())
            {
                var mapper = new Mapper(session); // Assuming you have a Mapper equivalent in C#
                LOG.LogInformation($"owner = {_owner}");
                LOG.LogInformation($"pet = {_pet}");

                mapper.Owner().Create(_owner);
                mapper.Pet().Create(_pet);

                foreach (var s in _sensors)
                {
                    LOG.LogInformation($"sensor = {s}");
                    mapper.Sensor().Create(s);
                }
            }
        }

        /// <summary>
        /// Generate random sensor data and push it to the database.
        /// </summary>
        private void Run()
        {
            using (var session = Keyspace())
            {
                var prepared = session.Prepare("INSERT INTO measurement (sensor_id, ts, value) VALUES (?, ?, ?)");
                var ms = new List<Measure>();
                var prev = DateTimeOffset.UtcNow;

                while (true)
                {
                    while ((DateTimeOffset.UtcNow - prev) < _config.BufferInterval)
                    {
                        if (!Sleep(_config.Measurement))
                            return;

                        foreach (var s in _sensors)
                        {
                            var m = ReadSensorData(s);
                            ms.Add(m);
                            LOG.LogInformation(m.ToString());
                        }
                    }

                    var elapsed = DateTimeOffset.UtcNow - prev;
                    var intervals = elapsed.Ticks / _config.BufferInterval.Ticks;
                    prev = prev.AddTicks(intervals * _config.BufferInterval.Ticks);

                    LOG.LogInformation("pushing data");

                    var batch = new BatchStatement();
                    foreach (var m in ms)
                    {
                        batch.Add(prepared.Bind(m.SensorId, m.Timestamp.UtcDateTime, m.Value));
                    }

                    session.Execute(batch);
                    ms.Clear();
                }
            }
        }

        private bool Sleep(TimeSpan interval)
        {
            try
            {
                Thread.Sleep(interval);
                return true;
            }
            catch (ThreadInterruptedException)
            {
                return false;
            }
        }

        private Measure ReadSensorData(CarePet.Model.Sensor s)
        {
            return new Measure(s.SensorId, DateTimeOffset.UtcNow, s.RandomData());
        }

        public class SensorConfig : Config
        {
            public TimeSpan BufferInterval { get; set; } = TimeSpan.FromHours(1);
            public TimeSpan Measurement { get; set; } = TimeSpan.FromMinutes(1);

            public static SensorConfig ParseArgs(string[] args)
            {
                var config = new SensorConfig();

                var rootCommand = new RootCommand
                {
                    new Option<TimeSpan>(
                        "--buffer-interval",
                        () => TimeSpan.FromHours(1),
                        "Buffer interval to accumulate measures"),
                    new Option<TimeSpan>(
                        "--measure",
                        () => TimeSpan.FromMinutes(1),
                        "Sensors measurement interval")
                };

                rootCommand.Handler = CommandHandler.Create<TimeSpan, TimeSpan>((bufferInterval, measure) =>
                {
                    config.BufferInterval = bufferInterval;
                    config.Measurement = measure;
                });

                rootCommand.Invoke(args);
                return config;
            }
        }
    }
}
