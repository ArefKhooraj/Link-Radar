using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class User : BaseEntity
{
    public string Name { get; set; }

    public string ApiKey { get; set; }

    public UserStatus IsActive { get; set; }

    #region Relation

    public ICollection<Webhook> Webhooks { get; set; }

    #endregion
}

public enum UserStatus
{
    Active,
    Inactive
}