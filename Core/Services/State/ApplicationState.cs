using Services.Features.Appointments.Dtos;
using Services.Features.Consultation.Dto;
using Services.Features.PatientManagement.Dtos.Details;
using Services.Features.UserAccounts.Dtos.User;

namespace Services.State;

public static class ApplicationState
{
    public static class SelectedAppointment
    {
        public static Guid Id { get; set; }
        public static GetAppointmentDto GetAppointment { get; set; }
    }

    public static class Patient
    {
        public static Guid Id { get; set; } = Guid.Empty;
        public static string Name { get; set; }
        public static PatientSummaryDto Summary { get; set; }
    }

    public static class SelectedConsultation
    {
        public static GetConsultationDetailDto Detail { get; set; }
        public static Guid Id { get; set; }
    }

    public static class Auth
    {
        public static LoginResponseDto CurrentUser { get; set; }
        public static bool IsLoggedIn { get; set; }
        public static Guid SelectedModuleId { get; set; }
        public static List<int> SelectedClinics { get; set; }

        public static void SetClinicIds(List<int> clinicIds)
        {
            SelectedClinics = clinicIds;
        }
    }

    public static class Telehealth
    {
        public static string MeetingName { get; set; }
        public static string MeetingLink { get; set; }
    }


    public static int GetSelectedClinicId()
    {
        return Auth.CurrentUser.ClinicId;
    }
    public static Guid GetUserCurrentId()
    {
        return Auth.CurrentUser.UserId;
    }
    
    
    
    
    public static void SetPatientId(Guid patientId, string name = null)
    {
        Patient.Id = patientId;
        Patient.Name = name ?? string.Empty;
    }
    public static void SetPatient(Guid patientId, PatientSummaryDto summary)
    {
        Patient.Id = patientId;
        Patient.Name = summary.Name;
        Patient.Summary = summary;
    }
    public static Guid GetSelectPatientId()
    {
        return Patient.Id;
    }
    public static string GetSelectPatientName()
    {
        return Patient.Name;
    }
    public static void ClearPatient()
    {
        Patient.Id = Guid.Empty;
        Patient.Name = string.Empty;
        Patient.Summary = new PatientSummaryDto();
    }
}