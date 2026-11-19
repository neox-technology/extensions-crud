using Microsoft.EntityFrameworkCore;
using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Services;

public sealed class EfCrudService<TEntity, TContext> : ICrudService<TEntity>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly Func<TContext, DbSet<TEntity>> _set;

    public EfCrudService(TContext context, Func<TContext, DbSet<TEntity>> setSelector)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(setSelector);

        _context = context;
        _set = setSelector;
    }

    private DbSet<TEntity> Set => _set(_context);

    public async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        Set.Add(entity);
        await UniqueConstraintGuard.SaveChangesAsync(_context, cancellationToken).ConfigureAwait(false);

        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await RetrieveAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entity '{id}' was not found.");

        Set.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedList<TEntity>> ListAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(page.Filters);
        ArgumentNullException.ThrowIfNull(page.Sorts);

        var filtered = CrudQueryApplier.ApplyFilters(Set.AsQueryable(), page.Filters);
        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await CrudQueryApplier.ApplySorts<TEntity>(filtered, page.Sorts)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedList<TEntity>(items, totalCount, page.Page, page.PageSize);
    }

    public Task<TEntity?> RetrieveAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        Set.Update(entity);
        await UniqueConstraintGuard.SaveChangesAsync(_context, cancellationToken).ConfigureAwait(false);

        return entity;
    }
}
