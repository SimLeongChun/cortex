using Cortex.States;
using Cortex.Streams.Operators;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams
{
    public interface IStream<TIn, TCurrent>
    {
        /// <summary>
        /// Start the stream processing.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the stream processing.
        /// </summary>
        void Stop();

        /// <summary>
        /// Processes the specified input value and emits it to the underlying stream.
        /// </summary>
        /// <param name="value">The input value to be emitted. The meaning and requirements of this value depend on the implementation.</param>
        void Emit(TIn value);

        // feature #102: Support async emit with cancellation token

        /// <summary>
        /// Asynchronously emits the specified value to the underlying stream.
        /// </summary>
        /// <param name="value">The value to emit. The meaning and requirements of this value depend on the implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the emit operation.</param>
        /// <returns>A task that represents the asynchronous emit operation.</returns>
        Task EmitAsync(TIn value, CancellationToken cancellationToken = default);

        StreamStatuses GetStatus();

        IReadOnlyDictionary<string, BranchOperator<TCurrent>> GetBranches();

        TStateStore GetStateStoreByName<TStateStore>(string name) where TStateStore : IDataStore;
        IEnumerable<TStateStore> GetStateStoresByType<TStateStore>() where TStateStore : IDataStore;
    }
}
