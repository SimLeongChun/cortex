using System;
using System.Threading.Tasks;

namespace Cortex.Types
{
    /// <summary>
    /// Provides static factory methods and utilities for creating Result instances
    /// </summary>
    public static class Result
    {
        /// <summary>
        /// Creates a successful result with the specified value
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="value">The success value</param>
        /// <returns>A successful Result</returns>
        public static Result<T> Success<T>(T value) =>
            Result<T>.Success(value);

        /// <summary>
        /// Creates a successful result with the specified value and custom error type
        /// </summary>
        /// <typeparam name="TValue">Type of the value</typeparam>
        /// <typeparam name="TError">Type of the error</typeparam>
        /// <param name="value">The success value</param>
        /// <returns>A successful Result</returns>
        public static Result<TValue, TError> Success<TValue, TError>(TValue value) =>
            Result<TValue, TError>.Success(value);

        /// <summary>
        /// Creates a failed result with the specified error
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="error">The error</param>
        /// <returns>A failed Result</returns>
        public static Result<T> Failure<T>(ResultError error) =>
            Result<T>.Failure(error);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="errorMessage">The error message</param>
        /// <returns>A failed Result</returns>
        public static Result<T> Failure<T>(string errorMessage) =>
            Result<T>.Failure(errorMessage);

        /// <summary>
        /// Creates a failed result from an exception
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="exception">The exception</param>
        /// <returns>A failed Result</returns>
        public static Result<T> Failure<T>(Exception exception) =>
            Result<T>.Failure(exception);

        /// <summary>
        /// Creates a failed result with the specified error and custom error type
        /// </summary>
        /// <typeparam name="TValue">Type of the value</typeparam>
        /// <typeparam name="TError">Type of the error</typeparam>
        /// <param name="error">The error</param>
        /// <returns>A failed Result</returns>
        public static Result<TValue, TError> Failure<TValue, TError>(TError error) =>
            Result<TValue, TError>.Failure(error);

        /// <summary>
        /// Executes the specified function and wraps any exception in a failed Result
        /// </summary>
        /// <typeparam name="T">Type of the return value</typeparam>
        /// <param name="func">Function to execute</param>
        /// <returns>A Result containing the function result or the caught exception</returns>
        public static Result<T> Try<T>(Func<T> func)
        {
            try
            {
                return Result<T>.Success(func());
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(ex);
            }
        }

        /// <summary>
        /// Executes the specified function and wraps any exception in a failed Result
        /// </summary>
        /// <typeparam name="T">Type of the return value</typeparam>
        /// <param name="func">Function to execute</param>
        /// <param name="exceptionHandler">Handler to convert exception to error</param>
        /// <returns>A Result containing the function result or the handled exception</returns>
        public static Result<T> Try<T>(Func<T> func, Func<Exception, ResultError> exceptionHandler)
        {
            try
            {
                return Result<T>.Success(func());
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(exceptionHandler(ex));
            }
        }

        /// <summary>
        /// Executes the specified async function and wraps any exception in a failed Result
        /// </summary>
        /// <typeparam name="T">Type of the return value</typeparam>
        /// <param name="func">Async function to execute</param>
        /// <returns>A Task containing a Result with the function result or the caught exception</returns>
        public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func)
        {
            try
            {
                return Result<T>.Success(await func().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(ex);
            }
        }

        /// <summary>
        /// Combines two results, returning failure if either fails
        /// </summary>
        public static Result<(T1, T2)> Combine<T1, T2>(
            Result<T1> result1,
            Result<T2> result2)
        {
            if (result1.IsFailure)
                return Result<(T1, T2)>.Failure(result1.Error);
            if (result2.IsFailure)
                return Result<(T1, T2)>.Failure(result2.Error);

            return Result<(T1, T2)>.Success((result1.Value, result2.Value));
        }

        /// <summary>
        /// Combines three results, returning failure if any fails
        /// </summary>
        public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
            Result<T1> result1,
            Result<T2> result2,
            Result<T3> result3)
        {
            if (result1.IsFailure)
                return Result<(T1, T2, T3)>.Failure(result1.Error);
            if (result2.IsFailure)
                return Result<(T1, T2, T3)>.Failure(result2.Error);
            if (result3.IsFailure)
                return Result<(T1, T2, T3)>.Failure(result3.Error);

            return Result<(T1, T2, T3)>.Success((result1.Value, result2.Value, result3.Value));
        }

        /// <summary>
        /// Creates a Result based on a condition
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="condition">Condition to evaluate</param>
        /// <param name="value">Value to use if condition is true</param>
        /// <param name="error">Error to use if condition is false</param>
        /// <returns>Success or Failure Result based on condition</returns>
        public static Result<T> SuccessIf<T>(bool condition, T value, ResultError error) =>
            condition ? Result<T>.Success(value) : Result<T>.Failure(error);

        /// <summary>
        /// Creates a Result based on a condition
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="condition">Condition to evaluate</param>
        /// <param name="value">Value to use if condition is true</param>
        /// <param name="errorMessage">Error message to use if condition is false</param>
        /// <returns>Success or Failure Result based on condition</returns>
        public static Result<T> SuccessIf<T>(bool condition, T value, string errorMessage) =>
            condition ? Result<T>.Success(value) : Result<T>.Failure(errorMessage);

        /// <summary>
        /// Creates a Result based on a condition (inverted)
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="condition">Condition to evaluate</param>
        /// <param name="value">Value to use if condition is false</param>
        /// <param name="error">Error to use if condition is true</param>
        /// <returns>Success or Failure Result based on condition</returns>
        public static Result<T> FailureIf<T>(bool condition, T value, ResultError error) =>
            condition ? Result<T>.Failure(error) : Result<T>.Success(value);

        /// <summary>
        /// Creates a Result based on a condition (inverted)
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="condition">Condition to evaluate</param>
        /// <param name="value">Value to use if condition is false</param>
        /// <param name="errorMessage">Error message to use if condition is true</param>
        /// <returns>Success or Failure Result based on condition</returns>
        public static Result<T> FailureIf<T>(bool condition, T value, string errorMessage) =>
            condition ? Result<T>.Failure(errorMessage) : Result<T>.Success(value);
    }
}
