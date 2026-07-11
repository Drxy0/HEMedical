namespace HEMedical.Client.Services;

/// <summary>
/// Range checks for statistics-query inputs, kept in the service layer so both the HE and
/// the plaintext path enforce the same rules from one place (rather than each controller
/// repeating attributes). Each method returns an error message when the input is invalid,
/// or null when it is acceptable; callers turn a non-null message into a failed result with
/// <see cref="HEMedical.Shared.Common.ErrorKind.InvalidInput"/>, which the web layer maps to 400.
/// </summary>
internal static class QueryValidation
{
    public const int MinAge = 0;
    public const int MaxAge = 150;

    /// <summary>An age range must sit within [0, 150] and not run backwards.</summary>
    public static string? AgeRange(int startAge, int endAge)
    {
        if (startAge < MinAge || endAge > MaxAge)
            return $"Age must be between {MinAge} and {MaxAge}.";
        if (endAge < startAge)
            return "End age must not be less than start age.";
        return null;
    }

    /// <summary>Bin width must be positive and the bin count within [1, MaxBinCount].</summary>
    public static string? Bins(decimal binWidth, int binCount)
    {
        if (binWidth <= 0)
            return "Bin width must be positive.";
        if (binCount < 1 || binCount > HistogramBins.MaxBinCount)
            return $"Bin count must be between 1 and {HistogramBins.MaxBinCount}.";
        return null;
    }
}
