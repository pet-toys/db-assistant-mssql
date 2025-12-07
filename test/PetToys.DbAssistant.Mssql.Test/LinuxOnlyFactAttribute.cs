using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace PetToys.DbAssistant.Mssql.Test;

public sealed class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = "This test is only run on Linux.";
        }
    }
}
