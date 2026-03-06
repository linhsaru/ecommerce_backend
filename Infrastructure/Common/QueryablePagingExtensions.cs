using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Common
{
    public static class QueryablePagingExtensions
    {

        public static async Task<(List<T> items, long total)> toPagedAsync<T>(
            this IQueryable<T> query,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var safePage = Math.Max(page, 1);
            var safeSize = Math.Clamp(pageSize, 1, 100);
            var total = await query.LongCountAsync(ct);
            var items = await query.Skip((safePage - 1)*safeSize).Take(safeSize).ToListAsync(ct);

            return (items, total);
        }
    }
}
