using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.AspNetCore;

public sealed class CrudQueryResult
{
    public CrudQueryResult(PageRequest page, bool count)
    {
        ArgumentNullException.ThrowIfNull(page);

        Count = count;
        Page = page;
    }

    public bool Count { get; }

    public PageRequest Page { get; }
}
