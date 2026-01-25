using Cortex.Mediator.Streaming;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Cortex.Streams.Mediator.Handlers
{
    /// <summary>
    /// A streaming query handler that provides stream data through the Mediator's streaming query interface.
    /// This enables consuming Cortex Stream data through Mediator's IAsyncEnumerable pattern.
    /// </summary>
    /// <typeparam name="TQuery">The type of streaming query to handle.</typeparam>
    /// <typeparam name="TResult">The type of items in the result stream.</typeparam>
    public abstract class StreamBackedStreamQueryHandler<TQuery, TResult> : IStreamQueryHandler<TQuery, TResult>
        where TQuery : IStreamQuery<TResult>
    {
        private readonly IStream<TResult, TResult> _stream;
        private readonly int _channelCapacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamBackedStreamQueryHandler{TQuery, TResult}"/> class.
        /// </summary>
        /// <param name="stream">The Cortex Stream to read data from.</param>
        /// <param name="channelCapacity">The capacity of the internal channel buffer. Default is 100.</param>
        protected StreamBackedStreamQueryHandler(IStream<TResult, TResult> stream, int channelCapacity = 100)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _channelCapacity = channelCapacity;
        }

        /// <summary>
        /// Handles the streaming query by yielding items from the stream.
        /// </summary>
        /// <param name="query">The streaming query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async enumerable of stream items.</returns>
        public async IAsyncEnumerable<TResult> Handle(TQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Create a channel to bridge the stream's push model with IAsyncEnumerable's pull model
            var channel = Channel.CreateBounded<TResult>(new BoundedChannelOptions(_channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            // Apply any query-specific filtering
            var filter = GetQueryFilter(query);

            // This would typically be set up by a custom stream branch or sink
            // For now, this provides the pattern - actual implementation would depend on stream setup

            try
            {
                await foreach (var item in ReadFromChannel(channel.Reader, cancellationToken))
                {
                    if (filter == null || filter(item))
                    {
                        yield return item;
                    }
                }
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Override this method to provide query-specific filtering.
        /// </summary>
        /// <param name="query">The streaming query containing filter criteria.</param>
        /// <returns>A filter function, or null for no filtering.</returns>
        protected virtual Func<TResult, bool> GetQueryFilter(TQuery query)
        {
            return null;
        }

        private async IAsyncEnumerable<TResult> ReadFromChannel(
            ChannelReader<TResult> reader,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
    }
}
