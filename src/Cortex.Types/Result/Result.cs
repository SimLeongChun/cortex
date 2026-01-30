using System;
using System.Diagnostics.CodeAnalysis;

namespace Cortex.Types
{
    /// <summary>
    /// Represents the result of an operation that can succeed with a value or fail with a built-in error
    /// </summary>
    /// <typeparam name="T">Type of the success value</typeparam>
    public readonly struct Result<T> : IEquatable<Result<T>>, IResult<T, ResultError>
    {
        private readonly T _value;
        private readonly ResultError _error;
        private readonly bool _isSuccess;

        /// <inheritdoc />
        public bool IsSuccess => _isSuccess;

        /// <inheritdoc />
        public bool IsFailure => !_isSuccess;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result</exception>
        public T Value => _isSuccess
            ? _value
            : throw new InvalidOperationException(
                $"Cannot access Value on a failed Result. Error: {_error}");

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result</exception>
        public ResultError Error => !_isSuccess
            ? _error
            : throw new InvalidOperationException(
                "Cannot access Error on a successful Result");

        private Result(T value, ResultError error, bool isSuccess)
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
        public static Result<T> Success(T value) =>
            new(value, default, true);

        /// <summary>
        /// Creates a failed result with the specified error
        /// </summary>
        /// <param name="error">The error</param>
        /// <returns>A failed Result</returns>
        public static Result<T> Failure(ResultError error) =>
            new(default, error ?? throw new ArgumentNullException(nameof(error)), false);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <returns>A failed Result</returns>
        public static Result<T> Failure(string errorMessage) =>
            new(default, new ResultError(errorMessage), false);

        /// <summary>
        /// Creates a failed result from an exception
        /// </summary>
        /// <param name="exception">The exception</param>
        /// <returns>A failed Result</returns>
        public static Result<T> Failure(Exception exception) =>
            new(default, ResultError.FromException(exception), false);

        /// <summary>
        /// Implicit conversion from value to successful Result
        /// </summary>
        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Implicit conversion from ResultError to failed Result
        /// </summary>
        public static implicit operator Result<T>(ResultError error) => Failure(error);

        /// <summary>
        /// Attempts to get the success value
        /// </summary>
        /// <param name="value">The success value if successful</param>
        /// <returns>True if successful, false otherwise</returns>
#if !NETSTANDARD2_0
        public bool TryGetValue([NotNullWhen(true)] out T value)
#else
        public bool TryGetValue(out T value)
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
        public bool TryGetError([NotNullWhen(true)] out ResultError error)
#else
        public bool TryGetError(out ResultError error)
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
        public T GetValueOrDefault(T defaultValue = default) =>
            _isSuccess ? _value : defaultValue;

        /// <summary>
        /// Gets the value if successful, otherwise returns the result of the factory function
        /// </summary>
        /// <param name="defaultFactory">Factory function to create default value</param>
        /// <returns>The success value or factory result</returns>
        public T GetValueOrDefault(Func<T> defaultFactory) =>
            _isSuccess ? _value : defaultFactory();

        /// <summary>
        /// Gets the value if successful, otherwise returns the result of the error handler
        /// </summary>
        /// <param name="errorHandler">Handler that receives the error and returns a default value</param>
        /// <returns>The success value or handler result</returns>
        public T GetValueOrDefault(Func<ResultError, T> errorHandler) =>
            _isSuccess ? _value : errorHandler(_error);

        /// <summary>
        /// Pattern matches on the result, executing the appropriate handler
        /// </summary>
        /// <typeparam name="TResult">Return type of handlers</typeparam>
        /// <param name="onSuccess">Handler for success case</param>
        /// <param name="onFailure">Handler for failure case</param>
        /// <returns>Result of the executed handler</returns>
        public TResult Match<TResult>(
            Func<T, TResult> onSuccess,
            Func<ResultError, TResult> onFailure) =>
            _isSuccess ? onSuccess(_value) : onFailure(_error);

        /// <summary>
        /// Executes the appropriate action based on success or failure
        /// </summary>
        /// <param name="onSuccess">Action for success case</param>
        /// <param name="onFailure">Action for failure case</param>
        public void Switch(
            Action<T> onSuccess,
            Action<ResultError> onFailure)
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
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
            _isSuccess
                ? Result<TNew>.Success(mapper(_value))
                : Result<TNew>.Failure(_error);

        /// <summary>
        /// Transforms the error using the specified mapping function
        /// </summary>
        /// <param name="mapper">Function to transform the error</param>
        /// <returns>A new Result with the original value or transformed error</returns>
        public Result<T> MapError(Func<ResultError, ResultError> mapper) =>
            _isSuccess
                ? this
                : Result<T>.Failure(mapper(_error));

        /// <summary>
        /// Chains another operation that returns a Result
        /// </summary>
        /// <typeparam name="TNew">Type of the new value</typeparam>
        /// <param name="binder">Function that returns a new Result</param>
        /// <returns>The new Result or the original error</returns>
        public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
            _isSuccess ? binder(_value) : Result<TNew>.Failure(_error);

        /// <summary>
        /// Executes an action on success, returning the original result
        /// </summary>
        /// <param name="action">Action to execute on success</param>
        /// <returns>The original Result</returns>
        public Result<T> Tap(Action<T> action)
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
        public Result<T> TapError(Action<ResultError> action)
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
        public Result<T> Ensure(Func<T, bool> predicate, ResultError error) =>
            _isSuccess && !predicate(_value)
                ? Result<T>.Failure(error)
                : this;

        /// <summary>
        /// Ensures a condition is met, converting to failure if not
        /// </summary>
        /// <param name="predicate">Condition to check</param>
        /// <param name="errorMessage">Error message to use if condition fails</param>
        /// <returns>The original Result or a failed Result</returns>
        public Result<T> Ensure(Func<T, bool> predicate, string errorMessage) =>
            Ensure(predicate, new ResultError(errorMessage));

        public bool Equals(Result<T> other) =>
            _isSuccess == other._isSuccess &&
            Equals(_value, other._value) &&
            Equals(_error, other._error);

        public override bool Equals(object obj) =>
            obj is Result<T> other && Equals(other);

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

        public static bool operator ==(Result<T> left, Result<T> right) =>
            left.Equals(right);

        public static bool operator !=(Result<T> left, Result<T> right) =>
            !left.Equals(right);

        public override string ToString() =>
            _isSuccess
                ? $"Success({_value})"
                : $"Failure({_error})";
    }
}
