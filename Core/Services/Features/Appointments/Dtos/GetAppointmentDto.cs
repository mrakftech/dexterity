
using System.ComponentModel.DataAnnotations;
using Services.State;
using Shared.Attributes;

namespace Services.Features.Appointments.Dtos;

public class GetAppointmentDto 
{
    public Guid Id { get; set; }
    public string Subject { get; set; }
    public DateTime StartTime { get; set; } = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Today.Day, 9, 00, 0);
    public DateTime EndTime { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsReadonly { get; set; }
    public bool IsBlock { get; set; }
    public bool IsSeries { get; set; }
    public Guid CustomRecurrenceId { get; set; }

    public string RecurrenceRule { get; set; }
    public string RecurrenceException { get; set; }
    public int? RecurrenceID { get; set; }
    public string Status { get; set; }
    public int Duration { get; set; }
    public int CancelReasonId { get; set; }
   
    [NotEmpty(ErrorMessage = "Please select type")]
    public Guid AppointmentTypeId { get; set; }

    public int ClinicId { get; set; } = ApplicationState.GetSelectedClinicId();

    [NotEmpty(ErrorMessage = "Please select site")]
    public Guid ClinicSiteId { get; set; }
    
    [NotEmpty]
    public Guid PatientId { get; set; }
    
    [NotEmpty]
    public Guid HcpId { get; set; }

    public string Type { get; set; }
    public string PatientName { get; set; }
    public string DoctorName { get; set; }
}
public class UpsertAppointmentDto 
{
    public Guid Id { get; set; }
    public string Subject { get; set; }
    public DateTime StartTime { get; set; } = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Today.Day, 9, 00, 0);
    public DateTime EndTime { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public bool IsSeries { get; set; }
    public Guid CustomRecurrenceId { get; set; }

    public string RecurrenceRule { get; set; }
    public string RecurrenceException { get; set; }
    public int? RecurrenceID { get; set; }
    public string Status { get; set; }
    public int Duration { get; set; }
   
    [NotEmpty(ErrorMessage = "Please select type")]
    public Guid AppointmentTypeId { get; set; }

    public int ClinicId { get; set; } = ApplicationState.GetSelectedClinicId();

    [NotEmpty(ErrorMessage = "Please select site")]
    public Guid ClinicSiteId { get; set; }
    
    [NotEmpty]
    public Guid PatientId { get; set; }
    
    [NotEmpty]
    public Guid HcpId { get; set; }

    public string PatientName { get; set; }
    public string DoctorName { get; set; }
}
public class AppointmentCardDto 
{
    public DateTime StartTime { get; set; } = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Today.Day, 9, 00, 0);
    public string Location { get; set; }
    public string PatientName { get; set; }
    public string DoctorName { get; set; }
}