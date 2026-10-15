using Microsoft.EntityFrameworkCore;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Extensions;

public static class ModelBuilderExtensions
{
    public static ModelBuilder IsolateSchema(this ModelBuilder modelBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        modelBuilder.HasDefaultSchema(schema);

        return modelBuilder;
    }
}
