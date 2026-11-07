namespace WindowsIconsAdmin.Core.Apps;

public interface IAppDiscoveryService
{
    IReadOnlyList<InstalledApp> DiscoverInstalledApps();
}
