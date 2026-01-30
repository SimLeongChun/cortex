using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Cortex.Streams.AzureBlobStorage.Serializers;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cortex.Streams.AzureBlobStorage
{
    /// <summary>
    /// Azure Blob Storage Bulk Sink Operator that batches and writes serialized data to a blob container.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class AzureBlobStorageBulkSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"AzureBlobStorageBulkSinkOperator<{typeof(TInput).Name}>";

        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _directoryPath;
        private readonly ISerializer<TInput> _serializer;
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<AzureBlobStorageBulkSinkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _isRunning;
        private bool _disposed;

        // For batching
        private readonly List<TInput> _buffer = new List<TInput>();
        private readonly int _batchSize;
        private readonly TimeSpan _flushInterval;
        private Timer _timer;
        private readonly object _bufferLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobStorageBulkSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="connectionString">Azure Blob Storage connection string.</param>
        /// <param name="containerName">Name of the Blob container.</param>
        /// <param name="directoryPath">Path within the container to store data (e.g., "data/ingest").</param>
        /// <param name="serializer">Serializer to convert TInput objects to strings.</param>
        /// <param name="batchSize">Number of messages to batch before uploading.</param>
        /// <param name="flushInterval">Time interval to flush the buffer regardless of batch size.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public AzureBlobStorageBulkSinkOperator(
            string connectionString,
            string containerName,
            string directoryPath,
            ISerializer<TInput> serializer = null,
            int batchSize = 100,
            TimeSpan? flushInterval = null,
            ILogger<AzureBlobStorageBulkSinkOperator<TInput>>? logger = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _batchSize = batchSize;
            _flushInterval = flushInterval ?? TimeSpan.FromSeconds(10);
            _logger = logger ?? NullLogger<AzureBlobStorageBulkSinkOperator<TInput>>.Instance;

            _containerClient = new BlobContainerClient(_connectionString, _containerName);
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Starts the sink operator by ensuring the Blob container exists.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AzureBlobStorageBulkSinkOperator<TInput>));
            if (_isRunning) return;

            _containerClient.CreateIfNotExists();
            _timer = new Timer(_ => FlushBuffer(), null, _flushInterval, _flushInterval);
            _isRunning = true;
            _logger.LogInformation("AzureBlobStorageBulkSinkOperator started for container '{ContainerName}'", _containerName);
        }

        /// <summary>
        /// Processes the input object by adding it to the buffer.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AzureBlobStorageBulkSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("AzureBlobStorageBulkSinkOperator is not running. Call Start() before processing messages");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("AzureBlobStorageBulkSinkOperator received null input. Skipping");
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
                ErrorHandlingHelper.TryExecute(
                    _executionOptions,
                    OperatorName,
                    batchToUpload,
                    (Action<List<TInput>>)UploadBatchToBlob);
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
                        (Action<List<TInput>>)UploadBatchToBlob);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled flush to Azure Blob Storage container '{ContainerName}'", _containerName);
                }
            }
        }

        private void UploadBatchToBlob(List<TInput> batch)
        {
            var serializedBatch = string.Join(Environment.NewLine, batch.Select(obj => _serializer.Serialize(obj)));
            var fileName = $"{Guid.NewGuid()}.jsonl";
            var blobName = $"{_directoryPath}/{fileName}";
            var blobClient = _containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serializedBatch));
            blobClient.Upload(stream, new BlobHttpHeaders { ContentType = "application/jsonl" });
            _logger.LogDebug("Uploaded batch of {Count} items to Azure Blob Storage: {BlobName}", batch.Count, blobName);
        }

        /// <summary>
        /// Stops the sink operator by flushing the buffer.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            _isRunning = false;
            FlushBuffer();
            _logger.LogInformation("AzureBlobStorageBulkSinkOperator stopped for container '{ContainerName}'", _containerName);
        }

        /// <summary>
        /// Disposes the resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Dispose();
        }
    }
}
