namespace LinkRadar.Api.Interface;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    public int UserId
    {
        get
        {
            var userId = httpContextAccessor.HttpContext?
                .Items["UserId"];

            if (userId is not int id)
                throw new UnauthorizedAccessException();

            return id;
        }
    }
}