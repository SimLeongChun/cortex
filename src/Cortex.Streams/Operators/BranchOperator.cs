using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    public class BranchOperator<T> : IOperator, IHasNextOperators, ITelemetryEnabled
    {
        private readonly string _branchName;
        private readonly IOperator _branchOperator;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        public BranchOperator(string branchName, IOperator branchOperator)
        {
            _branchName = branchName ?? throw new ArgumentNullException(nameof(branchName));
            _branchOperator = branchOperator ?? throw new ArgumentNullException(nameof(branchOperator));
        }

        public string BranchName => _branchName;

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"branch_operator_processed_{_branchName}_{typeof(T).Name}", "Number of items processed by BranchOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"branch_operator_processing_time_{_branchName}_{typeof(T).Name}", "Processing time for BranchOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"BranchOperator_{_branchName}_{typeof(T).Name}");

                // Cache delegates
                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry to the branch operator
            if (_branchOperator is ITelemetryEnabled telemetryEnabled)
            {
                telemetryEnabled.SetTelemetryProvider(telemetryProvider);
            }
        }

        public void Process(object input)
        {
            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan($"BranchOperator.Process.{_branchName}"))
                {
                    try
                    {
                        span.SetAttribute("branch_name", _branchName);
                        _branchOperator.Process(input);
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
                _branchOperator.Process(input);
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            throw new InvalidOperationException("Cannot set next operator on a BranchOperator.");
        }

        public IEnumerable<IOperator> GetNextOperators()
        {
            if (_branchOperator != null)
                yield return _branchOperator;
        }
    }
}
