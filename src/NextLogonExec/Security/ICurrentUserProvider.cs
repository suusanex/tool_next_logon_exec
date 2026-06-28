namespace NextLogonExec.Security;

public interface ICurrentUserProvider
{
    UserInfo GetCurrentUser();
}

public sealed record UserInfo(string Name, string? Sid);
