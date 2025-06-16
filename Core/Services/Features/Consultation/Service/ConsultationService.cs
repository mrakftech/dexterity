using AutoMapper;
using Database;
using Domain.Entities.Consultation;
using Domain.Entities.Consultation.Common;
using Domain.Entities.Consultation.Detail;
using Domain.Entities.Consultation.Documents;
using Domain.Entities.Consultation.Immunisations;
using Domain.Entities.Consultation.InvestigationDetails;
using Domain.Entities.Consultation.Notes;
using Domain.Entities.Settings.Consultation;
using Domain.Entities.Settings.Consultation.Immunisation;
using Domain.Entities.Settings.Templates.InvestigationTemplates;
using Microsoft.EntityFrameworkCore;
using Services.Features.Consultation.Dto;
using Services.Features.Consultation.Dto.BaselineDetails;
using Services.Features.Consultation.Dto.Immunisations;
using Services.Features.Consultation.Dto.Investigations;
using Services.Features.Consultation.Dto.Letter;
using Services.Features.Consultation.Dto.Notes;
using Services.Features.Consultation.Dto.Reminder;
using Services.Features.Messaging.Dtos.Sms;
using Services.Features.Messaging.Mail;
using Services.Features.PatientManagement.Service;
using Services.Features.Settings.Dtos;
using Services.Features.Settings.Service;
using Services.State;
using Shared.Constants.Module.Consultation;
using Shared.Helper;
using Shared.Wrapper;

namespace Services.Features.Consultation.Service;

public class ConsultationService(
    ApplicationDbContext context,
    IMapper mapper,
    IPatientService patientService,
    ISettingService settingService,
    IFileManagerService iFileManagerService,
    IMailService mailService)
    : IConsultationService
{
    #region Consultation Details

    public async Task<List<GetConsultationDetailDto>> GetConsultationDetailsByPatient(Guid patientId)
    {
        var list = new List<GetConsultationDetailDto>();
        var consultations = await context.ConsultationDetails
            .AsNoTracking()
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == patientId)
            .ToListAsync();

        foreach (var item in consultations)
        {
            var activeDiagnosisNotes = await GetActiveDiagonsisByConsultationId(item.Id);
            var data = new GetConsultationDetailDto()
            {
                Id = item.Id,
                Date = item.ConsultationDate.ToString("d"),
                Type = item.ConsultationType,
                IsFinish = item.IsFinished,
                Hcp = item.Hcp.FullName,
                ActiveDiagnosisNotes = activeDiagnosisNotes
            };
            list.Add(data);
        }

        return list;
    }

    public async Task<IResult<Guid>> SaveConsultation(Guid id, SaveConsultationDto request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                var consultation = new ConsultationDetail
                {
                    ConsultationDate = request.ConsultationDate,
                    ClinicSiteId = request.ClinicSiteId,
                    ClinicId = request.ClinicId,
                    ConsultationClass = request.ConsultationClass,
                    PatientId = ApplicationState.GetSelectPatientId(),
                    HcpId = ApplicationState.Auth.CurrentUser.UserId,
                    ConsultationType = request.ConsultationType
                };
                context.ConsultationDetails.Add(consultation);
                await context.SaveChangesAsync();
                return await Result<Guid>.SuccessAsync(message: "Consultation has been added.", data: consultation.Id);
            }
            else
            {
                var consultation = await context.ConsultationDetails.FirstOrDefaultAsync(x => x.Id == id);
                if (consultation is null)
                    return await Result<Guid>.FailAsync("Consultation not found");

                consultation.ConsultationDate = request.ConsultationDate;
                consultation.ConsultationClass = request.ConsultationClass;
                consultation.Pomr = request.Pomr;
                consultation.ConsultationType = request.ConsultationType;
                consultation.ClinicSiteId = request.ClinicSiteId;
                consultation.ClinicId = request.ClinicId;

                context.ConsultationDetails.Update(consultation);
                await context.SaveChangesAsync();
                return await Result<Guid>.SuccessAsync("Consultation has been saved.");
            }
        }
        catch (Exception e)
        {
            return await Result<Guid>.FailAsync(e.Message);
        }
    }

    public async Task<SaveConsultationDto> GetConsultationEditDetail(Guid id)
    {
        var consultation = await context.ConsultationDetails.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (consultation is null)
            return new SaveConsultationDto();

        return new SaveConsultationDto()
        {
            ConsultationDate = consultation.ConsultationDate,
            ClinicSiteId = consultation.ClinicSiteId,
            ClinicId = consultation.ClinicId,
            ConsultationClass = consultation.ConsultationClass,
            ConsultationType = consultation.ConsultationType,
            Pomr = consultation.Pomr,
        };
    }

    public async Task<IResult> FinishConsultation(Guid id)
    {
        var consultation = await context.ConsultationDetails.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (consultation is null)
            return await Result.FailAsync("Consultation not found.");

        consultation.IsFinished = true;
        context.ChangeTracker.Clear();
        context.ConsultationDetails.Update(consultation);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Consultation has been finished.");
    }

    #endregion

    #region Baseline Details

    public async Task<List<BaselineDetailDto>> GetBaselineDetails()
    {
        var baselineDetails = await context.BaselineDetails
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
        var mapped = mapper.Map<List<BaselineDetailDto>>(baselineDetails);
        return mapped;
    }

    public async Task<IResult<Guid>> SaveBaselineDetail(Guid id, CreateBaselineDetailDto request)
    {
        try
        {
            var baselineDetail = mapper.Map<BaselineDetail>(request);
            if (id == Guid.Empty)
            {
                try
                {
                    context.BaselineDetails.Add(baselineDetail);
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    return await Result<Guid>.FailAsync(e.Message);
                }
            }
            else
            {
                var baselineDetailInDb =
                    await context.BaselineDetails.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (baselineDetailInDb is null)
                {
                    return await Result<Guid>.FailAsync("detail not found.");
                }

                context.ChangeTracker.Clear();

                baselineDetailInDb = mapper.Map(request, baselineDetailInDb);
                context.BaselineDetails.Update(baselineDetailInDb);
                await context.SaveChangesAsync();
            }

            return await Result<Guid>.SuccessAsync(request.Id, "Details are saved.");
        }
        catch (Exception e)
        {
            return await Result<Guid>.FailAsync(e.Message);
        }
    }

    public async Task<IResult<Guid>> DeleteBaselineDetail(Guid id)
    {
        var baselineDetailInDb = await context.BaselineDetails.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (baselineDetailInDb is null)
        {
            return await Result<Guid>.FailAsync("detail not found.");
        }

        context.BaselineDetails.Remove(baselineDetailInDb);
        await context.SaveChangesAsync();
        return await Result<Guid>.SuccessAsync("Details are deleted.");
    }

    #endregion

    #region Notes

    public async Task<List<HealthCode>> GetHealthCodes()
    {
        return await context.HealthConditionCodes.ToListAsync();
    }

    public async Task<List<ConsultationNoteDto>> GetConsultationNotes()
    {
        var notes = await context.ConsultationNotes
            .Include(x => x.HealthCode)
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
        var mapped = mapper.Map<List<ConsultationNoteDto>>(notes);
        return mapped;
    }

    public async Task<List<ConsultationNoteDto>> GetActiveDiagonsisByPatient()
    {
        var notes = await context.ConsultationNotes
            .Include(x => x.HealthCode)
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId() &&
                        x.IsActiveCondition)
            .AsNoTracking()
            .ToListAsync();
        var mapped = mapper.Map<List<ConsultationNoteDto>>(notes);
        return mapped;
    }

    private async Task<List<ConsultationNoteDto>> GetActiveDiagonsisByConsultationId(Guid consulataionId)
    {
        var notes = await context.ConsultationNotes
            .Include(x => x.HealthCode)
            .Include(x => x.Hcp)
            .Where(x => x.IsActiveCondition)
            .AsNoTracking()
            .ToListAsync();
        var mapped = mapper.Map<List<ConsultationNoteDto>>(notes);
        return mapped;
    }

    public async Task<List<ConsultationNoteDto>> GetPastMedicalHistory()
    {
        var notes = await context.ConsultationNotes
            .Include(x => x.HealthCode)
            .Include(x => x.Hcp)
            .Where(x =>
                x.PatientId == ApplicationState.GetSelectPatientId() && x.IsPastHistory ||
                x.IsScoialHistory ||
                x.IsFamilyHistory)
            .AsNoTracking()
            .ToListAsync();
        var mapped = mapper.Map<List<ConsultationNoteDto>>(notes);
        return mapped;
    }

    public async Task<IResult> UpsertConsultationNote(Guid id, UpsertConsultationNoteDto request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                var note = mapper.Map<ConsultationNote>(request);
                await context.ConsultationNotes.AddAsync(note);
                await context.SaveChangesAsync();
                return await Result.SuccessAsync("Note has been added.");
            }
            else
            {
                var noteInDb = await context.ConsultationNotes.FirstOrDefaultAsync(x => x.Id == id);
                noteInDb = mapper.Map(request, noteInDb);
                context.ConsultationNotes.Update(noteInDb);
                await context.SaveChangesAsync();
                return await Result.SuccessAsync("Note has been updated.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<ConsultationNoteDto> GetConsultationNote(Guid id)
    {
        var note = await context.ConsultationNotes
            .Include(x => x.HealthCode)
            .FirstOrDefaultAsync(x => x.Id == id);
        return note is null ? new ConsultationNoteDto() : mapper.Map<ConsultationNoteDto>(note);
    }

    public async Task<UpsertConsultationNoteDto> GetConsultationEditNote(Guid id)
    {
        var note = await context.ConsultationNotes
            .FirstOrDefaultAsync(x => x.Id == id);
        return note is null ? new UpsertConsultationNoteDto() : mapper.Map<UpsertConsultationNoteDto>(note);
    }

    public async Task<IResult<int>> DeleteConsultationNote(Guid id)
    {
        var note = await context.ConsultationNotes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (note is null)
        {
            return await Result<int>.FailAsync("detail not found.");
        }

        context.ConsultationNotes.Remove(note);
        await context.SaveChangesAsync();
        return await Result<int>.SuccessAsync("note deleted.");
    }

    #endregion

    #region Reminders

    public async Task<List<GetReminderDto>> GetReminders()
    {
        var reminders = await context.Reminders
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
        var mapped = mapper.Map<List<GetReminderDto>>(reminders);
        return mapped;
    }

    public async Task<int> GetActiveRemindersCount()
    {
        var reminders = await context.Reminders
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId() && x.IsActive)
            .ToListAsync();

        return reminders.Count;
    }

    public async Task<IResult> SaveReminders(int id, UpsertReminderDto request)
    {
        if (id == 0)
        {
            request.IsActive = true;
            var mappedData = mapper.Map<Reminder>(request);
            await context.Reminders.AddAsync(mappedData);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Reminder added.");
        }
        else
        {
            var reminderInDb = await context.Reminders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (reminderInDb is null)
            {
                return await Result<Guid>.FailAsync("reminder not found.");
            }

            reminderInDb = mapper.Map(request, reminderInDb);
            context.Reminders.Update(reminderInDb);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Reminder saved.");
        }
    }

    public async Task<IResult> CompleteReminder(int id)
    {
        var reminderInDb = await context.Reminders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (reminderInDb is null)
        {
            return await Result<Guid>.FailAsync("reminder not found.");
        }

        reminderInDb.IsActive = false;
        context.ChangeTracker.Clear();
        context.Reminders.Update(reminderInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Reminder mark as completed.");
    }

    public async Task<IResult> DeleteReminder(int id)
    {
        var reminderInDb = await context.Reminders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (reminderInDb is null)
        {
            return await Result<Guid>.FailAsync("reminder not found.");
        }

        context.ChangeTracker.Clear();
        context.Reminders.Remove(reminderInDb);
        await context.SaveChangesAsync();
        return await Result<Guid>.SuccessAsync("Reminder is deleted.");
    }

    #endregion

    #region Immunisations

    public async Task<IResult> SaveImmunisationSchedule(ImmunisationSchedule request)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await context.ImmunisationSchedules.AddAsync(request);

            //adding not administered Shot
            var administerShots = await AddAdministerShots(request.ImmunisationProgramId, request.Id);
            await context.AdministerShots.AddRangeAsync(administerShots);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await Result<Guid>.SuccessAsync("Schecdule has been saved.");
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            return await Result<Guid>.FailAsync(e.Message);
        }
    }

    private async Task<List<AdministerShot>> AddAdministerShots(Guid? programId, Guid scheduleId)
    {
        var shotList = new List<Shot>();
        var administerShots = new List<AdministerShot>();

        var program = await context
            .ImmunisationPrograms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == programId);

        var courses = await settingService.GetAssignedCoursesOfProgram(program.Id);
        foreach (var item in courses)
        {
            var shots = await settingService.GetAssignedShotToCourse(item.Id);
            if (shots is not null)
            {
                shotList.AddRange(shots);
            }
        }

        var dueDate = new DateTime();
        foreach (var item in shotList)
        {
            var newAdministerShot = new AdministerShot();
            var shotBatch = await settingService.GetShotBatchByShotId(item.Id);
            if (shotBatch is not null)
            {
                newAdministerShot.ImmunisationScheduleId = scheduleId;
                newAdministerShot.ShotBatchId = shotBatch.Id;
                newAdministerShot.GivenDate = null;
                newAdministerShot.IsDue = true;
            }

            if (item.IntervalType == "From Birth")
            {
                var dob = await patientService.GetPatientDob(ApplicationState.GetSelectPatientId());
                newAdministerShot.DueDate = dob.AddDays(item.IntervalMin);
            }
            else
            {
                newAdministerShot.DueDate = dueDate.AddDays(item.IntervalMin);
            }

            dueDate = newAdministerShot.DueDate;
            administerShots.Add(newAdministerShot);
        }

        return administerShots;
    }

    public async Task<List<ImmunisationScheduleDto>> GetImmunisationSchedule(Guid patientId)
    {
        var list = new List<ImmunisationScheduleDto>();
        var immunisationSchedules = await context.ImmunisationSchedules
            .Include(x => x.ImmunisationProgram)
            .Where(x => x.PatientId == patientId)
            .ToListAsync();
        foreach (var item in immunisationSchedules)
        {
            var courses = await settingService.GetAssignedCoursesOfProgram(item.ImmunisationProgramId!);
            var model = new ImmunisationScheduleDto()
            {
                Id = item.Id,
                ImmunisationProgram = item.ImmunisationProgram,
                ImmunisationProgramId = item.ImmunisationProgramId,
                Courses = courses
            };
            list.Add(model);
        }

        return list;
    }

    public async Task<List<AdministerShot>> GetAdministerShots(Guid scheduleId)
    {
        return await context.AdministerShots
            .Include(x => x.Hcp)
            .Include(x => x.ShotBatch)
            .Include(x => x.ShotBatch.Shot)
            .Include(x => x.ShotBatch.Batch)
            .Where(x => x.ImmunisationScheduleId == scheduleId && x.IsDue && x.IsCancelled == false)
            .OrderByDescending(x => x.DueDate).ToListAsync();
    }

    public async Task<AdministerShotDto> GetAdministerShot(Guid id)
    {
        var administerShot = await context.AdministerShots
            .Include(x => x.ShotBatch)
            .Include(x => x.ShotBatch.Shot)
            .Include(x => x.ShotBatch.Batch)
            .OrderByDescending(x => x.DueDate)
            .FirstOrDefaultAsync(x => x.Id == id);

        var model = new AdministerShotDto
        {
            DueDate = administerShot.DueDate,
            Side = administerShot.Side,
            IsFirstShot = administerShot.IsFirstShot,
        };
        if (administerShot.ShotBatch is not null)
        {
            var shot = administerShot.ShotBatch.Shot;
            var batch = administerShot.ShotBatch.Batch;
            if (shot is not null)
            {
                var batches = await settingService.GetAssignedBatches(shot.Id);
                model.ShotName = administerShot.ShotBatch.Shot.Name;
                model.ShotDose = administerShot.ShotBatch.Shot.Dose;
                model.ShotMethod = administerShot.ShotBatch.Shot.Method;
                model.ShotComment = administerShot.ShotBatch.Shot.Comment;
                model.Batches = batches;
            }

            if (batch is not null)
            {
                model.BatchId = administerShot.ShotBatch.Batch.Id;
                model.BatchNumber = administerShot.ShotBatch.Batch.BatchNumber;
                model.ManfactureName = administerShot.ShotBatch.Batch.ManfactureName;
                model.TradeName = administerShot.ShotBatch.Batch.TradeName;
                model.Remaining = administerShot.ShotBatch.Batch.Remaining;
                model.Expiry = administerShot.ShotBatch.Batch.Expiry;
                model.IsBacthExpired = CheckBatchExpired(model.Expiry);
            }
        }

        return model;
    }

    private static bool CheckBatchExpired(DateTime expiry)
    {
        return DateTime.Now.Date > expiry.Date;
    }

    public async Task<IResult> GivenAdministerShot(Guid id, AdministerShotDto request)
    {
        try
        {
            var administerShotInDb = await context.AdministerShots
                .Include(x => x.ShotBatch)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (administerShotInDb is null)
            {
                return await Result.FailAsync("Administer not found.");
            }

            administerShotInDb.GivenDate = request.GivenDate;
            administerShotInDb.Side = request.Side;
            administerShotInDb.HcpId = request.HcpId;
            administerShotInDb.IsFirstShot = request.IsFirstShot;
            administerShotInDb.IsDue = false;
            administerShotInDb.IsGiven = true;
            administerShotInDb.IsCancelled = false;
            administerShotInDb.PatientId = ApplicationState.GetSelectPatientId();
            context.AdministerShots.Update(administerShotInDb);
            await settingService.DecreaseBatchQty(administerShotInDb.ShotBatch.BatchId, 1);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Administer has been saved.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<List<AdministerShot>> FilterAdministerShots(Guid scheduleId, string type)
    {
        return type switch
        {
            "All" => await context.AdministerShots
                .Include(x => x.Hcp)
                .Include(x => x.ShotBatch)
                .Include(x => x.ShotBatch.Shot)
                .Include(x => x.ShotBatch.Batch).Where(x => x.ImmunisationScheduleId == scheduleId).ToListAsync(),
            "Given" => await context.AdministerShots
                .Include(x => x.Hcp)
                .Include(x => x.ShotBatch)
                .Include(x => x.ShotBatch.Shot)
                .Include(x => x.ShotBatch.Batch).Where(x => x.ImmunisationScheduleId == scheduleId && x.IsGiven)
                .ToListAsync(),
            "Due" => await context.AdministerShots
                .Include(x => x.Hcp)
                .Include(x => x.ShotBatch)
                .Include(x => x.ShotBatch.Shot)
                .Include(x => x.ShotBatch.Batch).Where(x => x.ImmunisationScheduleId == scheduleId && x.IsDue)
                .ToListAsync(),
            "Cancelled" => await context.AdministerShots
                .Where(x => x.ImmunisationScheduleId == scheduleId && x.IsCancelled)
                .Include(x => x.Hcp)
                .Include(x => x.ShotBatch)
                .Include(x => x.ShotBatch.Shot)
                .Include(x => x.ShotBatch.Batch)
                .ToListAsync(),
            _ => await context.AdministerShots
                .Include(x => x.Hcp)
                .Include(x => x.ShotBatch)
                .Include(x => x.ShotBatch.Shot)
                .Include(x => x.ShotBatch.Batch)
                .Where(x => x.ImmunisationScheduleId == scheduleId && x.IsDue)
                .ToListAsync()
        };
    }

    public async Task<IResult> CancelAdministerShot(Guid id)
    {
        var administerShotInDb = await context.AdministerShots.FirstOrDefaultAsync(x => x.Id == id);
        if (administerShotInDb is null)
        {
            return await Result.FailAsync("Administer not found.");
        }

        administerShotInDb.IsCancelled = true;
        administerShotInDb.IsDue = false;
        administerShotInDb.IsGiven = false;
        context.AdministerShots.Update(administerShotInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Administer has been cancelled.");
    }

    public async Task<IResult> SaveReaction(Guid id, Reaction request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                var reaction = new Reaction()
                {
                    Id = Guid.NewGuid(),
                    AdministerShotId = request.AdministerShotId,
                    ReactionType = request.ReactionType,
                    ReactionDate = request.ReactionDate,
                    Comment = request.Comment,
                    Side = request.Side,
                };
                await context.Reactions.AddAsync(reaction);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return await Result.FailAsync(e.Message);
        }

        return await Result.SuccessAsync("Reaction has been saved.");
    }

    public async Task<List<Reaction>> GetReactions(Guid administerId)
    {
        return await context.Reactions
            .Include(x => x.AdministerShot)
            .Include(x => x.AdministerShot.ShotBatch)
            .Include(x => x.AdministerShot.ShotBatch.Shot)
            .Where(x => x.AdministerShotId == administerId).ToListAsync();
    }

    public async Task<IResult> RemoveReaction(Guid id)
    {
        var reaction = await context.Reactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (reaction is null)
        {
            return await Result.FailAsync("reaction not found.");
        }

        context.Reactions.Remove(reaction);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("reaction has been removed.");
    }

    public async Task<IResult> AddRecurringSchedule(ImmunisationRecurringDto request)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            for (var i = 1; i <= request.SpecifiedCount; i++)
            {
                var schedule = new ImmunisationSchedule()
                {
                    Id = Guid.NewGuid(),
                    ScheduleDate = request.StartDate.AddDays(i * request.RecurrenceInterval),
                    ImmunisationProgramId = request.ImmunisationProgramId,
                    PatientId = request.PatientId,
                };
                await context.ImmunisationSchedules.AddAsync(schedule);
                var administerShots = await AddAdministerShots(request.ImmunisationProgramId, schedule.Id);
                await context.AdministerShots.AddRangeAsync(administerShots);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            return await Result.FailAsync(e.Message);
        }

        return await Result.SuccessAsync("Schedule has been added.");
    }

    #endregion

    #region Prescriptions

    public async Task<List<Prescription>> GetPrescriptions(string status)
    {
        return await context.Prescriptions
            .Include(x => x.Drug)
            .Where(x => x.Status == status && x.PatientId == ApplicationState.GetSelectPatientId()).ToListAsync();
    }

    #endregion

    #region Investigations

    public async Task<List<InvestigationGroup>> GetInvestigationGroups()
    {
        return await context.InvestigationGroups.ToListAsync();
    }

    public async Task<List<InvestigationDto>> GetInvestigations(Guid? groupId)
    {
        List<InvestigationDto> investigations;
        if (groupId == Guid.Empty)
        {
            var list = await context.Investigations.ToListAsync();
            investigations = mapper.Map<List<InvestigationDto>>(list);
        }
        else
        {
            var list = await context.AssignedInvestigationsGroup
                .Where(x => x.InvestigationGroupId == groupId)
                .Select(x => x.Investigation)
                .ToListAsync();

            investigations = mapper.Map<List<InvestigationDto>>(list);
        }

        return investigations;
    }

    public async Task<List<PatientInvestigation>> GetPatientInvestigations()
    {
        return await context.PatientInvestigations
            .Include(x => x.Investigation)
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId() &&
                        x.Status != InvestigationStatus.Cancelled)
            .ToListAsync();
    }

    public async Task<IResult> SavePatientInvestigation(PatientInvestigation request)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            if (request.Id == Guid.Empty)
            {
                var patientInvestigation = new PatientInvestigation()
                {
                    Id = Guid.NewGuid(),
                    Status = InvestigationStatus.Awaiting,
                    IsAbnormal = request.IsAbnormal,
                    HcpId = request.HcpId,
                    PatientId = ApplicationState.GetSelectPatientId(),
                    InvestigationId = request.InvestigationId,
                    CreatedDate = DateTime.UtcNow,
                };
                await context.PatientInvestigations.AddAsync(patientInvestigation);

                await AddResults(patientInvestigation.InvestigationId, patientInvestigation.Id);
            }
            else
            {
                var investigationInDb = await context.PatientInvestigations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.Id);
                if (investigationInDb is null)
                {
                    return await Result.FailAsync("Patient investigation not found.");
                }

                //investigationInDb.Date = patientInvestigation.Date;
                //investigationInDb.InvestigationId = patientInvestigation.InvestigationId;
                investigationInDb.Status = request.Status;
                investigationInDb.IsAbnormal = request.IsAbnormal;
                investigationInDb.ModifiedDate = DateTime.UtcNow;
                //investigationInDb.HcpId = patientInvestigation.HcpId;
                context.PatientInvestigations.Update(investigationInDb);

                await AddInvestiogationAudit(request.Status, investigationInDb.Id,
                    investigationInDb.HcpId);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            context.ChangeTracker.Clear();
            return await Result.SuccessAsync("Patient investigation has been saved.");
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            return await Result.FailAsync(e.Message);
        }
    }

    private async Task AddResults(Guid investigationId, Guid patientInvestigationId)
    {
        var results = await GetInvestigationResultDetails(investigationId);
        foreach (var newResult in results.Select(item => new InvestigationResult()
                 {
                     Id = Guid.NewGuid(),
                     PatientInvestigationId = patientInvestigationId,
                     InvestigationDetailId = item.InvestigationDetailId,
                     Result = string.Empty,
                 }))
        {
            context.InvestigationResults.Add(newResult);
        }
    }

    public async Task<IResult> DeletePatientInvestigation(Guid id)
    {
        var investigationInDb = await context.PatientInvestigations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (investigationInDb is null)
        {
            return await Result.FailAsync("Patient investigation not found.");
        }

        context.PatientInvestigations.Remove(investigationInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Patient investigation has been deleted.");
    }

    public async Task<List<InvestigationSelectionValue>> GetInvestigationResultSelection(Guid investigationId)
    {
        var selection = await context.InvestigationDetails
            .Include(x => x.InvestigationSelectionList)
            .ThenInclude(investigationSelectionList =>
                investigationSelectionList.InvestigationSelectionValues)
            .FirstOrDefaultAsync(x => x.InvestigationId == investigationId && x.FieldType == "List Selection");

        return selection == null ? [] : selection.InvestigationSelectionList.InvestigationSelectionValues.ToList();
    }


    public async Task<List<ResultInvestigationDto>> GetInvestigationResults(Guid patientInvestigationId)
    {
        return await context.InvestigationResults
            .Include(x => x.InvestigationDetail)
            .Where(x => x.PatientInvestigationId == patientInvestigationId)
            .Select(x => new ResultInvestigationDto()
            {
                Id = x.Id,
                Name = x.InvestigationDetail.Name,
                Description = x.InvestigationDetail.Description,
                IsActive = x.InvestigationDetail.IsActive,
                IsMaindatory = x.InvestigationDetail.IsMaindatory,
                FieldType = x.InvestigationDetail.FieldType,
                Range = $"{x.InvestigationDetail.NormalMinimum} - {x.InvestigationDetail.NormalMaximum}",
                Unit = x.InvestigationDetail.Unit,
                InvestigationId = x.InvestigationDetail.InvestigationId,
                InvestigationDetailId = x.InvestigationDetail.Id,
                NormalMinimum = x.InvestigationDetail.NormalMinimum,
                NormalMaximum = x.InvestigationDetail.NormalMaximum,
                ResultText = x.Result,
            })
            .ToListAsync();
    }

    public async Task<IResult> SaveInvestigationResult(UpdateResultDto request)
    {
        var result = await context.InvestigationResults.FirstOrDefaultAsync(x => x.Id == request.Id);
        result.Result = GetResultValue(request);
        context.InvestigationResults.Update(result);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("result has been saved.");
    }

    public async Task<PatientInvestigation> GetPatientInvestigation(Guid id)
    {
        return await context.PatientInvestigations
            .Include(x => x.Patient)
            .Include(x => x.InvestigationAudits)
            .Include(x => x.Investigation)
            .FirstOrDefaultAsync(x => x.Id == id);
    }


    private async Task<List<ResultInvestigationDto>> GetInvestigationResultDetails(Guid investigationId)
    {
        var investigations = await context.Investigations
            .Include(x => x.InvestigationDetails)
            .FirstOrDefaultAsync(x => x.Id == investigationId);

        var details = investigations.InvestigationDetails.ToList();
        return mapper.Map<List<ResultInvestigationDto>>(details);
    }

    private async Task AddInvestiogationAudit(string status, Guid patientInvestigationId, Guid hcpId)
    {
        var audit = new InvestigationAudit()
        {
            Id = Guid.NewGuid(),
            Date = DateTime.Now,
            PatientInvestigationId = patientInvestigationId,
            HcpName = ApplicationState.Auth.CurrentUser.Name,
            Status = status,
        };
        await context.InvestigationAudits.AddAsync(audit);
    }


    private static string GetResultValue(UpdateResultDto request)
    {
        return request!.FieldType switch
        {
            "Text" => request.ResultText,
            "Number" => request.ResultNumber.ToString("N0"),
            "Decimal" => request.ResultDecimal.ToString("F"),
            "List Selection" => request.ResultSelectedValue,
            "Date" => request.ResultDate.ToString("g"),
            _ => request.ResultText
        };
    }

    #endregion

    #region Documents

    #region Letter

    public async Task<ConsultationLetter> GetLetter(Guid id)
    {
        return await context.ConsultationLetters
            .AsNoTracking()
            .Include(x => x.LetterTemplate)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IResult> SaveLetter(Guid id, LetterDto request)
    {
        if (id == Guid.Empty)
        {
            id = Guid.NewGuid();
            var addLetter = new ConsultationLetter()
            {
                Id = id,
                LetterDt = request.LetterDt,
                Reference = request.Reference,
                ReferTo = request.ReferTo,
                HcpId = request.HcpId,
                PatientId = ApplicationState.GetSelectPatientId(),
                LetterTemplateId = request.LetterTemplateId,
                LetterTypeId = request.LetterTypeId,
                Description = request.Description,
                Status = LetterConstants.Status.Active,
                PatientFile = await settingService.GetLetterTemplateFile(request.LetterTemplateId),
                PatientFileName = $"{id}.docx"
            };
            await context.ConsultationLetters.AddAsync(addLetter);
            await iFileManagerService.CreateWordFile(addLetter.Id.ToString(), addLetter.PatientFile,
                ConsultationConstants.DocumentConstant.Letters);

            await context.SaveChangesAsync();
        }

        return await Result.SuccessAsync("Letter has been saved.");
    }

    public async Task<IResult> SaveLetterFile(Guid id, string file)
    {
        var letter = await context.ConsultationLetters
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (letter is null)
        {
            return await Result.FailAsync("Letter doesn't exist.");
        }

        letter.PatientFile = file;
        context.ChangeTracker.Clear();
        context.ConsultationLetters.Update(letter);
        await context.SaveChangesAsync();
        await iFileManagerService.CreateWordFile(letter.Id.ToString(), letter.PatientFile,
            ConsultationConstants.DocumentConstant.Letters);
        return await Result.SuccessAsync("Letter file has been saved.");
    }

    public async Task<IResult> ChangeStatus(Guid id, string status)
    {
        var letter = await context.ConsultationLetters
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (letter is null)
        {
            return await Result.FailAsync("Letter doesn't exist.");
        }

        letter.Status = status;
        if (status == LetterConstants.Status.Completed)
        {
            letter.CompletedAt = DateTime.UtcNow;
        }

        context.ChangeTracker.Clear();
        context.ConsultationLetters.Update(letter);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Letter file has been saved.");
    }

    public async Task<List<ConsultationLetter>> GetConsultationLetters()
    {
        return await context.ConsultationLetters
            .Include(x => x.LetterTemplate)
            .Include(x => x.LetterReplies)
            .Include(x => x.LetterTemplate.LetterType)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId() &&
                        x.Status != LetterConstants.Status.Cancelled)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IResult> SendEmailLetter(EmailDto request)
    {
        try
        {
            var req = new MailRequest()
            {
                Subject = request.Subject,
                To = request.To,
                CcList = request.CcList,
                Attachments = request.Attachments,
            };
            await mailService.SendAsync(req, default);
            return await Result.SuccessAsync("Mail has been sent.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> AddLetterRply(LetterReply letterReply)
    {
        try
        {
            await context.LetterReplies.AddAsync(letterReply);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Letter Reply has been saved.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    #endregion

    #region Documents

    public async Task<List<ScannedDocument>> GetScannedDocuments()
    {
        return await context.ScannedDocuments.Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
    }

    public async Task<IResult> SaveScannedDocuments(Guid id, ScannedDocument request)
    {
        if (id == Guid.Empty)
        {
            var newScannedDocument = new ScannedDocument()
            {
                Id = Guid.NewGuid(),
                PatientId = ApplicationState.GetSelectPatientId(),
                AdditionalNotes = request.AdditionalNotes,
                Description = request.Description,
                ScanDate = request.ScanDate,
                Type = request.Type,
                Category = request.Category,
                AttachedFile = request.AttachedFile,
            };
            //Category is folder name in this method
            await iFileManagerService.CreateWordFile(newScannedDocument.Id.ToString(), newScannedDocument.AttachedFile,
                request.Category);
            await context.ScannedDocuments.AddAsync(newScannedDocument);
        }
        else
        {
            var documentInDb = await context.ScannedDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (documentInDb is null)
                return await Result.FailAsync("Document doesn't exist.");

            documentInDb.Category = request.Category;
            documentInDb.Type = request.Type;
            documentInDb.AttachedFile = request.AttachedFile;
            documentInDb.ScanDate = request.ScanDate;
            documentInDb.AdditionalNotes = request.AdditionalNotes;
            documentInDb.Description = request.Description;
            context.ChangeTracker.Clear();
            context.ScannedDocuments.Update(documentInDb);
        }

        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Scanned Document has been saved.");
    }

    public async Task<IResult> SavePatientSketch(Guid id, string sketch, Guid sketchCategoryId)
    {
        if (id == Guid.Empty)
        {
            var patientSketch = new PatientSketch()
            {
                Id = Guid.NewGuid(),
                PatientId = ApplicationState.GetSelectPatientId(),
                Sketch = sketch,
                SketchCategoryId = sketchCategoryId
            };
            await context.PatientSketches.AddAsync(patientSketch);
            await context.SaveChangesAsync();
        }

        return await Result.SuccessAsync("Sketch has been saved.");
    }

    public async Task<List<PatientSketch>> GetPatientSketches(Guid sketchCategoryId)
    {
        return await context.PatientSketches
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId() && x.SketchCategoryId == sketchCategoryId)
            .ToListAsync();
    }

    #endregion

    #region Patietn Form

    public async Task<List<PatientCustomForm>> GetPatientCustomForms()
    {
        return await context.PatientCustomForms
            .Include(x => x.FormTemplate)
            .Include(x => x.Hcp)
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
    }

    public async Task<PatientCustomForm> GetPatientCustomForm(Guid id)
    {
        return await context.PatientCustomForms.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IResult> SavePatientCustomForm(Guid id, PatientCustomForm request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                var newCustomForm = new PatientCustomForm()
                {
                    Id = Guid.NewGuid(),
                    RefNumber = CryptographyHelper.GetUniqueKey(),
                    CreatedDate = DateTime.UtcNow,
                    PatientId = ApplicationState.GetSelectPatientId(),
                    FormTemplateId = request.FormTemplateId,
                    HcpId = request.HcpId,
                    Status = request.Status,
                };
                await context.PatientCustomForms.AddAsync(newCustomForm);
            }

            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Form has been saved.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> DeletePatientCustomForm(Guid id)
    {
        var letter = await context.PatientCustomForms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (letter is null)
        {
            return await Result.FailAsync("Form doesn't exist.");
        }

        context.PatientCustomForms.Remove(letter);
        await context.SaveChangesAsync();
        return await Result.FailAsync("Form has been deleted.");
    }

    #endregion

    #endregion
}