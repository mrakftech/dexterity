using Domain.Entities.Consultation;
using Domain.Entities.Consultation.Documents;
using Domain.Entities.Consultation.Immunisations;
using Domain.Entities.Consultation.InvestigationDetails;
using Domain.Entities.Settings.Consultation;
using Domain.Entities.Settings.Templates.InvestigationTemplates;
using Services.Features.Consultation.Dto;
using Services.Features.Consultation.Dto.BaselineDetails;
using Services.Features.Consultation.Dto.Immunisations;
using Services.Features.Consultation.Dto.Investigations;
using Services.Features.Consultation.Dto.Letter;
using Services.Features.Consultation.Dto.Notes;
using Services.Features.Consultation.Dto.Reminder;
using Services.Features.Settings.Dtos;
using Shared.Wrapper;

namespace Services.Features.Consultation.Service;

public interface IConsultationService
{
    Task<IResult<Guid>> SaveConsultation(Guid id, SaveConsultationDto request);
    Task<List<GetConsultationDetailDto>> GetConsultationDetailsByPatient(Guid patientId);
    Task<SaveConsultationDto> GetConsultationEditDetail(Guid id);
    Task<IResult> FinishConsultation(Guid id);

    #region Baseline Details

    Task<List<BaselineDetailDto>> GetBaselineDetails();
    Task<IResult<Guid>> SaveBaselineDetail(Guid id, CreateBaselineDetailDto request);
    Task<IResult<Guid>> DeleteBaselineDetail(Guid id);

    #endregion

    #region Notes

    Task<List<HealthCode>> GetHealthCodes();
    Task<List<ConsultationNoteDto>> GetConsultationNotes();
    Task<List<ConsultationNoteDto>> GetActiveDiagonsisByPatient();
    Task<List<ConsultationNoteDto>> GetPastMedicalHistory();
    Task<IResult> UpsertConsultationNote(Guid id, UpsertConsultationNoteDto request);
    Task<UpsertConsultationNoteDto> GetConsultationEditNote(Guid id);
    Task<ConsultationNoteDto> GetConsultationNote(Guid id);
    Task<IResult<int>> DeleteConsultationNote(Guid id);

    #endregion

    #region Immunisations

    Task<IResult> SaveImmunisationSchedule(ImmunisationSchedule request);
    Task<List<ImmunisationScheduleDto>> GetImmunisationSchedule(Guid patientId);
    Task<List<AdministerShot>> GetAdministerShots(Guid scheduleId);
    Task<AdministerShotDto> GetAdministerShot(Guid id);
    Task<IResult> GivenAdministerShot(Guid id, AdministerShotDto request);
    Task<IResult> CancelAdministerShot(Guid id);
    Task<List<AdministerShot>> FilterAdministerShots(Guid scheduleId, string type);
    Task<IResult> SaveReaction(Guid id, Reaction reaction);
    Task<List<Reaction>> GetReactions(Guid administerId);
    Task<IResult> RemoveReaction(Guid id);
    Task<IResult> AddRecurringSchedule(ImmunisationRecurringDto request);

    #endregion

    #region Reminder

    Task<List<GetReminderDto>> GetReminders();
    Task<int> GetActiveRemindersCount();
    Task<IResult> SaveReminders(int id, UpsertReminderDto request);
    Task<IResult> CompleteReminder(int id);
    Task<IResult> DeleteReminder(int id);

    #endregion

    #region Prescriptions

    Task<List<Prescription>> GetPrescriptions(string status);

    #endregion

    #region Investigations

    Task<List<InvestigationGroup>> GetInvestigationGroups();
    Task<List<InvestigationDto>> GetInvestigations(Guid? groupId);
    Task<List<PatientInvestigation>> GetPatientInvestigations();
    Task<IResult> SavePatientInvestigation(PatientInvestigation request);
    Task<IResult> DeletePatientInvestigation(Guid id);
    public Task<List<ResultInvestigationDto>> GetInvestigationResults(Guid patientInvestigationId);
    public Task<List<InvestigationSelectionValue>> GetInvestigationResultSelection(Guid investigationId);
    Task<IResult> SaveInvestigationResult(UpdateResultDto request);
    Task<PatientInvestigation> GetPatientInvestigation(Guid id);

    #endregion

    #region Documents

    #region Letter

    Task<ConsultationLetter> GetLetter(Guid id);
    Task<IResult> SaveLetter(Guid id, LetterDto request);
    Task<IResult> SaveLetterFile(Guid id, string file);
    Task<IResult> ChangeStatus(Guid id, string status);
    Task<List<ConsultationLetter>> GetConsultationLetters();
    Task<IResult> SendEmailLetter(EmailDto request);
    Task<IResult> AddLetterRply(LetterReply letterReply);

    #endregion

    #region Documents

    Task<List<ScannedDocument>> GetScannedDocuments();
    Task<IResult> SaveScannedDocuments(Guid id, ScannedDocument request);

    #endregion

    #region Sketches

    Task<IResult> SavePatientSketch(Guid id, string sketch,Guid sketchCategoryId);
    Task<List<PatientSketch>> GetPatientSketches(Guid sketchCategoryId);

    #endregion

    #region Patient Form

    Task<List<PatientCustomForm>> GetPatientCustomForms();
    Task<PatientCustomForm> GetPatientCustomForm(Guid id);

    Task<IResult> SavePatientCustomForm(Guid id, PatientCustomForm request);

    Task<IResult> DeletePatientCustomForm(Guid id);

    #endregion

    #endregion
}