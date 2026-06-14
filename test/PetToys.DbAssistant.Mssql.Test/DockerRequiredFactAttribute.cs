using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test unless a Docker engine
/// capable of running Linux containers is reachable. This gates on the real
/// capability the tests need (the SQL Server image is Linux-only) rather than on
/// the host operating system, so it runs on Linux and on a Windows host whose
/// Docker daemon is in Linux-container mode, and skips where Linux containers are
/// unavailable (a Windows-container daemon, or no daemon at all). The probe runs
/// once per test session and is cached.
/// </summary>
public sealed class DockerRequiredFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> LinuxDockerAvailable = new(ProbeDocker);

    public DockerRequiredFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!LinuxDockerAvailable.Value)
        {
            Skip = "A Linux-container Docker engine is not available on this host.";
        }
    }

    private static bool ProbeDocker()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info --format {{.OSType}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null) return false;
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            var osType = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0
                && osType.Equals("linux", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }
}
