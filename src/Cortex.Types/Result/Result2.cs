using System;
using System.Diagnostics.CodeAnalysis;

namespace Cortex.Types
{
    /// <summary>
    /// Represents the result of an operation that can succeed with a value or fail with a custom error type
    /// </summary>
    /// <typeparam name="TValue">Type of the success value</typeparam>
    /// <typeparam name="TError">Type of the error value</typeparam>
    public readonly struct Result<TValue, TError> : IEquatable<Result<TValue, TError>>, IResult<TValue, TError>
    {
        private readonly TValue _value;
        private readonly TError _error;
        private readonly bool _isSuccess;

        /// <inheritdoc />
        public bool IsSuccess => _isSuccess;

        /// <inheritdoc />
        public bool IsFailure => !_isSuccess;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result</exception>
        public TValue Value => _isSuccess
            ? _value
            : throw new InvalidOperationException(
                $"Cannot access Value on a failed Result. Error: {_error}");

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result</exception>
        public TError Error => !_isSuccess
            ? _error
            : throw new InvalidOperationException(
                "Cannot access Error on a successful Result");

        private Result(TValue value, TError error, bool isSuccess)
        {
            _value = value;
            _error = error;
            _isSuccess = isSuccess;
        }

        /// <summary>
        /// Creates a successful result with the specified value
        /// </summary>
        /// <param name="value">The success value</param>
        /// <returns>A successful Result</returns>
        public static Result<TValue, TError> Success(TValue value) =>
            new(value, default, true);

        /// <summary>
        /// Creates a failed result with the specified error
        /// </summary>
        /// <param name="error">The error</param>
        /// <returns>A failed Result</returns>
        public static Result<TValue, TError> Failure(TError error) =>
            new(default, error, false);

        /// <summary>
        /// Implicit conversion from value to successful Result
        /// </summary>
        public static implicit operator Result<TValue, TError>(TValue value) =>
            Success(value);

        /// <summary>
        /// Attempts to get the success value
        /// </summary>
        /// <param name="value">The success value if successful</param>
        /// <returns>True if successful, false otherwise</returns>
#if !NETSTANDARD2_0
        public bool TryGetValue([NotNullWhen(true)] out TValue value)
#else
        public bool TryGetValue(out TValue value)
#endif
        {
            if (_isSuccess)
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to get the error
        /// </summary>
        /// <param name="error">The error if failed</param>
        /// <returns>True if failed, false otherwise</returns>
#if !NETSTANDARD2_0
        public bool TryGetError([NotNullWhen(true)] out TError error)
#else
        public bool TryGetError(out TError error)
#endif
        {
            if (!_isSuccess)
            {
                error = _error;
                return true;
            }

            error = default;
            return false;
        }

        /// <summary>
        /// Gets the value if successful, otherwise returns the specified default value
        /// </summary>
        /// <param name="defaultValue">Default value to return on failure</param>
        /// <returns>The success value or default</returns>
        public TValue GetValueOrDefault(TValue defaultValue = default) =>
            _isSuccess ? _value : defaultValue;

        /// <summary>
        /// Gets the value if successful, otherwise returns the result of the factory function
        /// </summary>
        /// <param name="defaultFactory">Factory function to create default value</param>
        /// <returns>The success value or factory result</returns>
        public TValue GetValueOrDefault(Func<TValue> defaultFactory) =>
            _isSuccess ? _value : defaultFactory();

        /// <summary>
        /// Gets the value if successful, otherwise returns the result of the error handler
        /// </summary>
        /// <param name="errorHandler">Handler that receives the error and returns a default value</param>
        /// <returns>The success value or handler result</returns>
        public TValue GetValueOrDefault(Func<TError, TValue> errorHandler) =>
            _isSuccess ? _value : errorHandler(_error);

        /// <summary>
        /// Pattern matches on the result, executing the appropriate handler
        /// </summary>
        /// <typeparam name="TResult">Return type of handlers</typeparam>
        /// <param name="onSuccess">Handler for success case</param>
        /// <param name="onFailure">Handler for failure case</param>
        /// <returns>Result of the executed handler</returns>
        public TResult Match<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<TError, TResult> onFailure) =>
            _isSuccess ? onSuccess(_value) : onFailure(_error);

        /// <summary>
        /// Executes the appropriate action based on success or failure
        /// </summary>
        /// <param name="onSuccess">Action for success case</param>
        /// <param name="onFailure">Action for failure case</param>
        public void Switch(
            Action<TValue> onSuccess,
            Action<TError> onFailure)
        {
            if (_isSuccess)
                onSuccess(_value);
            else
                onFailure(_error);
        }

        /// <summary>
        /// Transforms the success value using the specified mapping function
        /// </summary>
        /// <typeparam name="TNew">Type of the new value</typeparam>
        /// <param name="mapper">Function to transform the value</param>
        /// <returns>A new Result with the transformed value or the original error</returns>
        public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper) =>
            _isSuccess
                ? Result<TNew, TError>.Success(mapper(_value))
                : Result<TNew, TError>.Failure(_error);

        /// <summary>
        /// Transforms the error using the specified mapping function
        /// </summary>
        /// <typeparam name="TNewError">Type of the new error</typeparam>
        /// <param name="mapper">Function to transform the error</param>
        /// <returns>A new Result with the original value or transformed error</returns>
        public Result<TValue, TNewError> MapError<TNewError>(Func<TError, TNewError> mapper) =>
            _isSuccess
                ? Result<TValue, TNewError>.Success(_value)
                : Result<TValue, TNewError>.Failure(mapper(_error));

        /// <summary>
        /// Chains another operation that returns a Result
        /// </summary>
        /// <typeparam name="TNew">Type of the new value</typeparam>
        /// <param name="binder">Function that returns a new Result</param>
        /// <returns>The new Result or the original error</returns>
        public Result<TNew, TError> Bind<TNew>(Func<TValue, Result<TNew, TError>> binder) =>
            _isSuccess ? binder(_value) : Result<TNew, TError>.Failure(_error);

        /// <summary>
        /// Executes an action on success, returning the original result
        /// </summary>
        /// <param name="action">Action to execute on success</param>
        /// <returns>The original Result</returns>
        public Result<TValue, TError> Tap(Action<TValue> action)
        {
            if (_isSuccess)
                action(_value);
            return this;
        }

        /// <summary>
        /// Executes an action on failure, returning the original result
        /// </summary>
        /// <param name="action">Action to execute on failure</param>
        /// <returns>The original Result</returns>
        public Result<TValue, TError> TapError(Action<TError> action)
        {
            if (!_isSuccess)
                action(_error);
            return this;
        }

        /// <summary>
        /// Ensures a condition is met, converting to failure if not
        /// </summary>
        /// <param name="predicate">Condition to check</param>
        /// <param name="error">Error to use if condition fails</param>
        /// <returns>The original Result or a failed Result</returns>
        public Result<TValue, TError> Ensure(Func<TValue, bool> predicate, TError error) =>
            _isSuccess && !predicate(_value)
                ? Result<TValue, TError>.Failure(error)
                : this;

        /// <summary>
        /// Converts this Result to use the built-in ResultError type
        /// </summary>
        /// <param name="errorMapper">Function to convert the error to ResultError</param>
        /// <returns>A Result with ResultError</returns>
        public Result<TValue> ToResult(Func<TError, ResultError> errorMapper) =>
            _isSuccess
                ? Result<TValue>.Success(_value)
                : Result<TValue>.Failure(errorMapper(_error));

        public bool Equals(Result<TValue, TError> other) =>
            _isSuccess == other._isSuccess &&
            Equals(_value, other._value) &&
            Equals(_error, other._error);

        public override bool Equals(object obj) =>
            obj is Result<TValue, TError> other && Equals(other);

        public override int GetHashCode()
        {
#if NETSTANDARD2_0
            unchecked
            {
                var hashCode = _isSuccess.GetHashCode();
                hashCode = (hashCode * 397) ^ (_value?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (_error?.GetHashCode() ?? 0);
                return hashCode;
            }
#else
            return HashCode.Combine(_isSuccess, _value, _error);
#endif
        }

        public static bool operator ==(Result<TValue, TError> left, Result<TValue, TError> right) =>
            left.Equals(right);

        public static bool operator !=(Result<TValue, TError> left, Result<TValue, TError> right) =>
            !left.Equals(right);

        public override string ToString() =>
            _isSuccess
                ? $"Success({_value})"
                : $"Failure({_error})";
    }
}
