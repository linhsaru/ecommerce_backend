using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common
{
    public class Result
    {
        public bool IsSuccess { get;}
        public bool IsFailure => !IsSuccess;

        public IReadOnlyList<Error> Errors { get; }

        protected Result(bool isSuccess, List<Error> errors)
        {
            IsSuccess = isSuccess;
            Errors = (errors ?? new List<Error>()).AsReadOnly();
        }

        public static Result Ok() => new(true, null);

        public static Result Fail(params Error[] errors) => new(false, errors.ToList());

        public static Result Fail(string code, string message, string? details = null) 
            => new(false, new List<Error> { new Error(code, message, details) });

    }

    // Khi dùng CQRS + MediaR, handler trả Result<T> để map ra API response
    public sealed class Result<T> : Result
    {
        public T? Value { get; }

        private Result(bool isSuccess, T? value, List<Error> errors) : base(isSuccess, errors)
        {
            Value = value;
        }

        public static Result<T> Ok(T value) => new(true, value, null);

        public static new Result<T> Fail(params Error[] errors) 
            => new(false, default, errors.ToList());

        public static Result<T> Fail(string code, string message, string? details = null)
        {
            return new(false, default, new List<Error> { new(code, message, details) });
        }
    }
}
