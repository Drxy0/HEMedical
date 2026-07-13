using HEMedical.Shared.Models;
using System.Text;

namespace HEMedical.Shared.Http;

/// <summary>
/// Builds the query strings for the statistics endpoints. The Client→HEServer and
/// HEServer→Proxy hops use the same parameter shape, so both HTTP clients share this.
/// </summary>
public static class StatisticsQueryString
{
    public static string ByDate(string path, string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        var url = new StringBuilder($"{path}?loincCode={Uri.EscapeDataString(loincCode)}");
        if (startDate.HasValue)
            url.Append($"&startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            url.Append($"&endDate={endDate.Value:yyyy-MM-dd}");
        AppendCommon(url, componentLoincCode, sex, threshold, includeStandardDeviation);
        return url.ToString();
    }

    public static string ByAge(string path, string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation = false)
    {
        var url = new StringBuilder($"{path}?loincCode={Uri.EscapeDataString(loincCode)}&startAge={startAge}&endAge={endAge}");
        AppendCommon(url, componentLoincCode, sex, threshold, includeStandardDeviation);
        return url.ToString();
    }

    public static string HistogramByDate(string path, string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        var url = new StringBuilder(ByDate(path, loincCode, componentLoincCode, startDate, endDate, sex, null, false));
        AppendBins(url, binStart, binWidth, binCount);
        return url.ToString();
    }

    public static string HistogramByAge(string path, string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        var url = new StringBuilder(ByAge(path, loincCode, componentLoincCode, startAge, endAge, sex, null, false));
        AppendBins(url, binStart, binWidth, binCount);
        return url.ToString();
    }

    private static void AppendCommon(StringBuilder url, string? componentLoincCode, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        if (componentLoincCode is not null)
            url.Append($"&componentLoincCode={Uri.EscapeDataString(componentLoincCode)}");
        if (sex.HasValue)
            url.Append($"&sex={sex.Value}");
        if (threshold.HasValue)
            url.Append($"&threshold={threshold.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        // The standard deviation is opt-in (off by default), so it is only sent when requested;
        // an absent parameter means false at every receiver.
        if (includeStandardDeviation)
            url.Append("&includeStandardDeviation=true");
    }

    // The bin layout travels with the query so every hospital bins identically —
    // slot b at one hospital must mean the same value range as slot b at another.
    private static void AppendBins(StringBuilder url, double binStart, double binWidth, int binCount)
    {
        url.Append($"&binStart={binStart.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        url.Append($"&binWidth={binWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        url.Append($"&binCount={binCount}");
    }
}
