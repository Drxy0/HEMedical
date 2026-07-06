using HEMedical.Shared.Models;
using System.Text;

namespace HEMedical.Shared.Http;

/// <summary>
/// Builds the query strings for the statistics endpoints. The Client→HEServer and
/// HEServer→Proxy hops use the same parameter shape, so both HTTP clients share this.
/// </summary>
public static class StatisticsQueryString
{
    public static string ByDate(string path, string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var url = new StringBuilder($"{path}?loincCode={Uri.EscapeDataString(loincCode)}");
        if (startDate.HasValue)
            url.Append($"&startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            url.Append($"&endDate={endDate.Value:yyyy-MM-dd}");
        AppendCommon(url, componentLoincCode, sex);
        return url.ToString();
    }

    public static string ByAge(string path, string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex)
    {
        var url = new StringBuilder($"{path}?loincCode={Uri.EscapeDataString(loincCode)}&startAge={startAge}&endAge={endAge}");
        AppendCommon(url, componentLoincCode, sex);
        return url.ToString();
    }

    private static void AppendCommon(StringBuilder url, string? componentLoincCode, PatientSex? sex)
    {
        if (componentLoincCode is not null)
            url.Append($"&componentLoincCode={Uri.EscapeDataString(componentLoincCode)}");
        if (sex.HasValue)
            url.Append($"&sex={sex.Value}");
    }
}
