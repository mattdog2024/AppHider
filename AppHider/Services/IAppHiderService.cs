using AppHider.Models;

namespace AppHider.Services;

public interface IAppHiderService
{
    IReadOnlyList<ProcessInfo> GetRunningApplications();
    Task HideApplicationsAsync(IEnumerable<int> processIds);
    Task ShowApplicationsAsync(IEnumerable<int> processIds);
    Task<bool> IsProcessHidden(int processId);
}
