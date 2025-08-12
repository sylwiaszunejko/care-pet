using System;
using System.Collections.Generic;
using Cassandra.Mapping;

namespace CarePet.Model
{
    public class SensorDAO
    {
        private readonly IMapper _mapper;

        public SensorDAO(IMapper mapper)
        {
            _mapper = mapper;
        }

        public void Create(Sensor sensor)
        {
            _mapper.Insert(sensor);
        }

        public void Update(Sensor sensor)
        {
            _mapper.Update(sensor);
        }

        public Sensor Get(Guid petId, Guid sensorId)
        {
            return _mapper.FirstOrDefault<Sensor>(
                "WHERE pet_id = ? AND sensor_id = ?",
                petId,
                sensorId
            );
        }

        public IEnumerable<Sensor> FindByPet(Guid petId)
        {
            return _mapper.Fetch<Sensor>(
                "WHERE pet_id = ?",
                petId
            );
        }
    }
}
