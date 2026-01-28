using System;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Types
{
    /// <summary>
    /// Represents an error in a Result operation with a message and optional exception
    /// </summary>
    public sealed class ResultError : IEquatable<ResultError>
    {
        /// <summary>
        /// Gets the error message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the error code (optional)
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the inner exception if one was the cause of the error
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets additional metadata about the error
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Creates a new ResultError with the specified message
        /// </summary>
        /// <param name="message">Error message</param>
        public ResultError(string message)
            : this(message, null, null, null)
        {
        }

        /// <summary>
        /// Creates a new ResultError with the specified message and code
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="code">Error code</param>
        public ResultError(string message, string code)
            : this(message, code, null, null)
        {
        }

        /// <summary>
        /// Creates a new ResultError with the specified message and exception
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="exception">Underlying exception</param>
        public ResultError(string message, Exception exception)
            : this(message, null, exception, null)
        {
        }

        /// <summary>
        /// Creates a new ResultError with all properties
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="code">Error code</param>
        /// <param name="exception">Underlying exception</param>
        /// <param name="metadata">Additional metadata</param>
        public ResultError(
            string message,
            string code,
            Exception exception,
            IDictionary<string, object> metadata)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Code = code;
            Exception = exception;
            Metadata = metadata != null
                ? new Dictionary<string, object>(metadata)
                : new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a ResultError from an exception
        /// </summary>
        /// <param name="exception">The exception to convert</param>
        /// <returns>A new ResultError</returns>
        public static ResultError FromException(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return new ResultError(
                exception.Message,
                exception.GetType().Name,
                exception,
                null);
        }

        /// <summary>
        /// Creates a composite error from multiple errors
        /// </summary>
        /// <param name="errors">Collection of errors</param>
        /// <returns>A new ResultError representing all errors</returns>
        public static ResultError Aggregate(IEnumerable<ResultError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            var errorList = errors.ToList();
            if (errorList.Count == 0)
                throw new ArgumentException("At least one error is required", nameof(errors));

            if (errorList.Count == 1)
                return errorList[0];

            var messages = string.Join("; ", errorList.Select(e => e.Message));
            var metadata = new Dictionary<string, object>
            {
                ["InnerErrors"] = errorList
            };

            return new ResultError(
                $"Multiple errors occurred: {messages}",
                "AGGREGATE_ERROR",
                null,
                metadata);
        }

        public bool Equals(ResultError other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Message == other.Message && Code == other.Code;
        }

        public override bool Equals(object obj) =>
            obj is ResultError other && Equals(other);

        public override int GetHashCode()
        {
#if NETSTANDARD2_0
            unchecked
            {
                return ((Message?.GetHashCode() ?? 0) * 397) ^ (Code?.GetHashCode() ?? 0);
            }
#else
            return HashCode.Combine(Message, Code);
#endif
        }

        public static bool operator ==(ResultError left, ResultError right) =>
            Equals(left, right);

        public static bool operator !=(ResultError left, ResultError right) =>
            !Equals(left, right);

        public override string ToString() =>
            string.IsNullOrEmpty(Code)
                ? Message
                : $"[{Code}] {Message}";
    }
}
