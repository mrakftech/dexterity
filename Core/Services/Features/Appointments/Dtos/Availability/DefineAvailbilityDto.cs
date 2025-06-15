namespace Services.Features.Appointments.Dtos.Availability;

public class DefineAvailbilityDto
{
    public string Name { get; set; }
    public int SlotInterval { get; set; }
    public TimeSpan StartHour { get; set; } = new TimeSpan(9, 0, 0);
    public TimeSpan EndHour { get; set; } = new TimeSpan(17, 0, 0);
    public int[] WorkingDays { get; set; }

    public List<GetAppointmentDto> Exceptoins { get; set; }
}