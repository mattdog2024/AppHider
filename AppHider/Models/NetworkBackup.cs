namespace AppHider.Models;

public class NetworkBackup
{
    public string AdapterName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public List<string> DnsServers { get; set; } = new();
    public bool DhcpEnabled { get; set; }
    public bool AdapterEnabled { get; set; }
    public List<FirewallRuleBackup> FirewallRules { get; set; } = new();
}

public class FirewallRuleBackup
{
    public string RuleName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
