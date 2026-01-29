using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Cortex.Streams.AWSSQS.Serializers;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace Cortex.Streams.AWSSQS
{
    public class SQSSinkOperator<TInput> : ISinkOperator<TInput>
    {

        private readonly string _queueUrl;
        private readonly IAmazonSQS _sqsClient;
        private readonly ISerializer<TInput> _serializer;
        private readonly ILogger<SQSSinkOperator<TInput>> _logger;

        public SQSSinkOperator(
            string queueUrl,
            RegionEndpoint region = null,
            ISerializer<TInput> serializer = null,
            ILogger<SQSSinkOperator<TInput>> logger = null)
        {
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));

            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<SQSSinkOperator<TInput>>.Instance;

            _sqsClient = new AmazonSQSClient(region ?? RegionEndpoint.USEast1);
        }

        public void Process(TInput input)
        {
            Task.Run(() => SendMessageAsync(input));
        }

        /// <summary>
        /// Sends a serialized message to AWS SQS asynchronously.
        /// </summary>
        /// <param name="obj">The input object to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SendMessageAsync(TInput obj)
        {
            var serializedMessage = _serializer.Serialize(obj);
            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = serializedMessage
            };

            try
            {
                var response = await _sqsClient.SendMessageAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to SQS queue {QueueUrl}", _queueUrl);
            }
        }

        public void Start()
        {
            // Any initialization if necessary
        }

        public void Stop()
        {
            _sqsClient?.Dispose();
        }
    }
}
