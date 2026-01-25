using Cortex.Mediator.Streaming;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Pipeline behavior for logging streaming query execution.
    /// Logs when the stream starts, each item yielded, and when the stream completes.
    /// </summary>
    /// <typeparam name="TQuery">The type of streaming query being handled.</typeparam>
    /// <typeparam name="TResult">The type of each item in the result stream.</typeparam>
    public sealed class LoggingStreamQueryBehavior<TQuery, TResult> : IStreamQueryPipelineBehavior<TQuery, TResult>
        where TQuery : IStreamQuery<TResult>
    {
        private readonly ILogger<LoggingStreamQueryBehavior<TQuery, TResult>> _logger;

        public LoggingStreamQueryBehavior(ILogger<LoggingStreamQueryBehavior<TQuery, TResult>> logger)
        {
            _logger = logger;
        }

        public IAsyncEnumerable<TResult> Handle(
            TQuery query,
            StreamQueryHandlerDelegate<TResult> next,
            CancellationToken cancellationToken)
        {
            return ExecuteWithLogging(query, next, cancellationToken);
        }

        private async IAsyncEnumerable<TResult> ExecuteWithLogging(
            TQuery query,
            StreamQueryHandlerDelegate<TResult> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var queryName = typeof(TQuery).Name;
            _logger.LogInformation("Starting stream for query {QueryName}", queryName);

            var stopwatch = Stopwatch.StartNew();
            var itemCount = 0;

            IAsyncEnumerable<TResult> stream;
            try
            {
                stream = next();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "Error creating stream for query {QueryName} after {ElapsedMilliseconds} ms",
                    queryName,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }

            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                itemCount++;
                _logger.LogDebug(
                    "Stream {QueryName} yielded item {ItemNumber}",
                    queryName,
                    itemCount);

                yield return item;
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Stream {QueryName} completed successfully. Yielded {ItemCount} items in {ElapsedMilliseconds} ms",
                queryName,
                itemCount,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
