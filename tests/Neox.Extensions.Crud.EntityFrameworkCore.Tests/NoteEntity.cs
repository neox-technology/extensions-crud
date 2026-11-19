using Neox.Extensions.Crud.Shared;

namespace Neox.Extensions.Crud.EntityFrameworkCore.Tests;

public sealed class NoteEntity : IEntity
{
    public bool? Archived { get; set; }

    public Guid Id { get; set; }

    public List<NoteTagEntity> Tags { get; set; } = [];

    public string Title { get; set; } = string.Empty;
}
