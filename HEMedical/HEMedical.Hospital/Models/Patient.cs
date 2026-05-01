namespace HEMedical.Hospital.Models;

public class Patient
{
    public int Id { get; set; }
    public string PseudoId { get; set; } = string.Empty; // TODO: using field keyword
                                                         // somehow read hospitalId from appsettings.json and combine it in the set portion
    public Sex Sex { get; set; }
    public DateOnly BirthDate { get; set; }
}
