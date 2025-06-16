using AutoMapper;
using Domain.Entities.Appointments;
using Services.Features.Appointments.Dtos;
using Services.Features.Appointments.Dtos.Availability;

namespace Services.Features.Appointments.Mapping;

public class AppointmentProfile : Profile
{
    public AppointmentProfile()
    {
        CreateMap<Appointment, GetAppointmentDto>()
            .ForMember(x => x.PatientName, c => c.MapFrom(m => m.Patient.FullName))
            .ForMember(x => x.DoctorName, c => c.MapFrom(m => m.Hcp.FullName))
            .ReverseMap();

        CreateMap<Appointment, UpsertAppointmentDto>()
            .ForMember(x => x.PatientName, c => c.MapFrom(m => m.Patient.FullName))
            .ForMember(x => x.DoctorName, c => c.MapFrom(m => m.Hcp.FullName))
            .ReverseMap();

        CreateMap<Appointment, SearchAppointmentDto>()
            .ForMember(x => x.PatientName, c => c.MapFrom(m => m.Patient.FullName))
            .ForMember(x => x.DateOfBirth, c => c.MapFrom(m => m.Patient.DateOfBirth))
            .ReverseMap();

        CreateMap<Appointment, AppointmentHistoryDto>()
            .ForMember(x => x.PatientName, c => c.MapFrom(m => m.Patient.FullName))
            .ForMember(x => x.Hcp, c => c.MapFrom(m => m.Hcp.FullName))
            .ForMember(x => x.Type, c => c.MapFrom(m => m.AppointmentType.Name))
            .ReverseMap();

        CreateMap<AppointmentSlotDto, AppointmentSlot>()
            .ReverseMap();

        CreateMap<AvailabilityException, AvailabilityExceptionDto>()
            .ReverseMap();
    }
}