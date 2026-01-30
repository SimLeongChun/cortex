using Amazon.S3;
using Amazon.S3.Transfer;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Streams.S3.Serializers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cortex.Streams.S3
{
    /// <summary>
    /// AWS S3 Bulk Sink Operator that batches and writes serialized data to an S3 bucket.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class S3SinkBulkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"S3SinkBulkOperator<{typeof(TInput).Name}>";

        private readonly string _bucketName;
        private readonly string _folderPath;
        private readonly ISerializer<TInput> _serializer;
        private readonly IAmazonS3 _s3Client;
        private readonly TransferUtility _transferUtility;
        private readonly ILogger<S3SinkBulkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _isRunning;
        private bool _disposed;

        // Bulk parameters
        private List<TInput> _buffer = new List<TInput>();
        private readonly int _batchSize;
        private readonly TimeSpan _flushInterval;
        private Timer _timer;
        private readonly object _bufferLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="S3SinkBulkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="bucketName">Name of the S3 bucket.</param>
        /// <param name="folderPath">Path within the bucket to store data (e.g., "data/ingest").</param>
        /// <param name="s3Client">Instance of IAmazonS3 for interacting with AWS S3.</param>
        /// <param name="serializer">Serializer to convert TInput objects to strings. Default is DefaultJsonSerializer</param>
        /// <param name="batchSize">Number of items to batch before uploading.</param>
        /// <param name="flushInterval">Time interval to flush the buffer.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public S3SinkBulkOperator(
            string bucketName,
            string folderPath,
            IAmazonS3 s3Client,
            ISerializer<TInput>? serializer = null,
            int batchSize = 100,
            TimeSpan? flushInterval = null,
            ILogger<S3SinkBulkOperator<TInput>>? logger = null)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _folderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));

            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<S3SinkBulkOperator<TInput>>.Instance;

            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _transferUtility = new TransferUtility(_s3Client);

            _batchSize = batchSize;
            _flushInterval = flushInterval ?? TimeSpan.FromSeconds(10);
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
            if (_disposed) throw new ObjectDisposedException(nameof(S3SinkBulkOperator<TInput>));
            if (_isRunning) return;

            _timer = new Timer(_ => FlushBuffer(), null, _flushInterval, _flushInterval);
            _isRunning = true;
            _logger.LogInformation("S3SinkBulkOperator started for bucket {BucketName}", _bucketName);
        }

        /// <summary>
        /// Processes the input object by buffering it for batch upload to AWS S3.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(S3SinkBulkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("S3SinkBulkOperator is not running. Call Start() before processing messages");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("S3SinkBulkOperator received null input. Skipping");
                return;
            }

            List<TInput> batchToUpload = null;

            lock (_bufferLock)
            {
                _buffer.Add(input);
                if (_buffer.Count >= _batchSize)
                {
                    batchToUpload = new List<TInput>(_buffer);
                    _buffer.Clear();
                }
            }

            if (batchToUpload != null)
            {
                // Use core error handling for batch upload
                ErrorHandlingHelper.TryExecute(
                    _executionOptions,
                    OperatorName,
                    batchToUpload,
                    (Action<List<TInput>>)UploadBatchToS3);
            }
        }

        private void FlushBuffer()
        {
            List<TInput> batchToUpload = null;

            lock (_bufferLock)
            {
                if (_buffer.Count > 0)
                {
                    batchToUpload = new List<TInput>(_buffer);
                    _buffer.Clear();
                }
            }

            if (batchToUpload != null)
            {
                try
                {
                    ErrorHandlingHelper.TryExecute(
                        _executionOptions,
                        OperatorName,
                        batchToUpload,
                        (Action<List<TInput>>)UploadBatchToS3);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled flush to S3 bucket {BucketName}", _bucketName);
                }
            }
        }

        private void UploadBatchToS3(List<TInput> batch)
        {
            var serializedBatch = string.Join(Environment.NewLine, batch.Select(obj => _serializer.Serialize(obj)));
            var fileName = $"{Guid.NewGuid()}.jsonl";
            var key = $"{_folderPath}/{fileName}";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serializedBatch));
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = key,
                BucketName = _bucketName,
                ContentType = "application/jsonl"
            };

            _transferUtility.Upload(uploadRequest);
            _logger.LogDebug("Uploaded batch of {Count} items to S3 bucket {BucketName} at key {Key}", batch.Count, _bucketName, key);
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            _isRunning = false;
            
            // Flush remaining items
            FlushBuffer();
            
            _logger.LogInformation("S3SinkBulkOperator stopped for bucket {BucketName}", _bucketName);
        }

        /// <summary>
        /// Disposes the AWS S3 client and transfer utility.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Dispose();
            _transferUtility?.Dispose();
            _s3Client?.Dispose();
        }
    }
}
