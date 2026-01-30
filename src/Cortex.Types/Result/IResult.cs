namespace Cortex.Types
{
    /// <summary>
    /// Base interface for all Result types providing common functionality
    /// </summary>
    public interface IResult
    {
        /// <summary>
        /// Gets whether the result represents a successful operation
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets whether the result represents a failed operation
        /// </summary>
        bool IsFailure { get; }
    }

    /// <summary>
    /// Interface for Result types that carry a value
    /// </summary>
    /// <typeparam name="TValue">Type of the success value</typeparam>
    public interface IResult<out TValue> : IResult
    {
        /// <summary>
        /// Gets the success value. Throws if the result is a failure.
        /// </summary>
        TValue Value { get; }
    }

    /// <summary>
    /// Interface for Result types that carry both value and error
    /// </summary>
    /// <typeparam name="TValue">Type of the success value</typeparam>
    /// <typeparam name="TError">Type of the error value</typeparam>
    public interface IResult<out TValue, out TError> : IResult<TValue>
    {
        /// <summary>
        /// Gets the error value. Throws if the result is a success.
        /// </summary>
        TError Error { get; }
    }
}
