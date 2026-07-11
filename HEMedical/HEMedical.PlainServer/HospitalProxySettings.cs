namespace HEMedical.PlainServer;

// For statically configuring hospital
// (since HEMedical.PlainServer is test only I think this is enough)
public class HospitalProxySettings
{
    public List<string> Urls { get; set; } = [];
}