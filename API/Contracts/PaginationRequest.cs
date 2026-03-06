namespace API.Contracts
{
    public sealed class PaginationRequest
    {
        private const int MaxPageSize = 100;
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 10;

        public int GetSkip() => (Math.Max(Page, 1) - 1) * GetTake();

        public int GetTake() => Math.Clamp(PageSize, 1, MaxPageSize);
    }
}
