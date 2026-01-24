using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    internal class ForkOperator<T> : IOperator, IHasNextOperators, ITelemetryEnabled
    {
        private readonly Dictionary<string, BranchOperator<T>> _branches = new Dictionary<string, BranchOperator<T>>();

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"fork_operator_processed_{typeof(T).Name}", "Number of items processed by ForkOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"fork_operator_processing_time_{typeof(T).Name}", "Processing time for ForkOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"ForkOperator_{typeof(T).Name}");

                // Cache delegates
                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry to all branches
            foreach (var branch in _branches.Values)
            {
                if (branch is ITelemetryEnabled telemetryEnabled)
                {
                    telemetryEnabled.SetTelemetryProvider(telemetryProvider);
                }
            }
        }

        public void AddBranch(string name, BranchOperator<T> branchOperator)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Branch name cannot be null or empty.", nameof(name));
            if (branchOperator == null)
                throw new ArgumentNullException(nameof(branchOperator));

            _branches[name] = branchOperator;

            // Propagate telemetry to the new branch if already configured
            if (_telemetryProvider != null && branchOperator is ITelemetryEnabled telemetryEnabled)
            {
                telemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }
        }

        public void Process(object input)
        {
            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("ForkOperator.Process"))
                {
                    try
                    {
                        span.SetAttribute("branch_count", _branches.Count.ToString());
                        foreach (var branch in _branches.Values)
                        {
                            branch.Process(input);
                        }
                        span.SetAttribute("status", "success");
                    }
                    catch (Exception ex)
                    {
                        span.SetAttribute("status", "error");
                        span.SetAttribute("exception", ex.ToString());
                        throw;
                    }
                    finally
                    {
                        stopwatch.Stop();
                        _recordProcessingTime(stopwatch.Elapsed.TotalMilliseconds);
                        _incrementProcessedCounter();
                    }
                }
            }
            else
            {
                foreach (var branch in _branches.Values)
                {
                    branch.Process(input);
                }
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            throw new InvalidOperationException("Cannot set next operator on a ForkOperator.");
        }

        public IEnumerable<IOperator> GetNextOperators()
        {
            return _branches.Values;
        }

        public IReadOnlyDictionary<string, BranchOperator<T>> Branches => _branches;
    }
}
