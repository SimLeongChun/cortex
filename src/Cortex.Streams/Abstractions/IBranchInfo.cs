namespace Cortex.Streams.Abstractions
{
    /// <summary>
    /// Provides information about a branch in the stream processing pipeline.
    /// This is a non-generic interface that allows accessing branch metadata
    /// without exposing internal type parameters.
    /// </summary>
    public interface IBranchInfo
    {
        /// <summary>
        /// Gets the name of the branch.
        /// </summary>
        string BranchName { get; }
    }
}
