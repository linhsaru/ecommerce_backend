namespace API.Contracts
{
    public sealed class SoftRequest
    {
        public string? Soft { get; init; }

        public (string field, bool desc)? Parse()
        {
            if(string.IsNullOrWhiteSpace(Soft)) return null;
            var s = Soft.Trim();
            var desc = s.StartsWith("-");
            var field = desc ? s[1..] : s;
            return (field, desc);
        }
    }
}
