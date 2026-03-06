using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common
{
    public abstract class SoftDeleteEntity<TKey> : AuditableEntity<TKey>
    {
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        public bool IsDeleted => DeletedAt.HasValue;

        public void MarkDeleted(string? by = null)
        {
            if (IsDeleted) return;
            DeletedAt = DateTimeOffset.UtcNow;
            DeletedBy = by;
        }

        public void Restore(string? by)
        {
            DeletedAt = null;
            DeletedBy = by;
            UpdatedAt = DateTimeOffset.UtcNow;
            UpdatedBy = by;
        }
    }
}
