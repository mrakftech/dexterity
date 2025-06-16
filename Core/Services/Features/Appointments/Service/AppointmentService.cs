using AutoMapper;
using Database;
using Domain.Entities.Appointments;
using Microsoft.EntityFrameworkCore;
using Services.Extensions;
using Services.Features.Appointments.Dtos;
using Services.Features.Appointments.Dtos.Availability;
using Services.Features.Appointments.Dtos.Slot;
using Services.State;
using Shared.Constants.Module;
using Shared.Wrapper;

namespace Services.Features.Appointments.Service;

public class AppointmentService(ApplicationDbContext context, IMapper mapper) : IAppointmentService
{
    #region Appoitment

    public async Task<List<GetAppointmentDto>> GetAllAppointments(DateTime startDate, DateTime endDate)
    {
        var exceptions = await GetExceptionAppointments(ApplicationState.Auth.CurrentUser.UserId);
        var data = await context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Hcp)
            .Include(x => x.AppointmentType)
            .Where(evt => evt.StartTime >= startDate
                          && evt.EndTime <= endDate
                          || evt.RecurrenceRule != null && evt.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId
            )
            .AsNoTracking()
            .ToListAsync();

        var mappedData = mapper.Map<List<GetAppointmentDto>>(data);
        mappedData.AddRange(exceptions);
        return mappedData;
    }

    public async Task<List<GetAppointmentDto>> GetAllAppointmentsByHcp(Guid hcpId)
    {
        var exceptions = await GetExceptionAppointments(hcpId);
        var data = await context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Hcp)
            .Include(x => x.AppointmentType)
            .Where(evt =>
                evt.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId
                && evt.HcpId == hcpId
            )
            .AsNoTracking()
            .ToListAsync();

        var mappedData = mapper.Map<List<GetAppointmentDto>>(data);
        mappedData.AddRange(exceptions);
        return mappedData;
    }

    public async Task<IResult<GetAppointmentDto>> GetAppointment(Guid id)
    {
        var appointment = await context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Hcp)
            .Include(x => x.AppointmentType)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (appointment == null)
            return await Result<GetAppointmentDto>.FailAsync("Appointment not found");
        var data = mapper.Map<GetAppointmentDto>(appointment);
        return await Result<GetAppointmentDto>.SuccessAsync(data);
    }

    public async Task<IResult<UpsertAppointmentDto>> GetEditAppointment(Guid id)
    {
        var appointment = await context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Hcp)
            .Include(x => x.AppointmentType)
            .FirstOrDefaultAsync(x => x.Id == id);
        
        if (appointment == null)
            return await Result<UpsertAppointmentDto>.FailAsync("Appointment not found");
        
        var data = mapper.Map<UpsertAppointmentDto>(appointment);
        return await Result<UpsertAppointmentDto>.SuccessAsync(data);
    }

    public async Task<List<SearchAppointmentDto>> FindAppointments()
    {
        var data = await context.Appointments
            .Include(x => x.Patient)
            .Where(x => x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
            .AsNoTracking()
            .ToListAsync();
        return mapper.Map<List<SearchAppointmentDto>>(data);
    }

    public async Task<List<AppointmentHistoryDto>> GetAppointmentHistory(Guid patientId)
    {
        var list = await context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Hcp)
            .Include(x => x.AppointmentType)
            .Where(x => x.PatientId == patientId
                        && x.StartTime < DateTime.Now)
            .ToListAsync();
        var data = mapper.Map<List<AppointmentHistoryDto>>(list);
        return data;
    }

    public async Task<IResult> SaveAppointment(Guid id, UpsertAppointmentDto request)
    {
        try
        {
            if (id == Guid.Empty)
            {
                
                if (await IsSlotAvaiable(request.StartTime, request.HcpId))
                {
                    return await Result.FailAsync("Slot not avaiable.");
                }

                var appointment = new Appointment
                {
                    Subject = $"{request.PatientName}, {GetAppointmentTypeName(request.AppointmentTypeId)}",
                    CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
                    ClinicSiteId = request.ClinicSiteId,
                    ClinicId = request.ClinicId,
                    CreatedDate = DateTime.Now,
                    StartTime = request.StartTime,
                    EndTime = request.StartTime.AddMinutes(request.Duration),
                    PatientId = request.PatientId,
                    HcpId = request.HcpId,
                    AppointmentTypeId = request.AppointmentTypeId,
                    Duration = request.Duration,
                    Description = request.Description,
                    Status = request.Status,
                    RecurrenceRule = request.RecurrenceRule,
                    RecurrenceException = request.RecurrenceException,
                    RecurrenceID = request.RecurrenceID,
                    Location = GetLocation(request.ClinicId, request.ClinicSiteId),
                };
                appointment.Status = AppointmentConstants.Status.Active;
                await context.Appointments.AddAsync(appointment);
            }
            else
            {
                var appointment = await context.Appointments
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (context.Appointments.Any(x =>
                        x.StartTime == request.StartTime && x.HcpId == request.HcpId && x.Id != appointment.Id))
                {
                    return await Result.FailAsync("Slot already booked.");
                }

                if (appointment == null) return await Result.FailAsync("Appointment not found.");

                appointment.Subject = $"{request.PatientName}, {GetAppointmentTypeName(request.AppointmentTypeId)}";
                appointment.ModifiedBy = ApplicationState.Auth.CurrentUser.UserId;
                appointment.ModifiedDate = DateTime.Now;
                appointment.StartTime = request.StartTime;
                appointment.EndTime = request.StartTime.AddMinutes(request.Duration);
                appointment.PatientId = request.PatientId;
                appointment.HcpId = request.HcpId;
                appointment.AppointmentTypeId = request.AppointmentTypeId;
                appointment.Duration = request.Duration;
                appointment.Description = request.Description;
                appointment.Status = AppointmentConstants.Status.Active;
                appointment.RecurrenceRule = request.RecurrenceRule;
                appointment.RecurrenceException = request.RecurrenceException;
                appointment.RecurrenceID = request.RecurrenceID;
                appointment.ClinicSiteId = request.ClinicSiteId;
                appointment.ClinicId = request.ClinicId;
                appointment.Location = GetLocation(request.ClinicId, request.ClinicSiteId);
                context.Appointments.Update(appointment);
                await context.SaveChangesAsync();
            }

            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Appointment Saved");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> DeleteAppointmentSeries(Guid id)
    {
        var appointments =
            await context.Appointments
                .Where(x => x.CustomRecurrenceId == id && x.IsSeries)
                .ToListAsync();

        context.Appointments.RemoveRange(appointments);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Appointment series has been deleted");
    }

    public async Task<IResult> CancelAppointment(Guid appointmentId, int cancelReasonId)
    {
        try
        {
            var appointment = await context.Appointments
                .FirstOrDefaultAsync(x => x.Id == appointmentId);

            if (appointment == null)
                return await Result<GetAppointmentDto>.FailAsync("Appointment not found");

            appointment.Status = AppointmentConstants.Status.Cancelled;
            appointment.CancelReasonId = cancelReasonId;
            appointment.IsReadonly = true;
            context.Appointments.Update(appointment);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Appointment Cancelled");
        }
        catch (Exception e)
        {
            return await Result<GetAppointmentDto>.FailAsync(e.Message);
        }
    }


    public async Task<IResult> CreateAppointment(UpsertAppointmentDto request)
    {
        var appointment = mapper.Map<Appointment>(request);
        appointment.EndTime = request.StartTime.AddMinutes(request.Duration);
        appointment.ClinicId = ApplicationState.Auth.CurrentUser.ClinicId;
        appointment.CreatedBy = ApplicationState.Auth.CurrentUser.UserId;
        appointment.CreatedDate = DateTime.Now;
        appointment.Status = AppointmentConstants.Status.Active;
        context.ChangeTracker.Clear();
        await context.Appointments.AddAsync(appointment);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Appointment Created");
    }

    public async Task UpdateAppointment(UpsertAppointmentDto request)
    {
        var appointment = await context.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.Id);

        if (appointment != null)
        {
            appointment.ModifiedBy = ApplicationState.Auth.CurrentUser.UserId;
            appointment.ModifiedDate = DateTime.Now;
            appointment.StartTime = request.StartTime;
            appointment.EndTime = request.EndTime;
            appointment.HcpId = request.HcpId;
            appointment.Duration = request.Duration;
            appointment.RecurrenceRule = request.RecurrenceRule;
            appointment.RecurrenceException = request.RecurrenceException;
            appointment.RecurrenceID = request.RecurrenceID;
            context.ChangeTracker.Clear();
            context.Appointments.Update(appointment);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAppointment(Guid appointmentId)
    {
        var appointmentInDb = await context.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId);

        if (appointmentInDb != null)
        {
            context.ChangeTracker.Clear();
            context.Appointments.Remove(appointmentInDb);
            await context.SaveChangesAsync();
        }
    }

    #endregion

    #region Recurrence

    private List<Appointment> GetAppointments(Guid hcpId, DateTime startDate, DateTime endDate)
    {
        var appts = context.Appointments
            .Where(a => a.HcpId == hcpId
                        && a.StartTime >= startDate
                        && a.EndTime <= endDate).ToList();
        return appts;
    }


    public async Task<IResult> AddRecurrenceEvents(List<AppointmentSlotDto> appointments,
        GetAppointmentDto getAppointment)
    {
        var recId = Guid.NewGuid();
        try
        {
            var isAllSlotSelected = await context.AppointmentSlots.AsNoTracking()
                .AnyAsync(x => x.IsAvailable == false && x.HcpId == ApplicationState.Auth.CurrentUser.UserId);

            if (isAllSlotSelected)
            {
                return await Result.FailAsync("Please select a slot");
            }

            foreach (var item in appointments)
            {
                var a = new Appointment()
                {
                    StartTime = item.StartTime,
                    EndTime = item.StartTime.AddMinutes(getAppointment.Duration),
                    PatientId = getAppointment.PatientId,
                    HcpId = getAppointment.HcpId,
                    ClinicSiteId = getAppointment.ClinicSiteId,
                    AppointmentTypeId = getAppointment.AppointmentTypeId,
                    ClinicId = ApplicationState.Auth.CurrentUser.ClinicId,
                    Status = AppointmentConstants.Status.Active,
                    Duration = getAppointment.Duration,
                    Location = GetLocation(getAppointment.ClinicId, getAppointment.ClinicSiteId),
                    Subject = $"{getAppointment.PatientName}, {getAppointment.Type}",
                    CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
                    CreatedDate = DateTime.Now,
                    Description = getAppointment.Description,
                    IsSeries = true,
                    CustomRecurrenceId = recId
                };
                await context.Appointments.AddAsync(a);

                await context.SaveChangesAsync();
            }

            return await Result.SuccessAsync("Appointment has been scheduled");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    #endregion

    #region Slots

    public async Task<bool> IsSlotAvaiable(DateTime date, Guid hcpId)
    {
        bool hasAppointment = await context.Appointments
            .AnyAsync(x => x.StartTime == date && x.HcpId == hcpId);

        bool hasAvailabilityException = await context.AvailabilityExceptions
            .AnyAsync(x => x.StartTime <= date
                           && x.EndTime >= date
                           && x.HcpId == hcpId);

        return hasAppointment || hasAvailabilityException;
    }

    public async Task<List<AppointmentSlotDto>> GetAllFreeSlots()
    {
        var slots = await context.AppointmentSlots.OrderBy(x => x.StartTime)
            .Where(x => x.HcpId == ApplicationState.Auth.CurrentUser.UserId).ToListAsync();
        return mapper.Map<List<AppointmentSlotDto>>(slots);
    }

    public async Task<List<AppointmentSlotDto>> GetFreeSlots(DateTime startDate, DateTime endDate, Guid hcpId,
        int duration)
    {
        var hcp = await context.Users.FirstOrDefaultAsync(x => x.Id == hcpId);
        // Get working days and hours for the doctor
        var workingDays = GetWorkingDays(hcp.WorkingDays);

        var freeSlots = new List<AppointmentSlotDto>();

        // Get appointments for the doctor between start and end dates
        var appointments = GetAppointments(hcpId, startDate, endDate);

        // Loop through each day between start and end dates
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Check if the day is a working day
            if (workingDays.Contains(date.DayOfWeek))
            {
                // Get the start and end time for the hour
                var startTime = date;
                var endTime = endDate.ChangeTime(17, 0, 0, 0);
                ;

                // Check if there are any appointments during this hour
                var isFree = true;
                foreach (var appointment in appointments)
                {
                    if (appointment.StartTime < endTime && appointment.EndTime > startTime)
                    {
                        isFree = false;
                        break;
                    }
                }

                // If the hour is free, add it to the list of free slots
                if (isFree)
                {
                    freeSlots.Add(new AppointmentSlotDto()
                        {StartTime = date, EndTime = date.AddMinutes(duration), IsAvailable = true});
                }
            }
        }

        return freeSlots;
    }

    public async Task<IResult> SelectFreeSlot(Guid id, DateTime startDate)
    {
        var slotInDb =
            await context.AppointmentSlots.FirstOrDefaultAsync(x =>
                x.Id == id && x.HcpId == ApplicationState.Auth.CurrentUser.UserId);
        if (slotInDb == null) return await Result.FailAsync("Slot not found");
        slotInDb.StartTime = startDate;
        slotInDb.IsAvailable = true;
        context.ChangeTracker.Clear();
        context.AppointmentSlots.Update(slotInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Appointment Slot Updated");
    }

    public async Task<IResult<List<FindSlotDto>>> FindFreeSlots(DateTime date, Guid hcpId)
    {
        //Getting Boooked Appointment Date and exceptions dates
        var bookedSlots =
            await context.Appointments.Where(x => x.HcpId == hcpId).Select(x => x.StartTime).ToListAsync();
        var exceptions = await GetExceptionAppointments(hcpId);

        foreach (var exception in exceptions)
        {
            var intervalDate = BreakInto15MinIntervals(exception.StartTime, exception.EndTime);
            bookedSlots.AddRange(intervalDate);
        }

        var freeSlots = new List<FindSlotDto>();

        // Define working hours (9 AM to 5 PM)
        var startTime = DateTime.Today.AddHours(AppointmentConstants.StartHour);
        var endTime = DateTime.Today.AddHours(AppointmentConstants.EndHour);

        var tempDate = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0);
        // Start from the current time or the start of working hours, whichever is later
        var currentSlot = tempDate > startTime ? tempDate : startTime;

        // Round up to the nearest 15-minute interval
        if (currentSlot.Minute % AppointmentConstants.AppointmentInterval != 0)
        {
            currentSlot = currentSlot.AddMinutes(AppointmentConstants.AppointmentInterval - (currentSlot.Minute % 15));
        }

        // Iterate through slots in 15-minute intervals
        while (currentSlot < endTime)
        {
            // Check if the slot is not booked
            if (!bookedSlots.Contains(currentSlot))
            {
                freeSlots.Add(new FindSlotDto() {StartDate = currentSlot});
            }

            // Move to the next 15-minute slot
            currentSlot = currentSlot.AddMinutes(AppointmentConstants.AppointmentInterval);
        }

        return await Result<List<FindSlotDto>>.SuccessAsync(freeSlots);
    }

    public async Task<IResult> AddRecurrenceEventsSlot(List<DateTime> recurrencDates, DateTime startDate, Guid hcpId)
    {
        var slots = new List<AppointmentSlotDto>();

        foreach (var item in recurrencDates)
        {
            var time = startDate.TimeOfDay;
            var s = item.Date.Add(time);
            var available = !await IsSlotAvaiable(s, hcpId);
            var evnt = new AppointmentSlotDto()
            {
                StartTime = s,
                IsAvailable = available,
                HcpId = ApplicationState.Auth.CurrentUser.UserId,
            };
            slots.Add(evnt);
        }

        context.ChangeTracker.Clear();
        var slotList = mapper.Map<List<AppointmentSlot>>(slots);
        await context.AppointmentSlots.AddRangeAsync(slotList);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Appointment Slots Added.");
    }

    public async Task<IResult> ClearRecurrenceEventsSlots(Guid hcpId)
    {
        context.ChangeTracker.Clear();
        context.AppointmentSlots.RemoveRange(context.AppointmentSlots.AsNoTracking()
            .Where(x => x.HcpId == hcpId));
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Appointment Slots Cleared");
    }

    #endregion

    #region Availbility

    public async Task<IResult<DefineAvailbilityDto>> GetDefineAvailbility(Guid userId)
    {
        var userInDb = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);
        if (userInDb is null)
        {
            return await Result<DefineAvailbilityDto>.FailAsync("User not found.");
        }

        var exceptions = await GetExceptionAppointments(userId);

        var data = new DefineAvailbilityDto()
        {
            Name = userInDb.FullName,
            WorkingDays = userInDb.WorkingDays,
            StartHour = userInDb.StartHour,
            EndHour = userInDb.EndHour,
            SlotInterval = userInDb.SlotInterval,
            Exceptoins = exceptions
        };
        return await Result<DefineAvailbilityDto>.SuccessAsync(data);
    }

    private async Task<List<GetAppointmentDto>> GetExceptionAppointments(Guid hcpId)
    {
        var exceptions = await context.AvailabilityExceptions.Select(x => new GetAppointmentDto()
        {
            Id = x.Id,
            HcpId = x.HcpId,
            Subject = x.Reason,
            StartTime = x.StartTime,
            EndTime = x.EndTime,
            IsBlock = x.IsBlock,
            Description = x.Subject
        }).Where(x => x.HcpId == hcpId).ToListAsync();
        return exceptions;
    }

    public async Task<IResult> DefineAvailbility(Guid userId, DefineAvailbilityDto request)
    {
        try
        {
            var userInDb = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId);
            if (userInDb is null)
            {
                return await Result.FailAsync("User not found.");
            }

            userInDb.SlotInterval = request.SlotInterval;
            context.ChangeTracker.Clear();
            context.Users.Update(userInDb);
            await context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }

        return await Result.SuccessAsync("Availabilities updated.");
    }

    #region Exceptions

    public async Task<IResult> SaveStandardException(AvailabilityExceptionDto request)
    {
        try
        {
            if (request.Id == Guid.Empty)
            {
                var data = mapper.Map<AvailabilityException>(request);
                data.Id = Guid.NewGuid();
                data.Subject = request.Reason;
                data.IsBlock = request.Type != AppointmentConstants.Availability.Available;
                context.AvailabilityExceptions.Add(data);
            }
            else
            {
                var exception = await context.AvailabilityExceptions.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.Id);
                if (exception == null) return await Result.FailAsync("No exception found");
                exception.HcpId = request.HcpId;
                exception.Type = request.Type;
                exception.IsBlock = request.Type != AppointmentConstants.Availability.Available;
                exception.Reason = request.Reason;
                exception.StartTime = request.StartTime;
                exception.EndTime = request.EndTime;
                exception.Subject = request.Reason;
                context.ChangeTracker.Clear();
                context.AvailabilityExceptions.Update(exception);
            }

            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Availability Exception saved.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> DeleteStandardException(Guid id)
    {
        try
        {
            var exception = await context.AvailabilityExceptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (exception == null) return await Result.FailAsync("No exception found");
            context.AvailabilityExceptions.Remove(exception);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Availability Exception removed.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult<List<AvailabilityExceptionDto>>> GetStandardExceptions()
    {
        var exceptions = await context.AvailabilityExceptions.ToListAsync();
        var data = mapper.Map<List<AvailabilityExceptionDto>>(exceptions);
        return await Result<List<AvailabilityExceptionDto>>.SuccessAsync(data);
    }

    #endregion

    #endregion

    #region Private Methods

    private string GetLocation(int clinicId, Guid clinicSiteId)
    {
        var clinicName = context.Clinics.FirstOrDefault(x => x.Id == clinicId)?.Name;
        var clinicSiteName = context.ClinicSites.FirstOrDefault(x => x.Id == clinicSiteId)?.Name;
        return $"{clinicName} - {clinicSiteName}";
    }

    private string GetAppointmentTypeName(Guid appointmentTypeId)
    {
        var appointmentTypeName =  context.AppointmentTypes.FirstOrDefault(x=>x.Id==appointmentTypeId)?.Name;
        return appointmentTypeName;
    }
    private static List<DateTime> BreakInto15MinIntervals(DateTime startDate, DateTime endDate)
    {
        var intervals = new List<DateTime>();

        while (startDate < endDate)
        {
            DateTime nextInterval = startDate.AddMinutes(15);
            intervals.Add(startDate);
            startDate = nextInterval;
        }

        return intervals;
    }

    private List<DayOfWeek> GetWorkingDays(int[] days)
    {
        var workingDays = new List<DayOfWeek>();

        foreach (var day in days)
        {
            var workingDay = (DayOfWeek) day;
            workingDays.Add(workingDay);
        }

        return workingDays;
    }

    #endregion
}