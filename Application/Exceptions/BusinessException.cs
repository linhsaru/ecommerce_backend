using Application.Common;

namespace Application.Exceptions;

/// <summary>
/// Exception cho loi nghiệp vụ (business rule violation).
/// </summary>
public sealed class BusinessException : AppException
{
    public BusinessException(string code, string message) : base(code, message)
    {
    }

    public BusinessException(string message) : base("BUSINESS_ERROR", message)
    {
    }
}
