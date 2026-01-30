using Amazon.S3;
using Amazon.S3.Transfer;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Streams.S3.Serializers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Text;

namespace Cortex.Streams.S3
{
    /// <summary>
    /// AWS S3 Sink Operator that writes serialized data to an S3 bucket.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class S3SinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"S3SinkOperator<{typeof(TInput).Name}>";

        private readonly string _bucketName;
        private readonly string _folderPath;
        private readonly ISerializer<TInput> _serializer;
        private readonly IAmazonS3 _s3Client;
        private readonly TransferUtility _transferUtility;
        private readonly ILogger<S3SinkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3SinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="bucketName">Name of the S3 bucket.</param>
        /// <param name="folderPath">Path within the bucket to store data (e.g., "data/ingest").</param>
        /// <param name="s3Client">Instance of IAmazonS3 for interacting with AWS S3.</param>
        /// <param name="serializer">Serializer to convert TInput objects to strings. Default is DefaultJsonSerializer</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public S3SinkOperator(
            string bucketName,
            string folderPath,
            IAmazonS3 s3Client,
            ISerializer<TInput>? serializer = null,
            ILogger<S3SinkOperator<TInput>>? logger = null)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _folderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));

            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<S3SinkOperator<TInput>>.Instance;

            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _transferUtility = new TransferUtility(_s3Client);
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(S3SinkOperator<TInput>));
            if (_isRunning) return;

            _isRunning = true;
            _logger.LogInformation("S3SinkOperator started for bucket {BucketName}", _bucketName);
        }

        /// <summary>
        /// Processes the input object by serializing it and sending it to AWS S3.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(S3SinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("S3SinkOperator is not running. Call Start() before processing messages");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("S3SinkOperator received null input. Skipping");
                return;
            }

            // Use core error handling for message processing
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)UploadToS3);
        }

        private void UploadToS3(TInput input)
        {
            var serializedMessage = _serializer.Serialize(input);
            var fileName = $"{Guid.NewGuid()}.json";
            var key = $"{_folderPath}/{fileName}";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = key,
                BucketName = _bucketName,
                ContentType = "application/json"
            };

            _transferUtility.Upload(uploadRequest);
            _logger.LogDebug("Uploaded message to S3 bucket {BucketName} at key {Key}", _bucketName, key);
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            _isRunning = false;
            _logger.LogInformation("S3SinkOperator stopped for bucket {BucketName}", _bucketName);
        }

        /// <summary>
        /// Disposes the AWS S3 client and transfer utility.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _transferUtility?.Dispose();
            _s3Client?.Dispose();
        }
    }
}
