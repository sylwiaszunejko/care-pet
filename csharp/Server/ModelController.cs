using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Cassandra; // DataStax Cassandra C# driver
using CarePet.Model; // Assuming your model namespace
using System.Net;

namespace CarePet.Server
{
    [ApiController]
    [Route("api")]
    public class ModelController : ControllerBase
    {
        private readonly ISession _session;
        private readonly Mapper _mapper;
        private readonly ILogger<ModelController> _logger;

        public ModelController(ISession session, ILogger<ModelController> logger)
        {
            _session = session;
            _mapper = new Mapper(session);
            _logger = logger;
        }

        private static void GroupBy(List<float> data, List<Measure> measures, int startHour, DateTime day, DateTime nowUtc)
        {
            bool sameDate = nowUtc.Date == day.Date;
            int lastHour = nowUtc.Hour;

            var ag = new (double value, int total)?[24];

            foreach (var m in measures)
            {
                int hour = m.Timestamp.UtcDateTime.Hour;

                if (!ag[hour].HasValue)
                    ag[hour] = (0, 0);

                var entry = ag[hour].Value;
                entry.total++;
                entry.value += m.Value;
                ag[hour] = entry;
            }

            for (int hour = startHour; hour < 24; hour++)
            {
                if (!sameDate || hour <= lastHour)
                {
                    if (!ag[hour].HasValue)
                        ag[hour] = (0, 0);
                }
            }

            for (int hour = startHour; hour < ag.Length && ag[hour].HasValue; hour++)
            {
                var entry = ag[hour].Value;
                data.Add(entry.total > 0 ? (float)(entry.value / entry.total) : 0f);
            }
        }

        [HttpGet("owner/{id}")]
        public ActionResult<Owner> Owner(Guid id)
        {
            return _mapper.Owner().Get(id);
        }

        [HttpGet("owner/{id}/pets")]
        public ActionResult<IEnumerable<Pet>> Pets(Guid id)
        {
            return Ok(_mapper.Pet().FindByOwner(id));
        }

        [HttpGet("pet/{id}/sensors")]
        public ActionResult<IEnumerable<Sensor>> Sensors(Guid id)
        {
            return Ok(_mapper.Sensor().FindByPet(id));
        }

        [HttpGet("sensor/{id}/values")]
        public ActionResult<IEnumerable<float>> Values(Guid id, [FromQuery] string from, [FromQuery] string to)
        {
            var resultSet = _mapper.Measurement().Find(id, DateTimeOffset.Parse(from).UtcDateTime, DateTimeOffset.Parse(to).UtcDateTime);
            var values = resultSet.Select(row => row.GetValue<float>(0)).ToList();
            return Ok(values);
        }

        [HttpGet("sensor/{id}/values/day/{day}")]
        public ActionResult<IEnumerable<float>> Avg(Guid id, string day)
        {
            var date = DateTime.Parse(day).Date;
            if (date > DateTime.UtcNow.Date)
            {
                return BadRequest("request into the future");
            }

            var resultSet = _mapper.SensorAvg().Find(id, date);
            var data = resultSet.Select(row => row.GetValue<float>(0)).ToList();

            if (data.Count != 24)
            {
                data = new List<float>(data);
                Aggregate(id, date, data);
            }

            return Ok(data);
        }

        public void Aggregate(Guid id, DateTime day, List<float> data)
        {
            var nowUtc = DateTime.UtcNow;

            if (day > nowUtc.Date)
            {
                throw new ArgumentException("request into the future");
            }

            int startHour = data.Count;
            var startDate = new DateTimeOffset(day, TimeSpan.Zero);
            var endDate = new DateTimeOffset(day.AddHours(23).AddMinutes(59).AddSeconds(59).AddTicks(9999999), TimeSpan.Zero);

            var measures = _mapper.Measurement()
                .FindWithTimestamps(id, startDate.UtcDateTime, endDate.UtcDateTime)
                .Select(row => new Measure(null, row.GetValue<DateTimeOffset>(0), row.GetValue<float>(1)))
                .ToList();

            int prevSize = data.Count;
            GroupBy(data, measures, startHour, day, nowUtc);
            SaveAggregate(id, data, prevSize, day, nowUtc);
        }

        private void SaveAggregate(Guid sensorId, List<float> data, int prevSize, DateTime day, DateTime nowUtc)
        {
            bool sameDate = nowUtc.Date == day.Date;
            int currentHour = nowUtc.Hour;

            for (int hour = prevSize; hour < data.Count; hour++)
            {
                if (sameDate && hour >= currentHour)
                    break;

                _mapper.SensorAvg().Create(new SensorAvg(sensorId, day, hour, data[hour]));
            }
        }

        [NonAction]
        public void Close()
        {
            _session.Dispose();
        }
    }
}
