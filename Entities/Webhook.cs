using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class Webhook : BaseEntity
{
    public int UserId { get; set; }
    
    public string WebhookUrl { get; set; }

    public string HeaderName { get; set; }

    public string HeaderValue { get; set; }

    #region Relation

    public User User { get; set; }

    #endregion
}