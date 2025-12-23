namespace AppHider.Models;

public class NetworkState
{
    public bool IsEnabled { get; set; }
    public string CurrentIpAddress { get; set; } = string.Empty;
    public bool FirewallActive { get; set; }
    public bool DnsServiceRunning { get; set; }
}
