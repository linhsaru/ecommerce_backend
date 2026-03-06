using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Contracts
{
    public sealed class PagedResponse<T>
    {
        public IReadOnlyList<T> items { get; init; } = Array.Empty<T>();

        public int Page { get; init; }
        public int PageSize { get; init; }
        public long TotalItems { get; init; }
        public int TotalPages => (int)Math.Ceiling(TotalItems/(double)PageSize);

        public bool HasNext => Page < TotalPages;
        public bool HasPrev => Page > 1;

        public static PagedResponse<T> Create(IEnumerable<T> items, int page, int pageSize, long total)
        {
            return new PagedResponse<T>
            {
                items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };
        }
    }
}
