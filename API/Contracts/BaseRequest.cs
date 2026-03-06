namespace API.Contracts
{
    public abstract class BaseRequest
    {
        //Helps idempotency by providing a unique identifier for each request
        public string? RequestId { get; init; }
    }
}
