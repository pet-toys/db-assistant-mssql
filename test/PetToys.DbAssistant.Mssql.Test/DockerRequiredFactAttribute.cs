using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test when a Docker engine is not
/// reachable, instead of proxying on the operating system. The probe runs once
/// per test session and is cached.
/// </summary>
public sealed class DockerRequiredFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(ProbeDocker);

    public DockerRequiredFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!DockerAvailable.Value)
        {
            Skip = "Docker engine is not available on this host.";
        }
    }

    private static bool ProbeDocker()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
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

            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }
}
