namespace HEMedical.Client.Helpers;

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
    public static string? Bins(double binWidth, int binCount)
    {
        if (binWidth <= 0)
            return "Bin width must be positive.";
        if (binCount < 1 || binCount > HistogramBins.MaxBinCount)
            return $"Bin count must be between 1 and {HistogramBins.MaxBinCount}.";
        return null;
    }
}
