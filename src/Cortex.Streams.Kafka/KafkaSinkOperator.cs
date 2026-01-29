using Confluent.Kafka;
using Cortex.Streams.Kafka.Serializers;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Cortex.Streams.Kafka
{
    public sealed class KafkaSinkOperator<TInput> : ISinkOperator<TInput>
    {
        private readonly string _bootstrapServers;
        private readonly string _topic;
        private readonly IProducer<Null, TInput> _producer;
        private readonly ILogger<KafkaSinkOperator<TInput>> _logger;

        public KafkaSinkOperator(
            string bootstrapServers,
            string topic,
            ProducerConfig config = null,
            ISerializer<TInput> serializer = null,
            ILogger<KafkaSinkOperator<TInput>> logger = null)
        {
            _bootstrapServers = bootstrapServers;
            _topic = topic;
            _logger = logger ?? NullLogger<KafkaSinkOperator<TInput>>.Instance;

            var producerConfig = config ?? new ProducerConfig
            {
                BootstrapServers = _bootstrapServers
            };

            if (serializer == null)
                serializer = new DefaultJsonSerializer<TInput>();

            _producer = new ProducerBuilder<Null, TInput>(producerConfig)
                .SetValueSerializer(serializer)
                .Build();
        }

        public void Process(TInput input)
        {
            _producer.Produce(_topic, new Message<Null, TInput> { Value = input }, deliveryReport =>
            {
                if (deliveryReport.Error.IsError)
                {
                    _logger.LogError("Kafka delivery error to topic {Topic}: {Reason}", _topic, deliveryReport.Error.Reason);
                }
            });
        }

        public void Start()
        {
            // Any initialization if necessary
        }

        public void Stop()
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
    }
}
