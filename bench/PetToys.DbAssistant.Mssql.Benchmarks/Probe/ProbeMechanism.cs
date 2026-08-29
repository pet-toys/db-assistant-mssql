namespace PetToys.DbAssistant.Mssql.Benchmarks.Probe;

/// <summary>
/// The two ways of getting a materialised collection into SQL Server that a caller actually chooses
/// between.
/// </summary>
/// <remarks>
/// The benchmark project compares four, because two of them are about where the cost of a
/// hand-written reader goes. The probe compares two, because the question here is not what the
/// mapping costs but whether a second materialisation of the caller's data fits alongside the first.
/// The reflective and hand-written readers would answer that identically to the mapped context: they
/// hold one row.
/// </remarks>
internal enum ProbeMechanism
{
    /// <summary>Every row materialised into a <c>DataTable</c>, which is then copied.</summary>
    DataTable,

    /// <summary>The same rows copied through this library's mapped bulk context.</summary>
    MappedBulkContext,
}
