using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common
{
    public abstract class AppException : Exception
    {
        public string Code { get; }
        protected AppException(string code, string message) : base(message)
        {
            Code = code;
        }
    }

    public sealed class NotFoundException : AppException
    {
        public NotFoundException(string message) : base("NOT_FOUND", message)
        {
        }
    }

    public sealed class ValidationException : AppException
    {
        public IReadOnlyDictionary<string, string[]> Failures { get; }

        public ValidationException(IReadOnlyDictionary<string, string[]> failures)
            : base("VALIDATION_ERROR", "Validation failed")
        {
            Failures = failures;
        }
    }
}
