using System.Collections.Generic;

namespace TimeVault.Api.Infrastructure.Common
{
    /// <summary>
    /// Represents the result of an operation.
    /// </summary>
    /// <typeparam name="T">The type of data returned by the operation.</typeparam>
    public class Result<T>
    {
        public bool Success { get; private set; }
        public T Data { get; private set; }
        public string Message { get; private set; }
        public string Error { get; private set; }
        public List<string> ValidationErrors { get; private set; }

        private Result() 
        {
            ValidationErrors = new List<string>();
        }

        /// <summary>
        /// Creates a successful result with data.
        /// </summary>
        public static Result<T> SuccessWithData(T data, string message = "")
        {
            return new Result<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        /// <summary>
        /// Creates a successful result without data.
        /// </summary>
        public static Result<T> SuccessWithoutData(string message = "")
        {
            return new Result<T>
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result<T> Failure(string error)
        {
            return new Result<T>
            {
                Success = false,
                Error = error
            };
        }

        /// <summary>
        /// Creates a failed result with validation errors.
        /// </summary>
        public static Result<T> ValidationFailed(List<string> validationErrors)
        {
            return new Result<T>
            {
                Success = false,
                ValidationErrors = validationErrors,
                Error = "Validation failed"
            };
        }
    }

    /// <summary>
    /// Non-generic version of Result for operations that don't return data
    /// </summary>
    public class Result
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public string Error { get; private set; }
        public List<string> ValidationErrors { get; private set; }

        private Result()
        {
            ValidationErrors = new List<string>();
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result SuccessResult(string message = "")
        {
            return new Result
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result Failure(string error)
        {
            return new Result
            {
                Success = false,
                Error = error
            };
        }

        /// <summary>
        /// Creates a failed result with validation errors.
        /// </summary>
        public static Result ValidationFailed(List<string> validationErrors)
        {
            return new Result
            {
                Success = false,
                ValidationErrors = validationErrors,
                Error = "Validation failed"
            };
        }
    }
} 