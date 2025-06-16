using Services.State;

namespace Services.Features.Consultation.Dto;

public class SaveConsultationDto
{
    public DateTime ConsultationDate { get; set; } = DateTime.Now;
    public string ConsultationType { get; set; } = "General";
    public string ConsultationClass { get; set; } = "General";
    public string Pomr { get; set; }
    public Guid ClinicSiteId { get; set; }
    public int ClinicId { get; set; } = ApplicationState.GetSelectedClinicId();
    public string PatientName { get; set; } = ApplicationState.Patient.Name;
    public string DoctorName { get; set; } = ApplicationState.Auth.CurrentUser.Name;
}