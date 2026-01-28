using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BHBWorker;

/// <summary>
/// Linux system management service for enterprise features:
/// - Service/daemon management (systemctl)
/// - Docker/container management
/// - User/group management
/// - Package management (apt/yum/dnf)
/// - Firewall management (ufw/iptables/firewalld)
/// - Cron job management
/// - SSH session monitoring
/// - Security auditing
/// - Remote command execution
/// - File browser
/// </summary>
public class LinuxSystemService
{
    private readonly ILogger<LinuxSystemService> _logger;
    private readonly string _workerId;
    private readonly bool _isLinux;

    public LinuxSystemService(ILogger<LinuxSystemService> logger, string workerId)
    {
        _logger = logger;
        _workerId = workerId;
        _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    #region Service/Daemon Management

    public async Task<ServiceListResponse> GetServicesAsync(CancellationToken ct = default)
    {
        var response = new ServiceListResponse { WorkerId = _workerId };

        if (!_isLinux)
        {
            response.Error = "Service management is only available on Linux systems";
            return response;
        }

        try
        {
            var output = await ExecuteCommandAsync("systemctl list-units --type=service --all --no-pager --plain", ct);
            if (!output.Success)
            {
                response.Error = output.Error;
                return response;
            }

            var lines = output.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = Regex.Split(line.Trim(), @"\s+");
                if (parts.Length < 4) continue;

                var serviceName = parts[0].Replace(".service", "");
                var loadState = parts[1];
                var activeState = parts[2];
                var subState = parts[3];
                var description = parts.Length > 4 ? string.Join(" ", parts.Skip(4)) : "";

                response.Services.Add(new ServiceInfo
                {
                    Name = serviceName,
                    Status = activeState,
                    State = loadState,
                    SubState = subState,
                    IsActive = activeState == "active",
                    Description = description
                });
            }

            // Get enabled/disabled state for each service
            foreach (var service in response.Services)
            {
                var enabledCheck = await ExecuteCommandAsync($"systemctl is-enabled {service.Name}.service 2>/dev/null || true", ct);
                service.IsEnabled = enabledCheck.StdOut.Trim() == "enabled";
            }

            response.TotalCount = response.Services.Count;
            response.RunningCount = response.Services.Count(s => s.IsActive);
            response.FailedCount = response.Services.Count(s => s.Status == "failed");
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services");
            response.Error = ex.Message;
        }

        return response;
    }

    public async Task<ServiceActionResponse> ControlServiceAsync(ServiceActionRequest request, CancellationToken ct = default)
    {
        var response = new ServiceActionResponse
        {
            WorkerId = _workerId,
            ServiceName = request.ServiceName,
            Action = request.Action
        };

        if (!_isLinux)
        {
            response.Message = "Service management is only available on Linux systems";
            return response;
        }

        var validActions = new[] { "start", "stop", "restart", "reload", "enable", "disable", "status" };
        if (!validActions.Contains(request.Action.ToLower()))
        {
            response.Message = $"Invalid action. Valid actions: {string.Join(", ", validActions)}";
            return response;
        }

        try
        {
            var output = await ExecuteCommandAsync($"sudo systemctl {request.Action} {request.ServiceName}", ct);
            response.Success = output.ExitCode == 0;
            response.Message = output.Success ? $"Service {request.ServiceName} {request.Action} successful" : output.StdErr;
            response.Output = output.StdOut;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error controlling service {Service}", request.ServiceName);
            response.Message = ex.Message;
        }

        return response;
    }

    #endregion

    #region Docker/Container Management

    public async Task<ContainerListResponse> GetContainersAsync(bool includeAll = true, CancellationToken ct = default)
    {
        var response = new ContainerListResponse { WorkerId = _workerId };

        try
        {
            // Check if Docker is available
            var versionCheck = await ExecuteCommandAsync("docker --version 2>/dev/null || true", ct);
            response.DockerAvailable = versionCheck.Success && versionCheck.StdOut.Contains("Docker");
            
            if (!response.DockerAvailable)
            {
                response.Error = "Docker is not installed or not accessible";
                return response;
            }

            response.DockerVersion = versionCheck.StdOut.Trim();

            var allFlag = includeAll ? "-a" : "";
            var output = await ExecuteCommandAsync($"docker ps {allFlag} --format '{{{{.ID}}}}|{{{{.Names}}}}|{{{{.Image}}}}|{{{{.Status}}}}|{{{{.State}}}}|{{{{.Ports}}}}|{{{{.CreatedAt}}}}'", ct);

            if (!output.Success)
            {
                response.Error = output.Error;
                return response;
            }

            var lines = output.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                var container = new ContainerInfo
                {
                    Id = parts[0],
                    Name = parts[1],
                    Image = parts[2],
                    Status = parts[3],
                    State = parts[4],
                    Ports = parts.Length > 5 ? parts[5] : ""
                };

                // Get stats for running containers
                if (container.State == "running")
                {
                    var stats = await ExecuteCommandAsync($"docker stats {container.Id} --no-stream --format '{{{{.CPUPerc}}}}|{{{{.MemUsage}}}}|{{{{.MemPerc}}}}|{{{{.NetIO}}}}|{{{{.BlockIO}}}}'", ct);
                    if (stats.Success)
                    {
                        var statParts = stats.StdOut.Trim().Split('|');
                        if (statParts.Length >= 3)
                        {
                            container.CpuPercent = ParsePercent(statParts[0]);
                            container.MemoryPercent = ParsePercent(statParts[2]);
                            if (statParts.Length > 3) container.NetworkIO = statParts[3];
                            if (statParts.Length > 4) container.BlockIO = statParts[4];
                        }
                    }
                }

                response.Containers.Add(container);
            }

            response.TotalCount = response.Containers.Count;
            response.RunningCount = response.Containers.Count(c => c.State == "running");
            response.StoppedCount = response.Containers.Count(c => c.State != "running");
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting containers");
            response.Error = ex.Message;
        }

        return response;
    }

    public async Task<ContainerActionResponse> ControlContainerAsync(ContainerActionRequest request, CancellationToken ct = default)
    {
        var response = new ContainerActionResponse
        {
            WorkerId = _workerId,
            ContainerId = request.ContainerId,
            Action = request.Action
        };

        var validActions = new[] { "start", "stop", "restart", "pause", "unpause", "remove", "logs" };
        if (!validActions.Contains(request.Action.ToLower()))
        {
            response.Message = $"Invalid action. Valid actions: {string.Join(", ", validActions)}";
            return response;
        }

        try
        {
            string command;
            if (request.Action.ToLower() == "logs")
            {
                var lines = request.LogLines ?? 100;
                command = $"docker logs --tail {lines} {request.ContainerId}";
            }
            else if (request.Action.ToLower() == "remove")
            {
                command = $"docker rm -f {request.ContainerId}";
            }
            else
            {
                command = $"docker {request.Action} {request.ContainerId}";
            }

            var output = await ExecuteCommandAsync(command, ct);
            response.Success = output.ExitCode == 0;
            response.Message = output.Success ? $"Container {request.Action} successful" : output.StdErr;

            if (request.Action.ToLower() == "logs")
            {
                response.Logs = output.StdOut.Split('\n').ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error controlling container {Container}", request.ContainerId);
            response.Message = ex.Message;
        }

        return response;
    }

    #endregion

    #region User Management

    public async Task<UserListResponse> GetUsersAsync(CancellationToken ct = default)
    {
        var response = new UserListResponse { WorkerId = _workerId };

        if (!_isLinux)
        {
            response.Error = "User management is only available on Linux systems";
            return response;
        }

        try
        {
            var passwdContent = await File.ReadAllTextAsync("/etc/passwd", ct);
            var lines = passwdContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length < 7) continue;

                var uid = int.Parse(parts[2]);
                var user = new SystemUser
                {
                    Username = parts[0],
                    Uid = uid,
                    Gid = int.Parse(parts[3]),
                    FullName = parts[4].Split(',')[0],
                    HomeDirectory = parts[5],
                    Shell = parts[6],
                    IsSystemUser = uid < 1000 || uid == 65534
                };

                // Get user groups
                var groupsOutput = await ExecuteCommandAsync($"groups {user.Username} 2>/dev/null || true", ct);
                if (groupsOutput.Success)
                {
                    var groupsPart = groupsOutput.StdOut.Split(':');
                    if (groupsPart.Length > 1)
                    {
                        user.Groups = groupsPart[1].Trim().Split(' ').ToList();
                    }
                }

                // Check if account is locked
                var shadowCheck = await ExecuteCommandAsync($"sudo passwd -S {user.Username} 2>/dev/null || true", ct);
                if (shadowCheck.Success)
                {
                    user.IsLocked = shadowCheck.StdOut.Contains(" L ");
                }

                response.Users.Add(user);
            }

            response.TotalCount = response.Users.Count;
            response.SystemUserCount = response.Users.Count(u => u.IsSystemUser);
            response.HumanUserCount = response.Users.Count(u => !u.IsSystemUser);
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            response.Error = ex.Message;
        }

        return response;
    }

    #endregion

    #region Firewall Management

    public async Task<FirewallStatusResponse> GetFirewallStatusAsync(CancellationToken ct = default)
    {
        var response = new FirewallStatusResponse { WorkerId = _workerId };

        if (!_isLinux)
        {
            response.Error = "Firewall management is only available on Linux systems";
            return response;
        }

        try
        {
            // Try UFW first
            var ufwCheck = await ExecuteCommandAsync("which ufw 2>/dev/null && sudo ufw status verbose 2>/dev/null || true", ct);
            if (ufwCheck.Success && ufwCheck.StdOut.Contains("Status:"))
            {
                response.FirewallType = "ufw";
                response.IsActive = ufwCheck.StdOut.Contains("Status: active");

                var lines = ufwCheck.StdOut.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Default:"))
                    {
                        if (line.Contains("incoming")) response.DefaultIncoming = line.Contains("deny") ? "deny" : "allow";
                        if (line.Contains("outgoing")) response.DefaultOutgoing = line.Contains("deny") ? "deny" : "allow";
                    }
                }

                // Parse UFW rules
                var rulesOutput = await ExecuteCommandAsync("sudo ufw status numbered 2>/dev/null || true", ct);
                if (rulesOutput.Success)
                {
                    var ruleLines = rulesOutput.StdOut.Split('\n').Where(l => l.StartsWith("["));
                    foreach (var ruleLine in ruleLines)
                    {
                        var match = Regex.Match(ruleLine, @"\[(\d+)\]\s+(.+?)\s+(ALLOW|DENY|REJECT)\s+(.+)");
                        if (match.Success)
                        {
                            response.Rules.Add(new FirewallRule
                            {
                                Number = int.Parse(match.Groups[1].Value),
                                Port = match.Groups[2].Value,
                                Action = match.Groups[3].Value,
                                Source = match.Groups[4].Value
                            });
                        }
                    }
                }

                response.Success = true;
                return response;
            }

            // Try firewalld
            var firewalldCheck = await ExecuteCommandAsync("which firewall-cmd 2>/dev/null && sudo firewall-cmd --state 2>/dev/null || true", ct);
            if (firewalldCheck.Success && firewalldCheck.StdOut.Contains("running"))
            {
                response.FirewallType = "firewalld";
                response.IsActive = true;
                response.Success = true;
                return response;
            }

            // Fallback to iptables
            var iptablesCheck = await ExecuteCommandAsync("sudo iptables -L -n 2>/dev/null || true", ct);
            if (iptablesCheck.Success)
            {
                response.FirewallType = "iptables";
                response.IsActive = true;
                response.Success = true;
            }
            else
            {
                response.Error = "No supported firewall found (ufw, firewalld, or iptables)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting firewall status");
            response.Error = ex.Message;
        }

        return response;
    }

    #endregion

    #region SSH Session Management

    public async Task<SshSessionListResponse> GetSshSessionsAsync(CancellationToken ct = default)
    {
        var response = new SshSessionListResponse { WorkerId = _workerId };

        if (!_isLinux)
        {
            response.Error = "SSH session monitoring is only available on Linux systems";
            return response;
        }

        try
        {
            var output = await ExecuteCommandAsync("who -u", ct);
            if (!output.Success)
            {
                response.Error = output.Error;
                return response;
            }

            var lines = output.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = Regex.Split(line.Trim(), @"\s+");
                if (parts.Length < 5) continue;

                response.Sessions.Add(new SshSession
                {
                    User = parts[0],
                    Tty = parts[1],
                    RemoteHost = parts.Length > 5 ? parts[5].Trim('(', ')') : "local",
                    Pid = parts.Length > 4 ? parts[4] : "",
                    IdleTime = parts.Length > 3 ? parts[3] : ""
                });
            }

            response.ActiveCount = response.Sessions.Count;
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SSH sessions");
            response.Error = ex.Message;
        }

        return response;
    }

    #endregion

    #region Security Audit

    public async Task<SecurityAuditInfo> GetSecurityAuditAsync(CancellationToken ct = default)
    {
        var audit = new SecurityAuditInfo { WorkerId = _workerId };

        if (!_isLinux)
        {
            return audit;
        }

        try
        {
            // Check SSH config
            if (File.Exists("/etc/ssh/sshd_config"))
            {
                var sshConfig = await File.ReadAllTextAsync("/etc/ssh/sshd_config", ct);
                audit.SshPasswordAuthEnabled = !sshConfig.Contains("PasswordAuthentication no");
                audit.SshRootLoginEnabled = !sshConfig.Contains("PermitRootLogin no");
                audit.SshKeyAuthEnabled = sshConfig.Contains("PubkeyAuthentication yes") || !sshConfig.Contains("PubkeyAuthentication no");
                audit.SshX11ForwardingEnabled = sshConfig.Contains("X11Forwarding yes");

                // Parse SSH idle timeout
                var clientAliveMatch = System.Text.RegularExpressions.Regex.Match(sshConfig, @"ClientAliveInterval\s+(\d+)");
                if (clientAliveMatch.Success && int.TryParse(clientAliveMatch.Groups[1].Value, out var interval))
                    audit.SshIdleTimeout = interval;
            }

            // Check firewall
            var fwStatus = await GetFirewallStatusAsync(ct);
            audit.FirewallActive = fwStatus.IsActive;

            // Get firewall rules count
            var fwRules = await ExecuteCommandAsync("sudo iptables -L -n 2>/dev/null | grep -c '^[A-Z]' || echo 0", ct);
            int.TryParse(fwRules.StdOut.Trim(), out var rulesCount);
            audit.FirewallRulesCount = rulesCount;

            // Check IPv6 status
            var ipv6Check = await ExecuteCommandAsync("cat /proc/sys/net/ipv6/conf/all/disable_ipv6 2>/dev/null || echo 0", ct);
            audit.Ipv6Enabled = ipv6Check.StdOut.Trim() == "0";

            // Check SELinux
            var selinuxCheck = await ExecuteCommandAsync("getenforce 2>/dev/null || echo Disabled", ct);
            audit.SeLinuxMode = selinuxCheck.StdOut.Trim();
            audit.SeLinuxEnabled = audit.SeLinuxMode != "Disabled";

            // Check AppArmor
            var apparmorCheck = await ExecuteCommandAsync("aa-status --enabled 2>/dev/null && echo Enabled || echo Disabled", ct);
            audit.AppArmorEnabled = apparmorCheck.StdOut.Contains("Enabled");
            if (audit.AppArmorEnabled)
            {
                var apparmorMode = await ExecuteCommandAsync("aa-status 2>/dev/null | head -1", ct);
                audit.AppArmorMode = apparmorMode.StdOut.Trim();
            }

            // Check Fail2Ban - systemctl is-active returns exit code 0 only when service is active
            var fail2banCheck = await ExecuteCommandAsync("systemctl is-active fail2ban 2>/dev/null", ct);
            audit.Fail2BanActive = fail2banCheck.Success;
            if (audit.Fail2BanActive)
            {
                var jailCount = await ExecuteCommandAsync("sudo fail2ban-client status 2>/dev/null | grep 'Number of jail' | awk '{print $NF}' || echo 0", ct);
                int.TryParse(jailCount.StdOut.Trim(), out var jails);
                audit.Fail2BanJailsActive = jails;
            }

            // Check Auditd
            var auditdCheck = await ExecuteCommandAsync("systemctl is-active auditd 2>/dev/null || echo inactive", ct);
            audit.AuditdActive = auditdCheck.StdOut.Trim() == "active";

            // Get failed login attempts
            var failedLogins = await ExecuteCommandAsync("sudo grep 'Failed password' /var/log/auth.log 2>/dev/null | wc -l || echo 0", ct);
            int.TryParse(failedLogins.StdOut.Trim(), out var failedCount);
            audit.FailedLoginAttempts24h = failedCount;

            // Check users with empty passwords
            var emptyPasswd = await ExecuteCommandAsync("sudo awk -F: '($2 == \"\" ) { print $1 }' /etc/shadow 2>/dev/null | wc -l || echo 0", ct);
            int.TryParse(emptyPasswd.StdOut.Trim(), out var emptyCount);
            audit.UsersWithEmptyPassword = emptyCount;

            // Check NOPASSWD in sudoers
            var sudoNoPasswd = await ExecuteCommandAsync("sudo grep -r 'NOPASSWD' /etc/sudoers /etc/sudoers.d/ 2>/dev/null | grep -v '^#' | awk -F: '{print $2}' | awk '{print $1}'", ct);
            if (sudoNoPasswd.Success && !string.IsNullOrWhiteSpace(sudoNoPasswd.StdOut))
            {
                audit.SudoNoPasswordUsers = sudoNoPasswd.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            }

            // Get password policy settings
            if (File.Exists("/etc/login.defs"))
            {
                var loginDefs = await File.ReadAllTextAsync("/etc/login.defs", ct);
                var maxAgeMatch = System.Text.RegularExpressions.Regex.Match(loginDefs, @"PASS_MAX_DAYS\s+(\d+)");
                if (maxAgeMatch.Success && int.TryParse(maxAgeMatch.Groups[1].Value, out var maxAge))
                    audit.PasswordMaxAgeDays = maxAge;

                var minLenMatch = System.Text.RegularExpressions.Regex.Match(loginDefs, @"PASS_MIN_LEN\s+(\d+)");
                if (minLenMatch.Success && int.TryParse(minLenMatch.Groups[1].Value, out var minLen))
                    audit.PasswordMinLength = minLen;
            }

            // Get listening ports
            var listenPorts = await ExecuteCommandAsync("ss -tlnp 2>/dev/null | grep LISTEN | awk '{print $4}'", ct);
            if (listenPorts.Success)
            {
                audit.ListeningPorts = listenPorts.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                audit.OpenPorts = audit.ListeningPorts.Count;
            }

            // Check unattended upgrades
            audit.UnattendedUpgradesEnabled = File.Exists("/etc/apt/apt.conf.d/50unattended-upgrades");

            // Get last system update time
            var lastUpdate = await ExecuteCommandAsync("stat -c %Y /var/lib/apt/periodic/update-success-stamp 2>/dev/null || echo 0", ct);
            if (long.TryParse(lastUpdate.StdOut.Trim(), out var updateTs) && updateTs > 0)
                audit.LastSystemUpdate = DateTimeOffset.FromUnixTimeSeconds(updateTs).UtcDateTime;

            // Check /tmp mount options
            var tmpMount = await ExecuteCommandAsync("mount | grep ' /tmp ' 2>/dev/null", ct);
            audit.TmpNoExec = tmpMount.StdOut.Contains("noexec");

            // Get umask value
            var umaskCheck = await ExecuteCommandAsync("umask", ct);
            audit.UmaskValue = umaskCheck.StdOut.Trim();

            // Count loaded kernel modules
            var kmodCount = await ExecuteCommandAsync("lsmod 2>/dev/null | wc -l || echo 0", ct);
            int.TryParse(kmodCount.StdOut.Trim(), out var modCount);
            audit.LoadedKernelModules = Math.Max(0, modCount - 1); // Subtract header line

            // Count cron jobs
            var cronCount = await ExecuteCommandAsync("ls /etc/cron.d/ /var/spool/cron/crontabs/ 2>/dev/null | wc -l || echo 0", ct);
            int.TryParse(cronCount.StdOut.Trim(), out var crons);
            audit.CronJobsCount = crons;

            // Check core dumps disabled
            var coreLimit = await ExecuteCommandAsync("cat /proc/sys/kernel/core_pattern 2>/dev/null", ct);
            audit.CoreDumpsDisabled = coreLimit.StdOut.Contains("|/bin/false") || coreLimit.StdOut.Trim() == "";

            // Calculate security score
            audit.SecurityScore = CalculateSecurityScore(audit);

            // Generate recommendations
            audit.Recommendations = GenerateSecurityRecommendations(audit);

            audit.LastSecurityScan = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing security audit");
        }

        return audit;
    }

    private int CalculateSecurityScore(SecurityAuditInfo audit)
    {
        var score = 100;

        // SSH Security (-30 max)
        if (audit.SshPasswordAuthEnabled) score -= 10;
        if (audit.SshRootLoginEnabled) score -= 15;
        if (audit.SshX11ForwardingEnabled) score -= 3;
        if (audit.SshIdleTimeout == 0) score -= 2;

        // Firewall & Network (-20 max)
        if (!audit.FirewallActive) score -= 15;
        if (audit.OpenPorts > 10) score -= 5;

        // Security Frameworks (-15 max)
        if (!audit.SeLinuxEnabled && !audit.AppArmorEnabled) score -= 8;
        if (!audit.Fail2BanActive) score -= 5;
        if (!audit.AuditdActive) score -= 2;

        // Authentication (-20 max)
        if (audit.UsersWithEmptyPassword > 0) score -= 10;
        if (audit.SudoNoPasswordUsers.Count > 0) score -= 5;
        if (audit.FailedLoginAttempts24h > 100) score -= 5;

        // Updates (-10 max)
        if (!audit.UnattendedUpgradesEnabled) score -= 5;
        if (audit.PendingSecurityUpdates > 10) score -= 5;

        // File System (-5 max)
        if (!audit.TmpNoExec) score -= 3;
        if (audit.UmaskValue == "0000" || audit.UmaskValue == "000") score -= 2;

        return Math.Max(0, score);
    }

    private List<SecurityRecommendation> GenerateSecurityRecommendations(SecurityAuditInfo audit)
    {
        var recommendations = new List<SecurityRecommendation>();

        // SSH Recommendations
        if (audit.SshRootLoginEnabled)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "SSH",
                Severity = "critical",
                Title = "Disable SSH root login",
                Description = "Root login via SSH is enabled, which is a security risk.",
                Remediation = "Edit /etc/ssh/sshd_config and set 'PermitRootLogin no', then restart sshd."
            });
        }

        if (audit.SshPasswordAuthEnabled)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "SSH",
                Severity = "high",
                Title = "Disable SSH password authentication",
                Description = "Password authentication is enabled. Use SSH keys instead.",
                Remediation = "Set 'PasswordAuthentication no' in /etc/ssh/sshd_config and use SSH key authentication."
            });
        }

        if (audit.SshX11ForwardingEnabled)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "SSH",
                Severity = "low",
                Title = "Disable X11 forwarding",
                Description = "X11 forwarding is enabled, which can be a security risk if not needed.",
                Remediation = "Set 'X11Forwarding no' in /etc/ssh/sshd_config if GUI forwarding is not required."
            });
        }

        if (audit.SshIdleTimeout == 0)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "SSH",
                Severity = "low",
                Title = "Configure SSH idle timeout",
                Description = "No SSH idle timeout configured. Idle sessions stay open indefinitely.",
                Remediation = "Set 'ClientAliveInterval 300' and 'ClientAliveCountMax 2' in /etc/ssh/sshd_config."
            });
        }

        // Firewall Recommendations
        if (!audit.FirewallActive)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Firewall",
                Severity = "critical",
                Title = "Enable firewall",
                Description = "No active firewall detected. Your system is exposed to network attacks.",
                Remediation = "Install and enable ufw: 'sudo apt install ufw && sudo ufw enable'"
            });
        }

        if (audit.OpenPorts > 10)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Network",
                Severity = "medium",
                Title = "Review open ports",
                Description = $"System has {audit.OpenPorts} open ports. Review if all are necessary.",
                Remediation = "Run 'ss -tlnp' to review listening ports and close unnecessary services."
            });
        }

        // Security Frameworks
        if (!audit.SeLinuxEnabled && !audit.AppArmorEnabled)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Security Framework",
                Severity = "high",
                Title = "Enable mandatory access control",
                Description = "Neither SELinux nor AppArmor is enabled. System lacks MAC protection.",
                Remediation = "Enable AppArmor: 'sudo apt install apparmor apparmor-utils && sudo systemctl enable apparmor'"
            });
        }

        if (!audit.Fail2BanActive)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Intrusion Prevention",
                Severity = "high",
                Title = "Install and enable Fail2Ban",
                Description = "Fail2Ban is not active. System is vulnerable to brute-force attacks.",
                Remediation = "Install Fail2Ban: 'sudo apt install fail2ban && sudo systemctl enable fail2ban --now'"
            });
        }

        if (!audit.AuditdActive)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Auditing",
                Severity = "medium",
                Title = "Enable system auditing",
                Description = "Auditd is not running. Security events are not being logged.",
                Remediation = "Install auditd: 'sudo apt install auditd && sudo systemctl enable auditd --now'"
            });
        }

        // Authentication
        if (audit.UsersWithEmptyPassword > 0)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Authentication",
                Severity = "critical",
                Title = "Remove empty passwords",
                Description = $"{audit.UsersWithEmptyPassword} user(s) have empty passwords. This is a critical vulnerability.",
                Remediation = "Check /etc/shadow for users with empty passwords and set proper passwords."
            });
        }

        if (audit.SudoNoPasswordUsers.Count > 0)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Authentication",
                Severity = "high",
                Title = "Review NOPASSWD sudo entries",
                Description = $"{audit.SudoNoPasswordUsers.Count} user(s) can sudo without password.",
                Remediation = "Review /etc/sudoers and remove unnecessary NOPASSWD entries."
            });
        }

        if (audit.FailedLoginAttempts24h > 100)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Authentication",
                Severity = "high",
                Title = "High failed login attempts",
                Description = $"{audit.FailedLoginAttempts24h} failed login attempts in 24h indicates possible brute-force attack.",
                Remediation = "Review /var/log/auth.log for attack sources and consider blocking IPs or enabling Fail2Ban."
            });
        }

        // Updates
        if (!audit.UnattendedUpgradesEnabled)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Updates",
                Severity = "medium",
                Title = "Enable automatic security updates",
                Description = "Automatic security updates are not configured.",
                Remediation = "Install unattended-upgrades: 'sudo apt install unattended-upgrades && sudo dpkg-reconfigure unattended-upgrades'"
            });
        }

        if (audit.PendingSecurityUpdates > 0)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "Updates",
                Severity = audit.PendingSecurityUpdates > 10 ? "high" : "medium",
                Title = "Apply pending security updates",
                Description = $"{audit.PendingSecurityUpdates} security updates are pending installation.",
                Remediation = "Run 'sudo apt update && sudo apt upgrade' to apply pending updates."
            });
        }

        // File System
        if (!audit.TmpNoExec)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "File System",
                Severity = "medium",
                Title = "Mount /tmp with noexec",
                Description = "/tmp is not mounted with noexec. Malicious scripts could be executed from /tmp.",
                Remediation = "Add 'noexec,nosuid,nodev' options to /tmp mount in /etc/fstab."
            });
        }

        if (!audit.CoreDumpsDisabled)
        {
            recommendations.Add(new SecurityRecommendation
            {
                Category = "System Hardening",
                Severity = "low",
                Title = "Disable core dumps",
                Description = "Core dumps are enabled. They may contain sensitive data.",
                Remediation = "Add 'kernel.core_pattern=|/bin/false' to /etc/sysctl.conf and run 'sysctl -p'."
            });
        }

        return recommendations;
    }

    #endregion

    #region Remote Command Execution

    public async Task<CommandExecutionResponse> ExecuteRemoteCommandAsync(CommandExecutionRequest request, CancellationToken ct = default)
    {
        var response = new CommandExecutionResponse
        {
            WorkerId = _workerId,
            Command = request.Command,
            ExecutedAt = DateTime.UtcNow
        };

        // Security: Block dangerous commands
        var blockedPatterns = new[] { "rm -rf /", "mkfs", "dd if=", ":(){:|:&};:", "chmod -R 777 /" };
        if (blockedPatterns.Any(p => request.Command.Contains(p)))
        {
            response.Error = "Command blocked for security reasons";
            return response;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var command = request.RunAsRoot ? $"sudo {request.Command}" : request.Command;
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            var result = await ExecuteCommandAsync(command, cts.Token, request.WorkingDirectory);
            sw.Stop();

            response.ExitCode = result.ExitCode;
            response.StdOut = result.StdOut;
            response.StdErr = result.StdErr;
            response.Success = result.Success;
            response.ExecutionTimeMs = sw.ElapsedMilliseconds;
        }
        catch (OperationCanceledException)
        {
            response.Error = $"Command timed out after {request.TimeoutSeconds} seconds";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command");
            response.Error = ex.Message;
        }

        return response;
    }

    #endregion

    #region File Browser

    public async Task<DirectoryListResponse> ListDirectoryAsync(DirectoryListRequest request, CancellationToken ct = default)
    {
        var response = new DirectoryListResponse
        {
            WorkerId = _workerId,
            CurrentPath = request.Path
        };

        try
        {
            if (!Directory.Exists(request.Path))
            {
                response.Error = "Directory not found";
                return response;
            }

            var dirInfo = new DirectoryInfo(request.Path);
            response.ParentPath = dirInfo.Parent?.FullName ?? "/";

            var entries = new List<FileSystemEntry>();

            // Get directories
            foreach (var dir in dirInfo.GetDirectories())
            {
                if (!request.IncludeHidden && dir.Name.StartsWith(".")) continue;

                entries.Add(new FileSystemEntry
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    Type = "directory",
                    ModifiedAt = dir.LastWriteTimeUtc,
                    CreatedAt = dir.CreationTimeUtc,
                    Permissions = GetPermissions(dir.FullName)
                });
            }

            // Get files
            foreach (var file in dirInfo.GetFiles())
            {
                if (!request.IncludeHidden && file.Name.StartsWith(".")) continue;

                entries.Add(new FileSystemEntry
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    Type = "file",
                    Size = file.Length,
                    ModifiedAt = file.LastWriteTimeUtc,
                    CreatedAt = file.CreationTimeUtc,
                    Permissions = GetPermissions(file.FullName)
                });
            }

            response.Entries = entries.OrderBy(e => e.Type).ThenBy(e => e.Name).ToList();
            response.DirectoryCount = entries.Count(e => e.Type == "directory");
            response.FileCount = entries.Count(e => e.Type == "file");
            response.TotalSize = entries.Where(e => e.Type == "file").Sum(e => e.Size);
            response.Success = true;
        }
        catch (UnauthorizedAccessException)
        {
            response.Error = "Access denied";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing directory {Path}", request.Path);
            response.Error = ex.Message;
        }

        return response;
    }

    public async Task<FileContentResponse> ReadFileAsync(FileContentRequest request, CancellationToken ct = default)
    {
        var response = new FileContentResponse
        {
            WorkerId = _workerId,
            FilePath = request.FilePath
        };

        try
        {
            if (!File.Exists(request.FilePath))
            {
                response.Error = "File not found";
                return response;
            }

            var fileInfo = new FileInfo(request.FilePath);
            response.FileSize = fileInfo.Length;

            // Check if binary
            if (IsBinaryFile(request.FilePath))
            {
                response.IsBinary = true;
                response.MimeType = "application/octet-stream";
                response.Content = $"[Binary file, {FormatFileSize(fileInfo.Length)}]";
                response.Success = true;
                return response;
            }

            if (request.MaxLines.HasValue)
            {
                var lines = await File.ReadAllLinesAsync(request.FilePath, ct);
                var selectedLines = request.FromEnd
                    ? lines.TakeLast(request.MaxLines.Value)
                    : lines.Take(request.MaxLines.Value);
                response.Content = string.Join("\n", selectedLines);
            }
            else
            {
                response.Content = await File.ReadAllTextAsync(request.FilePath, ct);
            }

            response.MimeType = GetMimeType(request.FilePath);
            response.Success = true;
        }
        catch (UnauthorizedAccessException)
        {
            response.Error = "Access denied";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file {Path}", request.FilePath);
            response.Error = ex.Message;
        }

        return response;
    }

    #endregion

    #region Cron Job Management

    public async Task<CronJobListResponse> GetCronJobsAsync(CancellationToken ct = default)
    {
        var response = new CronJobListResponse { WorkerId = _workerId };

        if (!_isLinux)
        {
            response.Error = "Cron job management is only available on Linux systems";
            return response;
        }

        try
        {
            // Get system crontab
            var systemCron = await ExecuteCommandAsync("sudo cat /etc/crontab 2>/dev/null || true", ct);
            if (systemCron.Success)
            {
                ParseCronContent(systemCron.StdOut, "root", response.Jobs);
            }

            // Get user crontabs
            var users = await ExecuteCommandAsync("cut -d: -f1 /etc/passwd", ct);
            foreach (var user in users.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var userCron = await ExecuteCommandAsync($"sudo crontab -l -u {user} 2>/dev/null || true", ct);
                if (userCron.Success && !string.IsNullOrWhiteSpace(userCron.StdOut))
                {
                    ParseCronContent(userCron.StdOut, user, response.Jobs);
                }
            }

            response.TotalCount = response.Jobs.Count;
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cron jobs");
            response.Error = ex.Message;
        }

        return response;
    }

    private void ParseCronContent(string content, string user, List<CronJobInfo> jobs)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

            var match = Regex.Match(line.Trim(), @"^(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(.+)$");
            if (match.Success)
            {
                var schedule = $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value} {match.Groups[4].Value} {match.Groups[5].Value}";
                var command = match.Groups[6].Value;

                jobs.Add(new CronJobInfo
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Schedule = schedule,
                    Command = command,
                    User = user,
                    IsEnabled = !line.TrimStart().StartsWith("#")
                });
            }
        }
    }

    #endregion

    #region Helper Methods

    private async Task<CommandExecutionResponse> ExecuteCommandAsync(string command, CancellationToken ct, string? workingDirectory = null)
    {
        var response = new CommandExecutionResponse { Command = command };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                response.Error = "Failed to start process";
                return response;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            response.StdOut = stdout;
            response.StdErr = stderr;
            response.ExitCode = process.ExitCode;
            response.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            response.Error = ex.Message;
        }

        return response;
    }

    private static double ParsePercent(string value)
    {
        var clean = value.Replace("%", "").Trim();
        return double.TryParse(clean, out var result) ? result : 0;
    }

    private static string GetPermissions(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "";

        try
        {
            var info = new FileInfo(path);
            var mode = info.UnixFileMode;
            return Convert.ToString((int)mode, 8);
        }
        catch
        {
            return "";
        }
    }

    private static bool IsBinaryFile(string path)
    {
        try
        {
            var buffer = new byte[8192];
            using var fs = File.OpenRead(path);
            var bytesRead = fs.Read(buffer, 0, buffer.Length);
            return buffer.Take(bytesRead).Any(b => b == 0);
        }
        catch
        {
            return false;
        }
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "text/yaml",
            ".log" => "text/plain",
            ".conf" or ".cfg" or ".ini" => "text/plain",
            ".sh" => "application/x-sh",
            ".py" => "text/x-python",
            ".js" => "application/javascript",
            ".html" => "text/html",
            ".css" => "text/css",
            ".md" => "text/markdown",
            _ => "text/plain"
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    #endregion
}
