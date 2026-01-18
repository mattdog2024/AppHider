using System.Threading.Tasks;

namespace AppHider.Services;

public interface IVHDXManager
{
    /// <summary>
    /// Mounts a VHDX/LVM file and unlocks it.
    /// </summary>
    /// <param name="filePath">Path to the .lvm/.vhdx file</param>
    /// <param name="password">Password for BitLocker unlock</param>
    /// <returns>True if successful</returns>
    Task<bool> MountVHDXAsync(string filePath, string password);

    /// <summary>
    /// Forcefully dismounts the VHDX file and cleans up.
    /// </summary>
    /// <returns>True if successful</returns>
    Task<bool> DismountVHDXAsync();

    /// <summary>
    /// Checks if the VHDX is currently mounted.
    /// </summary>
    bool IsMounted { get; }
}
