using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.Abstractions;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Streams.Performance;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams
{
    /// <summary>
    /// Represents a built stream that can be started and stopped.
    /// </summary>
    /// <typeparam name="TIn">The type of the initial input to the stream.</typeparam>
    /// <typeparam name="TCurrent">The current type of data in the stream (internal use only).</typeparam>
    public class Stream<TIn, TCurrent> : IStream<TIn>, IStatefulOperator, IDisposable
    {
        private readonly string _name;
        private readonly IOperator _operatorChain;
        private readonly List<BranchOperator<TCurrent>> _branchOperators;
        private bool _isStarted;
        private bool _isDisposed;

        private readonly ITelemetryProvider _telemetryProvider;
        private readonly StreamExecutionOptions _executionOptions;
        private readonly StreamPerformanceOptions _performanceOptions;

        // Buffered processor for async processing
        private BufferedProcessor<TIn> _bufferedProcessor;
        private readonly object _processorLock = new object();


        internal Stream(
            string name,
            IOperator operatorChain,
            List<BranchOperator<TCurrent>> branchOperators,
            ITelemetryProvider telemetryProvider,
            StreamExecutionOptions executionOptions,
            StreamPerformanceOptions performanceOptions = null)
        {
            _name = name;
            _operatorChain = operatorChain;
            _branchOperators = branchOperators;
            _telemetryProvider = telemetryProvider;
            _executionOptions = executionOptions;
            _performanceOptions = performanceOptions ?? StreamPerformanceOptions.Default;

            // Initialize telemetry in operators
            InitializeTelemetry(_operatorChain);
            InitializeErrorHandling(_operatorChain);
        }

        private void InitializeTelemetry(IOperator op)
        {
            if (op == null)
                return;

            if (op is ITelemetryEnabled telemetryEnabled)
            {
                telemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            if (op is IHasNextOperators hasNextOperators)
            {
                foreach (var nextOp in hasNextOperators.GetNextOperators())
                {
                    InitializeTelemetry(nextOp);
                }
            }
            else
            {
                var field = op.GetType().GetField("_nextOperator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var nextOp = field.GetValue(op) as IOperator;
                    InitializeTelemetry(nextOp);
                }
            }
        }

        private void InitializeErrorHandling(IOperator op)
        {
            if (op == null)
                return;

            if (op is IErrorHandlingEnabled errorHandlingEnabled)
            {
                errorHandlingEnabled.SetErrorHandling(_executionOptions);
            }

            if (op is IHasNextOperators hasNextOperators)
            {
                foreach (var nextOp in hasNextOperators.GetNextOperators())
                {
                    InitializeErrorHandling(nextOp);
                }
            }
            else
            {
                var field = op.GetType().GetField("_nextOperator",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var nextOp = field.GetValue(op) as IOperator;
                    InitializeErrorHandling(nextOp);
                }
            }
        }

        private void EnsureBufferedProcessorInitialized()
        {
            if (_bufferedProcessor != null || !_performanceOptions.EnableBufferedProcessing)
                return;

            lock (_processorLock)
            {
                if (_bufferedProcessor != null)
                    return;

                _bufferedProcessor = new BufferedProcessor<TIn>(
                    _performanceOptions,
                    ProcessItem,
                    () => _isStarted = false);
            }
        }

        private void ProcessItem(TIn value)
        {
            try
            {
                _operatorChain.Process(value);
            }
            catch (StreamStoppedException)
            {
                Stop();
            }
        }


        /// <summary>
        /// Starts the stream processing.
        /// </summary>
        public void Start()
        {
            _isStarted = true;

            // Initialize buffered processor if enabled
            if (_performanceOptions.EnableBufferedProcessing)
            {
                EnsureBufferedProcessorInitialized();
            }
        }

        /// <summary>
        /// Stops the stream processing.
        /// </summary>
        public void Stop()
        {
            _isStarted = false;

            if (_operatorChain is SourceOperatorAdapter<TCurrent> sourceAdapter)
            {
                sourceAdapter.Stop();
            }

            // Stop the buffered processor if it exists
            _bufferedProcessor?.Stop();
        }

        /// <summary>
        /// Stops the stream processing asynchronously, waiting for any buffered items to be processed.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the graceful shutdown.</param>
        /// <returns>A task that represents the asynchronous stop operation.</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _isStarted = false;

            if (_operatorChain is SourceOperatorAdapter<TCurrent> sourceAdapter)
            {
                sourceAdapter.Stop();
            }

            // Wait for buffered items to be processed
            if (_bufferedProcessor != null)
            {
                await _bufferedProcessor.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the current status of the stream.
        /// </summary>
        /// <returns>A string indicating whether the stream is running or stopped.</returns>
        public StreamStatuses GetStatus()
        {
            return _isStarted ? StreamStatuses.RUNNING : StreamStatuses.NOT_RUNNING;
        }

        /// <summary>
        /// Emits data into the stream when no source operator is used.
        /// This method blocks until the entire pipeline has finished processing the item.
        /// </summary>
        /// <param name="value">The data to emit.</param>
        public void Emit(TIn value)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Stream has not been started.");

            if (_operatorChain is SourceOperatorAdapter<TIn>)
                throw new InvalidOperationException("Cannot manually emit data to a stream with a source operator.");

            try
            {
                _operatorChain.Process(value);
            }
            catch (StreamStoppedException)
            {
                // Global error strategy requested a graceful stop
                Stop();
                // Swallow for graceful shutdown
            }
        }

        /// <summary>
        /// Asynchronously emits data into the stream when no source operator is used.
        /// When buffered processing is enabled, this method adds the item to an internal buffer 
        /// and returns quickly (subject to backpressure settings).
        /// When buffered processing is disabled (default), this runs the pipeline asynchronously using Task.Run.
        /// </summary>
        /// <param name="value">The value to emit.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the emit operation.</param>
        /// <returns>A task that represents the asynchronous emit operation.</returns>
        public async Task EmitAsync(TIn value, CancellationToken cancellationToken = default)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Stream has not been started.");

            if (_operatorChain is SourceOperatorAdapter<TIn>)
                throw new InvalidOperationException("Cannot manually emit data to a stream with a source operator.");

            cancellationToken.ThrowIfCancellationRequested();

            if (_performanceOptions.EnableBufferedProcessing)
            {
                EnsureBufferedProcessorInitialized();
                await _bufferedProcessor.TryEnqueueAsync(value, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Backward compatible behavior: run synchronously on thread pool
                await Task.Run(() =>
                {
                    try
                    {
                        _operatorChain.Process(value);
                    }
                    catch (StreamStoppedException)
                    {
                        Stop();
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Emits multiple values to the stream asynchronously.
        /// When buffered processing is enabled, items are added to the buffer in bulk for better throughput.
        /// </summary>
        /// <param name="values">The values to emit.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the emit operation.</param>
        /// <returns>A task that represents the asynchronous batch emit operation.</returns>
        public async Task EmitBatchAsync(IEnumerable<TIn> values, CancellationToken cancellationToken = default)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Stream has not been started.");

            if (_operatorChain is SourceOperatorAdapter<TIn>)
                throw new InvalidOperationException("Cannot manually emit data to a stream with a source operator.");

            if (values == null)
                throw new ArgumentNullException(nameof(values));

            cancellationToken.ThrowIfCancellationRequested();

            if (_performanceOptions.EnableBufferedProcessing)
            {
                EnsureBufferedProcessorInitialized();
                foreach (var value in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _bufferedProcessor.TryEnqueueAsync(value, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                // Process each item asynchronously
                foreach (var value in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Run(() =>
                    {
                        try
                        {
                            _operatorChain.Process(value);
                        }
                        catch (StreamStoppedException)
                        {
                            Stop();
                        }
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Emits a value to the stream without waiting for processing to complete (fire-and-forget).
        /// Requires buffered processing to be enabled via <see cref="StreamPerformanceOptions"/>.
        /// If the buffer is full, behavior is determined by the <see cref="BackpressureStrategy"/>.
        /// </summary>
        /// <param name="value">The value to emit.</param>
        /// <returns>True if the value was accepted into the buffer; false if it was dropped.</returns>
        /// <exception cref="InvalidOperationException">Thrown when buffered processing is not enabled or stream is not started.</exception>
        /// <exception cref="BufferFullException">Thrown when the buffer is full and strategy is ThrowException.</exception>
        public bool EmitAndForget(TIn value)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Stream has not been started.");

            if (_operatorChain is SourceOperatorAdapter<TIn>)
                throw new InvalidOperationException("Cannot manually emit data to a stream with a source operator.");

            if (!_performanceOptions.EnableBufferedProcessing)
                throw new InvalidOperationException(
                    "EmitAndForget requires buffered processing to be enabled. " +
                    "Configure the stream with WithPerformanceOptions() and set EnableBufferedProcessing = true.");

            EnsureBufferedProcessorInitialized();
            return _bufferedProcessor.TryEnqueue(value);
        }

        /// <summary>
        /// Gets the current buffer statistics when buffered processing is enabled.
        /// Returns null if buffered processing is not enabled.
        /// </summary>
        public BufferStatistics GetBufferStatistics()
        {
            if (!_performanceOptions.EnableBufferedProcessing || _bufferedProcessor == null)
                return null;

            return new BufferStatistics
            {
                CurrentCount = _bufferedProcessor.CurrentBufferCount,
                Capacity = _performanceOptions.BufferCapacity,
                TotalEnqueued = _bufferedProcessor.ItemsEnqueued,
                TotalProcessed = _bufferedProcessor.ItemsProcessed,
                TotalDropped = _bufferedProcessor.ItemsDropped
            };
        }

        public IReadOnlyDictionary<string, IBranchInfo> GetBranches()
        {
            var branchDict = new Dictionary<string, IBranchInfo>();
            foreach (var branchOperator in _branchOperators)
            {
                branchDict[branchOperator.BranchName] = branchOperator;
            }
            return branchDict;
        }

        public IEnumerable<IDataStore> GetStateStores()
        {
            var visitedOperators = new HashSet<IOperator>();
            var stateStores = new List<IDataStore>();
            CollectStateStores(_operatorChain, stateStores, visitedOperators);
            return stateStores;
        }

        private void CollectStateStores(IOperator op, List<IDataStore> stateStores, HashSet<IOperator> visitedOperators)
        {
            if (op == null || visitedOperators.Contains(op))
                return;

            visitedOperators.Add(op);

            if (op is IStatefulOperator statefulOperator)
            {
                stateStores.AddRange(statefulOperator.GetStateStores());
            }

            if (op is IHasNextOperators hasNextOperators)
            {
                foreach (var nextOp in hasNextOperators.GetNextOperators())
                {
                    CollectStateStores(nextOp, stateStores, visitedOperators);
                }
            }
            else if (op is IOperator nextOperator)
            {
                var field = op.GetType().GetField("_nextOperator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var nextOp = field.GetValue(op) as IOperator;
                    CollectStateStores(nextOp, stateStores, visitedOperators);
                }
            }
        }

        public TStateStore GetStateStoreByName<TStateStore>(string name) where TStateStore : IDataStore
        {
            return GetStateStores()
                .OfType<TStateStore>()
                .FirstOrDefault(store => store.Name == name);
        }

        public IEnumerable<TStateStore> GetStateStoresByType<TStateStore>() where TStateStore : IDataStore
        {
            return GetStateStores()
                .OfType<TStateStore>();
        }

        /// <summary>
        /// Disposes the stream and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Stop();
            _bufferedProcessor?.Dispose();
        }
    }
}
