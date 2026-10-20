namespace Neox.Extensions.Crud.Shared;

public interface ICrudService<TEntity>
    where TEntity : IEntity
{
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedList<TEntity>> ListAsync(PageRequest page, CancellationToken cancellationToken = default);

    Task<TEntity?> RetrieveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
}
