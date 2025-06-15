using Domain.Entities.Appointments;
using Services.Features.Appointments.Dtos;
using Services.Features.Appointments.Dtos.Availability;
using Services.Features.Appointments.Dtos.Slot;
using Shared.Wrapper;

namespace Services.Features.Appointments.Service;

public interface IAppointmentService
{
    Task<List<SearchAppointmentDto>> FindAppointments();
    Task<List<AppointmentHistoryDto>> GetAppointmentHistory(Guid patientId);
    Task<List<GetAppointmentDto>> GetAllAppointments(DateTime startDate, DateTime endDate);
    Task<List<GetAppointmentDto>> GetAllAppointmentsByHcp(Guid hcpId);
    Task<IResult<GetAppointmentDto>> GetAppointment(Guid id);
    Task<IResult> CreateAppointment(UpsertAppointmentDto  getAppointment);
    Task UpdateAppointment(UpsertAppointmentDto  getAppointment);
    Task DeleteAppointment(Guid id);
    Task<IResult> DeleteAppointmentSeries(Guid id);
    Task<IResult> CancelAppointment(Guid appointmentId, int cancelReasonId);
    Task<IResult> SaveAppointment(Guid id, UpsertAppointmentDto  getAppointment);

    #region Recurring

    Task<IResult> AddRecurrenceEvents(List<AppointmentSlotDto> appointments, GetAppointmentDto getAppointment);
    Task<IResult> AddRecurrenceEventsSlot(List<DateTime> recurrencDates, DateTime startDate, Guid hcpId);
    Task<IResult> ClearRecurrenceEventsSlots(Guid hcpId);
    Task<IResult> SelectFreeSlot(Guid id, DateTime startDate);
    Task<IResult<List<FindSlotDto>>> FindFreeSlots(DateTime date, Guid hcpId);

    #endregion

    #region Slots

    Task<bool> IsSlotAvaiable(DateTime date, Guid hcpId);
    Task<List<AppointmentSlotDto>> GetAllFreeSlots();
    Task<List<AppointmentSlotDto>> GetFreeSlots(DateTime startDate, DateTime endDate, Guid hcpId, int duration);

    #endregion

    #region Availability

    Task<IResult> DefineAvailbility(Guid userId, DefineAvailbilityDto request);

    Task<IResult<DefineAvailbilityDto>> GetDefineAvailbility(Guid userId);


    #region Exceptions

    Task<IResult> SaveStandardException(AvailabilityExceptionDto request);
    Task<IResult> DeleteStandardException(Guid id);
    Task<IResult<List<AvailabilityExceptionDto>>> GetStandardExceptions();

    #endregion

    #endregion
}