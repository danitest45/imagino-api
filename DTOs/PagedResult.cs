using System.Collections.Generic;

namespace Imagino.Api.DTOs
{
    public class PagedResult<T>
    {
        public required IEnumerable<T> Items { get; set; }

        public long Total { get; set; }
    }
}
