using System.Security.Principal;

namespace NextLogonExec.Security;

public sealed class CurrentUserProvider : ICurrentUserProvider
{
    public UserInfo GetCurrentUser()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new UnsupportedPlatformException("Current Windows user information is only available on Windows.");
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new UserInfo(identity.Name, identity.User?.Value);
    }
}
