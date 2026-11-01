namespace LinkRadar.Api.Entities.Common;

public class BaseEntity<T>
{
    public T Id { get; set; } = default!;

    public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastChangeDate { get; set; }

    public bool IsDeleted { get; set; }
}

public class BaseEntity : BaseEntity<int>;