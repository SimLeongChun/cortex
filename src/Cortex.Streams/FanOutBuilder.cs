using Cortex.Streams.Abstractions;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Streams.Performance;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;

namespace Cortex.Streams
{
    /// <summary>
    /// Builder for creating fan-out patterns with multiple sinks.
    /// This class manages the configuration of multiple sink destinations
    /// that receive the same data stream in parallel.
    /// </summary>
    /// <typeparam name="TIn">The type of the initial input to the stream.</typeparam>
    /// <typeparam name="TCurrent">The current type of data being processed.</typeparam>
    internal class FanOutBuilder<TIn, TCurrent> : IFanOutBuilder<TIn, TCurrent>
    {
        private readonly string _streamName;
        private readonly IOperator _firstOperator;
        private readonly IOperator _lastOperatorBeforeFanOut;
        private readonly ForkOperator<TCurrent> _forkOperator;
        private readonly List<BranchOperator<TCurrent>> _branchOperators;
        private readonly HashSet<string> _sinkNames;
        private readonly ITelemetryProvider _telemetryProvider;
        private readonly StreamExecutionOptions _executionOptions;
        private readonly StreamPerformanceOptions _performanceOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FanOutBuilder{TIn, TCurrent}"/> class.
        /// </summary>
        /// <param name="streamName">The name of the stream.</param>
        /// <param name="firstOperator">The first operator in the pipeline.</param>
        /// <param name="lastOperatorBeforeFanOut">The last operator before the fan-out point.</param>
        /// <param name="telemetryProvider">Optional telemetry provider for metrics and tracing.</param>
        /// <param name="executionOptions">Stream execution options for error handling.</param>
        /// <param name="performanceOptions">Performance tuning options.</param>
        internal FanOutBuilder(
            string streamName,
            IOperator firstOperator,
            IOperator lastOperatorBeforeFanOut,
            ITelemetryProvider telemetryProvider,
            StreamExecutionOptions executionOptions,
            StreamPerformanceOptions performanceOptions)
        {
            _streamName = streamName ?? throw new ArgumentNullException(nameof(streamName));
            _telemetryProvider = telemetryProvider;
            _executionOptions = executionOptions ?? StreamExecutionOptions.Default;
            _performanceOptions = performanceOptions ?? StreamPerformanceOptions.Default;

            _forkOperator = new ForkOperator<TCurrent>();
            _branchOperators = new List<BranchOperator<TCurrent>>();
            _sinkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Store original first operator or use fork as first
            if (firstOperator == null)
            {
                _firstOperator = _forkOperator;
                _lastOperatorBeforeFanOut = null;
            }
            else
            {
                _firstOperator = firstOperator;
                _lastOperatorBeforeFanOut = lastOperatorBeforeFanOut;

                // Connect the fork operator to the pipeline
                if (_lastOperatorBeforeFanOut != null)
                {
                    _lastOperatorBeforeFanOut.SetNext(_forkOperator);
                }
                else
                {
                    // First operator is also the last, set fork as next
                    _firstOperator.SetNext(_forkOperator);
                }
            }
        }

        /// <inheritdoc />
        public IFanOutBuilder<TIn, TCurrent> To(string name, Action<TCurrent> sinkFunction)
        {
            ValidateSinkName(name);
            if (sinkFunction == null)
                throw new ArgumentNullException(nameof(sinkFunction));

            var sinkOperator = new SinkOperator<TCurrent>(sinkFunction);
            AddSinkBranch(name, sinkOperator);

            return this;
        }

        /// <inheritdoc />
        public IFanOutBuilder<TIn, TCurrent> To(string name, Func<TCurrent, bool> predicate, Action<TCurrent> sinkFunction)
        {
            ValidateSinkName(name);
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (sinkFunction == null)
                throw new ArgumentNullException(nameof(sinkFunction));

            // Create a mini-pipeline: Filter -> Sink
            var filterOperator = new FilterOperator<TCurrent>(predicate);
            var sinkOperator = new SinkOperator<TCurrent>(sinkFunction);
            filterOperator.SetNext(sinkOperator);

            AddSinkBranch(name, filterOperator);

            return this;
        }

        /// <inheritdoc />
        public IFanOutBuilder<TIn, TCurrent> To(string name, ISinkOperator<TCurrent> sinkOperator)
        {
            ValidateSinkName(name);
            if (sinkOperator == null)
                throw new ArgumentNullException(nameof(sinkOperator));

            var sinkAdapter = new SinkOperatorAdapter<TCurrent>(sinkOperator);
            AddSinkBranch(name, sinkAdapter);

            return this;
        }

        /// <inheritdoc />
        public IFanOutBuilder<TIn, TCurrent> To(string name, Func<TCurrent, bool> predicate, ISinkOperator<TCurrent> sinkOperator)
        {
            ValidateSinkName(name);
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (sinkOperator == null)
                throw new ArgumentNullException(nameof(sinkOperator));

            // Create a mini-pipeline: Filter -> SinkAdapter
            var filterOperator = new FilterOperator<TCurrent>(predicate);
            var sinkAdapter = new SinkOperatorAdapter<TCurrent>(sinkOperator);
            filterOperator.SetNext(sinkAdapter);

            AddSinkBranch(name, filterOperator);

            return this;
        }

        /// <inheritdoc />
        public IFanOutBuilder<TIn, TCurrent> ToWithTransform<TOutput>(string name, Func<TCurrent, TOutput> mapFunction, Action<TOutput> sinkFunction)
        {
            ValidateSinkName(name);
            if (mapFunction == null)
                throw new ArgumentNullException(nameof(mapFunction));
            if (sinkFunction == null)
                throw new ArgumentNullException(nameof(sinkFunction));

            // Create a mini-pipeline: Map -> Sink
            var mapOperator = new MapOperator<TCurrent, TOutput>(mapFunction);
            var sinkOperator = new SinkOperator<TOutput>(sinkFunction);
            mapOperator.SetNext(sinkOperator);

            // We need to create a branch that starts with the map operator
            // Since BranchOperator<TCurrent> expects input of TCurrent, the map operator handles the conversion
            var branchOperator = new BranchOperator<TCurrent>(name, mapOperator);

            _forkOperator.AddBranch(name, branchOperator);
            _branchOperators.Add(branchOperator);

            return this;
        }

        /// <inheritdoc />
        public IStream<TIn> Build()
        {
            if (_branchOperators.Count == 0)
            {
                throw new InvalidOperationException(
                    "FanOut must have at least one sink configured. " +
                    "Use .To() to add sinks before calling .Build().");
            }

            return new Stream<TIn, TCurrent>(
                _streamName,
                _firstOperator,
                _branchOperators,
                _telemetryProvider,
                _executionOptions,
                _performanceOptions);
        }

        /// <summary>
        /// Validates that the sink name is valid and unique.
        /// </summary>
        private void ValidateSinkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sink name cannot be null, empty, or whitespace.", nameof(name));

            if (!_sinkNames.Add(name))
                throw new ArgumentException($"A sink with the name '{name}' has already been added. Sink names must be unique.", nameof(name));
        }

        /// <summary>
        /// Adds a branch with the given first operator to the fork.
        /// </summary>
        private void AddSinkBranch(string name, IOperator firstOperator)
        {
            var branchOperator = new BranchOperator<TCurrent>(name, firstOperator);

            _forkOperator.AddBranch(name, branchOperator);
            _branchOperators.Add(branchOperator);
        }
    }
}
