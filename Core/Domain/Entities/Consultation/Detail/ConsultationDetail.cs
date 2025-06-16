using Domain.Contracts;
using Domain.Entities.Consultation.Notes;
using Domain.Entities.PatientManagement;
using Domain.Entities.Settings.Clinic;
using Domain.Entities.UserAccounts;

namespace Domain.Entities.Consultation.Detail;

public class ConsultationDetail : IBaseId
{
    public Guid Id { get; set; }
    public DateTime ConsultationDate { get; set; }
    public string ConsultationType { get; set; }
    public string ConsultationClass { get; set; }
    public string Pomr { get; set; }

    public ClinicSite ClinicSite { get; set; }
    public Guid ClinicSiteId { get; set; }
    
    public int ClinicId { get; set; }

    public User Hcp { get; set; }
    public Guid HcpId { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; }
    public bool IsFinished { get; set; }
}