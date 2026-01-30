using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Cortex.Streams.AzureBlobStorage.Serializers;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Text;

namespace Cortex.Streams.AzureBlobStorage
{
    /// <summary>
    /// Azure Blob Storage Sink Operator that writes each serialized data object as a separate blob file.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class AzureBlobStorageSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"AzureBlobStorageSinkOperator<{typeof(TInput).Name}>";

        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _directoryPath;
        private readonly ISerializer<TInput> _serializer;
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<AzureBlobStorageSinkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobStorageSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="connectionString">Azure Blob Storage connection string.</param>
        /// <param name="containerName">Name of the Blob container.</param>
        /// <param name="directoryPath">Path within the container to store data (e.g., "data/ingest").</param>
        /// <param name="serializer">Serializer to convert TInput objects to strings.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public AzureBlobStorageSinkOperator(
            string connectionString,
            string containerName,
            string directoryPath,
            ISerializer<TInput>? serializer = null,
            ILogger<AzureBlobStorageSinkOperator<TInput>>? logger = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<AzureBlobStorageSinkOperator<TInput>>.Instance;

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
            if (_disposed) throw new ObjectDisposedException(nameof(AzureBlobStorageSinkOperator<TInput>));
            if (_isRunning) return;

            _containerClient.CreateIfNotExists(PublicAccessType.None);
            _isRunning = true;
            _logger.LogInformation("AzureBlobStorageSinkOperator started for container '{ContainerName}', directory '{DirectoryPath}'", _containerName, _directoryPath);
        }

        /// <summary>
        /// Processes the input object by serializing it and sending it as a separate blob file.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AzureBlobStorageSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("AzureBlobStorageSinkOperator is not running. Call Start() before processing messages");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("AzureBlobStorageSinkOperator received null input. Skipping");
                return;
            }

            // Use core error handling for message processing
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)UploadToBlob);
        }

        private void UploadToBlob(TInput input)
        {
            var serializedMessage = _serializer.Serialize(input);
            var blobName = $"{_directoryPath}/{Guid.NewGuid()}.json";
            var blobClient = _containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));
            blobClient.Upload(stream, new BlobHttpHeaders { ContentType = "application/json" });
            _logger.LogDebug("Message uploaded to Azure Blob Storage: {BlobName}", blobName);
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            _isRunning = false;
            _logger.LogInformation("AzureBlobStorageSinkOperator stopped for container '{ContainerName}'", _containerName);
        }

        /// <summary>
        /// Disposes the resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
