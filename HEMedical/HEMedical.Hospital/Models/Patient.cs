using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Models;

public class Patient
{
    public int Id { get; set; }
    public PatientSex Sex { get; set; }
    public DateOnly BirthDate { get; set; }
}
