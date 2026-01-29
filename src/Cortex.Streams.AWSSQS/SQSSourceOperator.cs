using Amazon.SQS.Model;
using Amazon.SQS;
using Cortex.Streams.Operators;
using System.Threading.Tasks;
using System.Threading;
using System;
using Cortex.Streams.AWSSQS.Deserializers;
using Amazon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Streams.AWSSQS
{
    public class SQSSourceOperator<TOutput> : ISourceOperator<TOutput>
    {
        private readonly string _queueUrl;
        private readonly IAmazonSQS _sqsClient;
        private readonly IDeserializer<TOutput> _deserializer;
        private readonly ILogger<SQSSourceOperator<TOutput>> _logger;
        private CancellationTokenSource _cancellationTokenSource;

        public SQSSourceOperator(
            string queueUrl,
            IDeserializer<TOutput> deserializer = null,
            RegionEndpoint region = null,
            ILogger<SQSSourceOperator<TOutput>> logger = null)
        {
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));

            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<SQSSourceOperator<TOutput>>.Instance;

            _sqsClient = new AmazonSQSClient(region ?? RegionEndpoint.USEast1);
        }

        public void Start(Action<TOutput> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => PollMessagesAsync(emit, _cancellationTokenSource.Token));
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task PollMessagesAsync(Action<TOutput> emit, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20 // Long polling
                };

                try
                {
                    var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);
                    foreach (var message in response.Messages)
                    {
                        try
                        {
                            var obj = _deserializer.Deserialize(message.Body);
                            emit(obj);

                            // Optionally delete the message after successful processing
                            await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Deserialization or processing failed for SQS message from queue {QueueUrl}", _queueUrl);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving messages from SQS queue {QueueUrl}", _queueUrl);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying
                }
            }
        }
    }
}
