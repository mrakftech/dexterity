using AutoMapper;
using Database;
using Domain.Entities.PatientManagement;
using Microsoft.EntityFrameworkCore;
using Services.State;
using Shared.Helper;
using Shared.Wrapper;
using Domain.Entities.PatientManagement.Alert;
using Domain.Entities.PatientManagement.Allergies;
using Domain.Entities.PatientManagement.Billing;
using Domain.Entities.PatientManagement.Details;
using Domain.Entities.PatientManagement.Extra;
using Domain.Entities.PatientManagement.Family;
using Domain.Entities.PatientManagement.Group;
using Domain.Entities.PatientManagement.Options;
using Services.Features.PatientManagement.Dtos.Account;
using Services.Features.PatientManagement.Dtos.Alerts;
using Services.Features.PatientManagement.Dtos.Allergies;
using Services.Features.PatientManagement.Dtos.Details;
using Services.Features.PatientManagement.Dtos.Family;
using Services.Features.PatientManagement.Dtos.Grouping;
using Services.Features.PatientManagement.Dtos.Options;
using Services.Features.PatientManagement.Dtos.RelatedHcp;
using Services.Features.Settings.Service;
using Shared.Constants.Module;

namespace Services.Features.PatientManagement.Service;

public class PatientService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IMapper mapper,
    IFileManagerService fileManagerService)
    : IPatientService
{
    #region Patient

    public async Task<List<PatientListDto>> GetPatients()
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            
            var patients = await context.Patients
                .Where(x => x.IsDeleted == false && x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
                .ToListAsync();
            
            return mapper.Map<List<PatientListDto>>(patients);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<List<PatientListDto>> GetPatientsByClinic(int[] clinicIds)
    {
        try
        {
            var patientsList = new List<PatientListDto>();
            await using var context = await contextFactory.CreateDbContextAsync();
            foreach (var clinicId in clinicIds)
            {
                var patients = await context.Patients
                    .Where(x => x.IsDeleted == false && x.ClinicId == clinicId)
                    .ToListAsync();
                patientsList.AddRange(mapper.Map<List<PatientListDto>>(patients)); 
                
            }
            return patientsList;    
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<PatientDto> GetPatient(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var patientInDb = await context.Patients.FirstOrDefaultAsync(x => x.Id == id);
        var data = mapper.Map<PatientDto>(patientInDb);
        return data;
    }

    public async Task<DateTime> GetPatientDob(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var patientInDb = await context.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return patientInDb.DateOfBirth;
    }

    public async Task<IResult> QuickCreatePatient(QuickAddPatientDto request, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var patient = mapper.Map<Patient>(request);
            patient.Id = Guid.NewGuid();
            patient.MedicalRecordNumber = CryptographyHelper.GenerateMrNumber();
            patient.UniqueNumber = CryptographyHelper.GetUniqueKey(8);
            patient.MobilePhone = DexHelperMethod.GetMobileFormat(request.Mobile);
            patient.Address.AddressLine1 = request.AddressLine1;
            patient.CreatedBy = ApplicationState.Auth.CurrentUser.UserId;
            patient.ClinicId = ApplicationState.Auth.CurrentUser.ClinicId;
            patient.RegistrationDate = DateTime.Now;
            patient.Gender = request.Gender.ToString();
            await context.Patients.AddAsync(patient, cancellationToken);
            var rowsUpdated = await context.SaveChangesAsync(cancellationToken);
            if (rowsUpdated > 0)
            {
                fileManagerService.CreatePatientDefualtDirectories(patient.Id);
                return await Result.SuccessAsync("Patient has been saved.");
            }

            return await Result.FailAsync("Failed to save patient.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> DeletePatient(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var patientInDb = await context.Patients.FirstOrDefaultAsync(x => x.Id == id);
        if (patientInDb is null)
        {
            return await Result.FailAsync("Patient not found");
        }

        patientInDb.IsDeleted = true;
        context.Patients.Update(patientInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Patient has been deleted");
    }

    public async Task<PatientSummaryDto> GetPatientSummary(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var patient = await context.Patients
            .Include(patient => patient.Hcp)
            .Include(x => x.Hospitals)
            .Include(x => x.FamilyMembers)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == patientId);
        var hostpitals = await context.PatientHospitals.Include(x => x.Hospital).Where(x => x.PatientId == patientId)
            .ToListAsync();
        if (patient == null)
            return new PatientSummaryDto();

        var hospitals = mapper.Map<List<PatientHospitalDto>>(hostpitals);
        var families = mapper.Map<List<FamilyMemberDto>>(patient.FamilyMembers.Where(x => x.IsCarer == false));
        var summary = new PatientSummaryDto()
        {
            UniqueNumber = patient.UniqueNumber,
            Name = patient.FullName,
            DateOfBirth = patient.DateOfBirth.ToString("d"),
            Age = PatientConstants.CalculateAge(patient.DateOfBirth),
            Address = patient.Address.AddressLine1,
            Nationality = patient.Address.Country,
            Status = patient.Status,
            Type = patient.PatientType,
            Ppsn = patient.Ppsn,
            Mobile = patient.MobilePhone,
            Gender = patient.Gender,
            DefualtHcp = patient.Hcp.FullName,
            RegistrationDate = patient.RegistrationDate.ToString("d"),
            GmsStatus = patient.MedicalCardDetails.GmsStatus,
            GmsDoctor = patient.MedicalCardDetails.GmsDoctor,
            GmsNumber = patient.MedicalCardDetails.GmsPatientNumber,
            GmsReviewDate = patient.MedicalCardDetails.GmsReviewDate.ToString("d"),
            GmsDistance = patient.MedicalCardDetails.GmsDistanceCode,
            NkaFlag = patient.NkaFlag,
            Hospitals = hospitals,
            FamilyMembers = families
        };


        return summary;
    }


    public async Task<IResult> CreatePatient(UpsertPatientDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var patient = mapper.Map<Patient>(request);
            patient.Id = Guid.NewGuid();
            patient.MedicalRecordNumber = CryptographyHelper.GenerateMrNumber();
            patient.ClinicId = ApplicationState.Auth.CurrentUser.ClinicId;
            patient.CreatedBy = ApplicationState.Auth.CurrentUser.UserId;
            patient.CreatedDate = DateTime.Now;
            patient.RegistrationDate = DateTime.Now;
            patient.Gender = request.Gender.ToString();
            patient.PatientType = request.PatientType.ToString();
            //Address
            patient.Address.AddressLine1 = request.AddressLine1;
            patient.Address.AddressLine2 = request.AddressLine2;
            patient.Address.City = request.City;
            patient.Address.Country = request.Country;
            patient.Address.EriCode = request.EriCode;
            //Medical Details
            patient.MedicalCardDetails.GmsStatus = request.GmsStatus;
            patient.MedicalCardDetails.GmsDoctor = request.GmsDoctor;
            patient.MedicalCardDetails.GmsDoctorNumber = request.GmsDoctorNumber;
            patient.MedicalCardDetails.GmsPatientNumber =
                request.GmsPatientNumber == "A123456B" ? "" : request.GmsPatientNumber;
            patient.MedicalCardDetails.GmsReviewDate = request.GmsReviewDate;
            patient.MedicalCardDetails.GmsDoctorNumber = request.GmsDistanceCode;


            await context.Patients.AddAsync(patient);
            var rowsUpdated = await context.SaveChangesAsync();
            if (rowsUpdated > 0)
            {
                fileManagerService.CreatePatientDefualtDirectories(patient.Id);
                return await Result.SuccessAsync("Patient has been created.");
            }

            return await Result.FailAsync("Failed to save patient.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> UpdatePatient(Guid id, UpsertPatientDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var patientInDb = await context.Patients
                .Include(x=>x.PatientAccount)
                .FirstOrDefaultAsync(x => x.Id == id);
            
            if (patientInDb is null)
            {
                return await Result.FailAsync("Patient not found");
            }

            var patient = mapper.Map(request, patientInDb);
            patient.ClinicId = request.ClinicId;
            patient.ModifiedBy = ApplicationState.Auth.CurrentUser.UserId;
            patient.ModifiedDate = DateTime.Now;
            patient.Gender = request.Gender.ToString();
            patient.PatientType = request.PatientType.ToString();
            //Address
            patient.Address.AddressLine1 = request.AddressLine1;
            patient.Address.AddressLine2 = request.AddressLine2;
            patient.Address.City = request.City;
            patient.Address.Country = request.Country;
            patient.Address.EriCode = request.EriCode;
            //Medical Details
            patient.MedicalCardDetails.GmsStatus = request.GmsStatus;
            patient.MedicalCardDetails.GmsDoctor = request.GmsDoctor;
            patient.MedicalCardDetails.GmsDoctorNumber = request.GmsDoctorNumber;
            patient.MedicalCardDetails.GmsPatientNumber =
                request.GmsPatientNumber == "A000000B" ? "" : request.GmsPatientNumber;
            patient.MedicalCardDetails.GmsReviewDate = request.GmsReviewDate;
            patient.MedicalCardDetails.GmsDoctorNumber = request.GmsDistanceCode;

            context.ChangeTracker.Clear();
            context.Patients.Update(patient);
            var rowsUpdated = await context.SaveChangesAsync();
            if (rowsUpdated > 0)
            {
                return await Result.SuccessAsync("Patient has been updated.");
            }

            return await Result.FailAsync("Failed to save patient.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> SaveExtraDetails(PatientExtraDetailDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var extraDetail = await context.Patients
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PatientId);
            if (extraDetail is null)
            {
                return await Result.FailAsync("Details not found");
            }

            var otehrDetails = mapper.Map<OtherDetail>(request);
            var maritalDetail = mapper.Map<MaritalDetail>(request);
            extraDetail.OtherDetails = otehrDetails;
            extraDetail.MaritalDetails = maritalDetail;
            context.Patients.Update(extraDetail);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Details updated.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    #endregion

    #region Contact

    public async Task<List<PatientContactDto>> GetPatientContacts(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var list = await context.PatientContacts.Where(x => x.PatientId == id).ToListAsync();
        var data = mapper.Map<List<PatientContactDto>>(list);
        return data;
    }

    public async Task<PatientContactDto> GetPatientContact(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var contact = await context.PatientContacts.FirstOrDefaultAsync(x => x.Id == id);
        if (contact is null)
        {
            return new PatientContactDto();
        }

        return mapper.Map<PatientContactDto>(contact);
    }

    public async Task<IResult> DeletePatientContact(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var contact = await context.PatientContacts.FirstOrDefaultAsync(x => x.Id == id);
        if (contact is null)
        {
            return await Result.FailAsync("Contact not found");
        }

        context.PatientContacts.Remove(contact);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Contact has been removed");
    }

    public async Task<IResult> AddPatientContact(PatientContactDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var contact = mapper.Map<PatientContact>(request);
        await context.PatientContacts.AddAsync(contact);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Contact added.");
    }

    #endregion

    #region Patient Occupation

    public async Task<List<PatientOccupationDto>> GetPatientOccupations(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var list = await context.PatientOccupations.Where(x => x.PatientId == id).ToListAsync();
        var data = mapper.Map<List<PatientOccupationDto>>(list);
        return data;
    }

    public async Task<PatientOccupationDto> GetPatientOccupation(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var occupation = await context.PatientOccupations.FirstOrDefaultAsync(x => x.Id == id);
        if (occupation is null)
        {
            return new PatientOccupationDto();
        }

        return mapper.Map<PatientOccupationDto>(occupation);
    }

    public async Task<IResult> DeletePatientOccupation(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var occupation = await context.PatientOccupations.FirstOrDefaultAsync(x => x.Id == id);
        if (occupation is null)
        {
            return await Result.FailAsync("Occupation not found");
        }

        context.PatientOccupations.Remove(occupation);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Occupation has been removed");
    }

    public async Task<IResult> AddPatientOccupation(PatientOccupationDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var occupation = mapper.Map<PatientOccupation>(request);
        await context.PatientOccupations.AddAsync(occupation);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Occupation has been added.");
    }

    #endregion

    #region Patient Alerts

    public async Task<List<PatientAlertDto>> GetPatientAlerts(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientAlerts
            .Include(x => x.AlertCategory)
            .Where(x => x.IsDeleted == false)
            .AsNoTracking().Where(x => x.PatientId == patientId)
            .ToListAsync();
        context.ChangeTracker.Clear();
        var mappedData = mapper.Map<List<PatientAlertDto>>(list);
        return mappedData;
    }

    public async Task<List<PatientAlertDto>> GetPatientAlertByModule(Guid patientId, string alertType)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientAlerts
            .Include(x => x.AlertCategory)
            .AsNoTracking()
            .Where(x => x.PatientId == patientId
                        && x.Type == alertType
                        && x.IsDeleted == false
                        && x.IsResolved == false)
            .ToListAsync();

        context.ChangeTracker.Clear();
        var mappedData = mapper.Map<List<PatientAlertDto>>(list);
        return mappedData;
    }


    public async Task<PatientAlertDto> GetPatientAlert(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var alert = await context.PatientAlerts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        context.ChangeTracker.Clear();
        var mappedData = mapper.Map<PatientAlertDto>(alert);
        return mappedData;
    }

    public async Task<IResult> SavePatientAlert(Guid id, PatientAlertCreateDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var alert = new PatientAlert
        {
            Details = request.Details,
            Type = request.AlertType.ToString(),
            Severity = request.Severity.ToString(),
            AlertCategoryId = request.AlertCategoryId,
            CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
            CreatedDate = DateTime.Now,
            PatientId = request.PatientId
        };
        await context.PatientAlerts.AddAsync(alert);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Alert has been saved.");
    }

    public async Task<IResult> DeletePatientAlert(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var alert = await context.PatientAlerts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (alert is null)
        {
            return await Result.FailAsync("Alert not found");
        }

        alert.IsDeleted = true;
        alert.ModifiedDate = DateTime.Now;
        alert.ModifiedBy = ApplicationState.Auth.CurrentUser.UserId;
        context.ChangeTracker.Clear();
        context.PatientAlerts.Update(alert);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Alert has been removed");
    }

    public async Task<IResult> ResolvePatientAlert(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var alert = await context.PatientAlerts
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (alert is null)
        {
            return await Result.FailAsync("Alert not found");
        }

        alert.IsResolved = true;
        alert.ModifiedDate = DateTime.Now;
        alert.ModifiedBy = ApplicationState.Auth.CurrentUser.UserId;
        context.ChangeTracker.Clear();
        context.PatientAlerts.Update(alert);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Alert has been resolved.");
    }


    #region Alert Categories

    public async Task<List<AlertCategory>> GetAlertCategories()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.AlertCategories.ToListAsync();
    }

    public async Task<IResult> AddAlertCategories(string name)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var alertCategory = new AlertCategory()
        {
            Name = name
        };
        await context.AlertCategories.AddAsync(alertCategory);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Category has been added");
    }

    public async Task<IResult> DeleteAlertCategory(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var category = await context.AlertCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (category is null)
        {
            return await Result.FailAsync("Alert Category not found");
        }

        context.AlertCategories.Remove(category);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Alert Category has been removed");
    }

    #endregion

    #endregion

    #region Health care professional

    public async Task<List<RelatedHcpDto>> GetRealtedHcps(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.RelatedHcps
            .Where(x => x.PatientId == patientId)
            .ToListAsync();

        var mappedData = mapper.Map<List<RelatedHcpDto>>(list);
        return mappedData;
    }

    public async Task<RelatedHcpDto> GetRealtedHcp(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hcp = await context.RelatedHcps.FirstOrDefaultAsync(x => x.Id == id);
        var mappedData = mapper.Map<RelatedHcpDto>(hcp);
        return mappedData;
    }

    public async Task<IResult> SaveRelatedHcp(RelatedHcpDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (request.Id == 0)
            {
                var hcp = new RelatedHcp()
                {
                    Name = request.Name,
                    Type = request.Type,
                    Address = request.Address,
                    Telephone = request.Telephone,
                    Fax = request.Fax,
                    Email = request.Email,
                    Website = request.Website,
                    PatientId = request.PatientId
                };
                await context.RelatedHcps.AddAsync(hcp);
                await context.SaveChangesAsync();
                return await Result.SuccessAsync("HealthCare professional added.");
            }
            else
            {
                var hcpInDb = await context.RelatedHcps.FirstOrDefaultAsync(x => x.Id == request.Id);
                if (hcpInDb == null) return await Result.FailAsync("HCP not found.");

                hcpInDb = mapper.Map(request, hcpInDb);
                context.RelatedHcps.Update(hcpInDb);
                await context.SaveChangesAsync();
                return await Result.SuccessAsync("HealthCare professional Saved.");
            }
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> DeleteRelatedHcp(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hcpInDb = await context.RelatedHcps.FirstOrDefaultAsync(x => x.Id == id);
        if (hcpInDb == null) return await Result.FailAsync("HCP not found.");

        context.RelatedHcps.Remove(hcpInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("HCP deleted");
    }

    #endregion

    #region Select Hospital

    public async Task<List<PatientHospitalDto>> GetSelectedHospitals(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var data = await context.PatientHospitals
            .Include(x => x.Hospital)
            .Where(x => x.PatientId == patientId).ToListAsync();
        var mappedData = mapper.Map<List<PatientHospitalDto>>(data);
        return mappedData;
    }

    public async Task<IResult> SelectHospital(PatientHospitalDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (await context.PatientHospitals.AnyAsync(x => x.HospitalId == request.HospitalId))
        {
            return await Result.FailAsync("Hospital already exists");
        }

        var hospital = mapper.Map<PatientHospital>(request);
        await context.PatientHospitals.AddAsync(hospital);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Hospital added");
    }

    public async Task<IResult> DeleteSelectedHospital(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hospitalInDb = await context.PatientHospitals.FirstOrDefaultAsync(x => x.Id == id);
        if (hospitalInDb == null)
        {
            return await Result.FailAsync("Hospital not found");
        }

        context.PatientHospitals.Remove(hospitalInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Hospital delete");
    }

    #endregion

    #region Group

    public async Task<List<GroupDto>> GetGroups()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.Groups.ToListAsync();
        var data = mapper.Map<List<GroupDto>>(list);
        return data;
    }

    public async Task<GroupDto> GetGroup(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var group = await context.Groups.Include(x => x.RegisteredPatients)
            .FirstOrDefaultAsync(x => x.Id == id);
        var data = mapper.Map<GroupDto>(group);
        return data;
    }

    public async Task<IResult> SaveGroup(int id, UpsertGroupDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (id == 0)
        {
            if (await context.Groups.AnyAsync(x => x.Name == request.Name))
            {
                return await Result.FailAsync("Group name already exists.");
            }

            var group = mapper.Map<Group>(request);
            await context.Groups.AddAsync(group);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Group added");
        }
        else
        {
            var groupInDb = await context.Groups.FirstOrDefaultAsync(x => x.Id == id);
            if (groupInDb == null)
            {
                return await Result.FailAsync("group not found");
            }

            groupInDb = mapper.Map(request, groupInDb);
            context.Groups.Update(groupInDb);
            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Group updated");
        }
    }

    public async Task<IResult> DeleteGroup(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var groupInDb = await context.Groups.FirstOrDefaultAsync(x => x.Id == id);
        if (groupInDb == null)
        {
            return await Result.FailAsync("group not found");
        }

        context.Groups.Remove(groupInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Group delete");
    }


    #region Group Patients

    public async Task<List<GroupPatientDto>> GetPatientsByGroup(int groupId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.GroupPatients
            .Include(x => x.Patient)
            .Where(x => x.GroupId == groupId)
            .ToListAsync();
        var data = mapper.Map<List<GroupPatientDto>>(list);
        return data;
    }

    public async Task<GroupDto> GetSelectedGroup(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var patientGroup = await context.GroupPatients
            .Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.PatientId == patientId);
        if (patientGroup is null)
        {
            return new GroupDto();
        }

        var mappedData = mapper.Map<GroupDto>(patientGroup.Group);
        return patientGroup != null ? mappedData : new GroupDto();
    }

    public async Task<IResult> RegisterPatientToGroup(GroupPatientDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var register = mapper.Map<GroupPatient>(request);
        await context.GroupPatients.AddAsync(register);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Patient register to group");
    }

    #endregion

    #endregion

    #region Family

    public async Task<List<FamilyMemberDto>> GetFamilyMembers(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.FamilyMembers.Where(x => x.PatientId == patientId).ToListAsync();
        var mapped = mapper.Map<List<FamilyMemberDto>>(list);
        return mapped;
    }

    public async Task<FamilyMemberDto> GetFamilyMember(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var data = await context.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id);
        var mapped = mapper.Map<FamilyMemberDto>(data);
        return mapped;
    }

    public async Task<IResult> SaveFamilyMember(int id, FamilyMemberDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (id == 0)
        {
            var nextOfKin = mapper.Map<FamilyMember>(request);
            await context.FamilyMembers.AddAsync(nextOfKin);
        }
        else
        {
            var nextOfKinInDb = await context.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id);
            if (nextOfKinInDb == null)
            {
                return await Result.FailAsync("member not found");
            }

            nextOfKinInDb = mapper.Map(request, nextOfKinInDb);
            context.FamilyMembers.Update(nextOfKinInDb);
        }

        await context.SaveChangesAsync();

        return await Result.SuccessAsync("Member has been saved.");
    }

    public async Task<IResult> DeleteFamilyMember(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var nextKin = await context.FamilyMembers.FirstOrDefaultAsync(x => x.Id == id);
        if (nextKin == null)
        {
            return await Result.FailAsync("record not found");
        }

        context.FamilyMembers.Remove(nextKin);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Member removed");
    }

    #endregion

    #region Account

    public async Task<GetPatientAccountDto> GetPatientAccount(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var patient = await context.PatientAccounts
            .Include(x => x.Patient)
            .Include(x => x.PatientTransactions)
            .FirstOrDefaultAsync(x => x.PatientId == patientId);
        var data = mapper.Map<GetPatientAccountDto>(patient);
        return data;
    }

    public async Task<List<GetTransactionDto>> GetOutstanding(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientTransactions
            .Include(x => x.PatientAccount)
            .OrderByDescending(x => x.CreatedDate)
            .Where(t => t.PatientAccountId == accountId
                        && !t.IsDeleted
                        && t.TransactionType == TransactionType.Charge).ToListAsync();
        var data = mapper.Map<List<GetTransactionDto>>(list);
        return data;
    }

    public async Task<List<AccountStatementDto>> GetStatement(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var statements = new List<AccountStatementDto>();
        var account = await context.PatientAccounts.Include(x =>
                x.PatientTransactions.Where(t =>
                    t.TransactionType == TransactionType.Charge && !t.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == accountId);
        var statement = mapper.Map<AccountStatementDto>(account);
        statements.Add(statement);
        return statements;
    }

    public async Task<List<GetTransactionDto>> GetPrintLog(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientTransactions
            .Include(x => x.PatientAccount)
            .Where(t => t.PatientAccountId == accountId
                        && t.IsPrinted).ToListAsync();
        var data = mapper.Map<List<GetTransactionDto>>(list);
        return data;
    }

    public async Task<List<GetTransactionDto>> GetAudit(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientTransactions
            .Include(x => x.PatientAccount)
            .Where(t => t.PatientAccountId == accountId && t.IsDeleted).ToListAsync();
        var data = mapper.Map<List<GetTransactionDto>>(list);
        return data;
    }

    public async Task<List<GetTransactionDto>> GetHistory(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientTransactions
            .Include(x => x.PatientAccount)
            .Where(t => t.PatientAccountId == accountId)
            .ToListAsync();
        var data = mapper.Map<List<GetTransactionDto>>(list);
        return data;
    }

    public async Task<IResult> Charge(ChargeDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        try
        {
            var previousBalance = await GetBroughtForwardBalance(request.AccountId);
            var chargedTransaction = new PatientTransaction
            {
                CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
                CreatedDate = request.Date,
                TransactionType = TransactionType.Charge,
                PatientAccountType = request.ChargeTo,
                Description = request.Description ?? TransactionType.Charge.ToString(),
                PatientAccountId = request.AccountId,
                ActionType = request.AccountType ?? TransactionActionType.Charge.ToString(),
                Debit = request.Amount
            };
            await context.PatientTransactions.AddAsync(chargedTransaction);

            if (request.MakePayment)
            {
                if (request.Amount != request.PaidAmount)
                {
                    previousBalance += request.PaidAmount;
                    chargedTransaction.Balance = previousBalance;
                }
                else
                {
                    previousBalance += request.Amount;
                    chargedTransaction.IsDeleted = true;
                }

                await context.SaveChangesAsync();
                await AddSinglePayment(request);
            }

            return await Result.SuccessAsync("Charged has been added.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    private async Task AddSinglePayment(ChargeDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var previousBalance = await GetBroughtForwardBalance(request.AccountId);
        previousBalance -= request.PaidAmount;
        var paymentTransaction = new PatientTransaction()
        {
            CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
            CreatedDate = request.Date,
            TransactionType = TransactionType.Payment,
            PatientAccountType = request.ChargeTo,
            Description = request.Description ?? TransactionType.Payment.ToString(),
            Credit = request.PaidAmount,
            IsPrinted = request.IsPrinted,
            PatientAccountId = request.AccountId,
            ActionType = request.AccountType ?? TransactionActionType.Payment.ToString(),
            Balance = previousBalance,
        };
        await context.PatientTransactions.AddAsync(paymentTransaction);
        await context.SaveChangesAsync();
       await UpdateTransaction(paymentTransaction.Id);
      await  UpdatePersonalBalance(request.AccountId, TransactionActionType.Payment, request.PaidAmount);
    }

    public async Task<IResult> PaymentForAllocatedItems(PaymentAllocatedDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var previousBalance = await GetBroughtForwardBalance(request.AccountId);
            foreach (var item in request.SelectedTransaction)
            {
                previousBalance -= item.Amount;

                var transaction = new PatientTransaction()
                {
                    CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
                    CreatedDate = request.Date,
                    TransactionType = TransactionType.Payment,
                    PatientAccountType = request.PaymentTo,
                    Description = request.Description ?? TransactionType.Payment.ToString(),
                    Credit = item.Amount,
                    PatientAccountId = request.AccountId,
                    IsPrinted = request.IsPrinted,
                    ActionType = request.AccountType ?? TransactionActionType.Payment.ToString(),
                    Balance = previousBalance
                };

                await context.PatientTransactions.AddAsync(transaction);
              await  UpdateTransaction(item.Id);
            }

            await context.SaveChangesAsync();
           await UpdatePersonalBalance(request.AccountId, TransactionActionType.Payment, request.Amount);
            return await Result.SuccessAsync("Payment has been made successfully.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> StrikeOff(StrikeOffDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var previousBalance = await GetBroughtForwardBalance(request.AccountId);

            foreach (var item in request.SelectedTransaction)
            {
                previousBalance -= item.Amount;

                var transaction = new PatientTransaction()
                {
                    CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
                    CreatedDate = request.Date,
                    TransactionType = TransactionType.Payment,
                    PatientAccountType = request.PaymentTo,
                    Description = request.AccountType ?? TransactionActionType.StrikeOff.ToString(),
                    Credit = item.Debit,
                    PatientAccountId = request.AccountId,
                    ActionType = request.AccountType ?? TransactionActionType.StrikeOff.ToString(),
                    Balance = previousBalance
                };
                await context.PatientTransactions.AddAsync(transaction);
              await  UpdateTransaction(item.Id);
            }

            await context.SaveChangesAsync();
            return await Result.SuccessAsync("Strike-off has been made successfully.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> DeleteTransaction(int transactionId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var transaction = context.PatientTransactions
            .FirstOrDefault(x => x.Id == transactionId);

        if (transaction is null)
        {
            return await Result.FailAsync("Transaction is not found.");
        }

        transaction.IsDeleted = true;
        context.ChangeTracker.Clear();
        context.PatientTransactions.Update(transaction);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Transactions has been deleted successfully.");
    }

    private async Task UpdateTransaction(int transId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var transaction = context.PatientTransactions
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == transId);

        if (transaction is not null)
        {
            transaction.IsDeleted = true;
            context.ChangeTracker.Clear();
            context.PatientTransactions.Update(transaction);
        }
    }

    private async Task<decimal> UpdatePersonalBalance(int accountId, TransactionActionType transactionType,
        decimal amount)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var patientAccount = await context.PatientAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == accountId);
        decimal newBalance = 0;
        if (patientAccount is not null)
        {
            if (transactionType == TransactionActionType.Payment)
            {
                patientAccount.Balance -= amount;
            }
            else if (transactionType == TransactionActionType.StrikeOff)
            {
                patientAccount.Balance += amount;
            }

            context.ChangeTracker.Clear();
            context.PatientAccounts.Update(patientAccount);
            await context.SaveChangesAsync();
            newBalance = patientAccount.Balance;
        }

        return newBalance;
    }

    private async Task<decimal> GetPersonalBalance(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var patientAccount = await context.PatientAccounts
            .FirstOrDefaultAsync(x => x.Id == accountId);

        return patientAccount?.Balance ?? 0;
    }

    public async Task<decimal> GetBroughtForwardBalance(int accountId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var broughtForwardBalance = await context.PatientTransactions
            .Where(t => t.CreatedDate.Date.Month == DateTime.Today.Month && t.PatientAccountId == accountId &&
                        !t.IsDeleted)
            .SumAsync(t => t.Debit - t.Credit);

        return broughtForwardBalance;
    }

    #endregion

    #region Options

    #region Hospital Viewer

    public async Task<List<HospitalDto>> GetHospitals()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hospitals = await context.Hospitals.ToListAsync();
        var mapped = mapper.Map<List<HospitalDto>>(hospitals);
        return mapped;
    }

    public async Task<HospitalDto> GetHospital(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hospital = await context.Hospitals.FirstOrDefaultAsync(x => x.Id == id);
        var mapped = mapper.Map<HospitalDto>(hospital);
        return mapped;
    }

    public async Task<IResult> SaveHospital(int id, HospitalDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hospital = mapper.Map<Hospital>(request);
        if (id == 0)
        {
            await context.Hospitals.AddAsync(hospital);
            await context.SaveChangesAsync();
        }
        else
        {
            var hospitalInDb = await context.Hospitals.FirstOrDefaultAsync(x => x.Id == id);
            if (hospitalInDb is null)
                return await Result.FailAsync("Hospital not found.");

            hospitalInDb = mapper.Map(hospital, hospitalInDb);
            context.ChangeTracker.Clear();
            context.Hospitals.Update(hospitalInDb);
            await context.SaveChangesAsync();
        }

        return await Result.SuccessAsync("Hospital saved.");
    }

    public async Task<IResult> DeleteHospital(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var hospital = await context.Hospitals.FirstOrDefaultAsync(x => x.Id == id);
        if (hospital is null)
            return await Result.FailAsync("Hospital not found.");
        context.Hospitals.Remove(hospital);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Hospital deleted.");
    }

    public async Task<IResult> CreateGroupAlerts(CreatePatientGroupAlertDto request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        foreach (var alert in request.SelectedPatients.Select(item => new PatientAlert
                 {
                     Details = request.Details,
                     Type = request.AlertType.ToString(),
                     Severity = request.Severity.ToString(),
                     AlertCategoryId = request.AlertCategoryId,
                     CreatedBy = ApplicationState.Auth.CurrentUser.UserId,
                     CreatedDate = DateTime.Now,
                     PatientId = item.Id
                 }))
        {
            await context.PatientAlerts.AddAsync(alert);
        }

        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Group Alerts has been saved.");
    }

    public async Task<IResult> ResolveGroupAlerts(List<GetGroupAlertDto> groupAlerts)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        foreach (var item in groupAlerts)
        {
            var alertInDb = await context.PatientAlerts.FirstOrDefaultAsync(x => x.Id == item.Id);
            if (alertInDb is not null)
            {
                alertInDb.IsResolved = true;
                context.PatientAlerts.Update(alertInDb);
            }
        }

        await context.SaveChangesAsync();

        return await Result.SuccessAsync("Selected Alerts has been resolved.");
    }

    public async Task<IResult> DeleteGroupAlerts(List<GetGroupAlertDto> groupAlerts)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        foreach (var item in groupAlerts)
        {
            var alertInDb = await context.PatientAlerts.FirstOrDefaultAsync(x => x.Id == item.Id);
            if (alertInDb is null) continue;
            var data = mapper.Map<PatientAlert>(alertInDb);
            context.PatientAlerts.Remove(data);
        }

        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Selected Alerts has been deleted.");
    }

    public async Task<List<GetGroupAlertDto>> GetAllPatientAlerts()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientAlerts
            .Include(x => x.AlertCategory)
            .Include(x => x.Patient)
            .Where(x => x.IsDeleted == false)
            .AsNoTracking()
            .ToListAsync();
        context.ChangeTracker.Clear();
        var mappedData = mapper.Map<List<GetGroupAlertDto>>(list);
        return mappedData;
    }

    #endregion

    #endregion

    #region Allergies

    public async Task<List<PatientAllergyDto>> GetPatientAllergies()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientAllergies.AsNoTracking()
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
        var data = mapper.Map<List<PatientAllergyDto>>(list);
        return data;
    }

    public async Task<PatientAllergyDto> GetPatientAllergy(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var allergyInDb = await context.PatientAllergies.FirstOrDefaultAsync(x => x.Id == id);
        if (allergyInDb is null)
        {
            return new PatientAllergyDto();
        }

        var mapped = mapper.Map<PatientAllergyDto>(allergyInDb);
        return mapped;
    }

    public async Task<IResult> UpsertPatientAllergy(int id, UpsertAllergyDto allergy)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (id == 0)
            {
                var data = mapper.Map<PatientAllergy>(allergy);
                await context.PatientAllergies.AddAsync(data);
                await context.SaveChangesAsync();
            }
            else
            {
                var allergyInDb = await context.PatientAllergies.FirstOrDefaultAsync(x => x.Id == id);
                allergyInDb = mapper.Map(allergy, allergyInDb);
                context.PatientAllergies.Update(allergyInDb);
                await context.SaveChangesAsync();
            }

            await UpdatePatientNka();
            return await Result.SuccessAsync("Allergy has been recorded.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<IResult> RemovePatientAllergy(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var allergyInDb = await context.PatientAllergies.FirstOrDefaultAsync(x => x.Id == id);
        if (allergyInDb is null)
        {
            return await Result.FailAsync("item not found");
        }

        context.PatientAllergies.Remove(allergyInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Allergy has been deleted.");
    }


    public async Task<List<DrugAllergyDto>> GetPatientDrugAllergies()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var list = await context.PatientDrugAllergies.AsNoTracking()
            .Where(x => x.PatientId == ApplicationState.GetSelectPatientId())
            .ToListAsync();
        var data = mapper.Map<List<DrugAllergyDto>>(list);
        return data;
    }

    public async Task<IResult> UpsertPatientDrugAllergy(int id, UpsertDrugAllergyDto allergy)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (id == 0)
            {
                var data = mapper.Map<PatientDrugAllergy>(allergy);
                await context.PatientDrugAllergies.AddAsync(data);
                await context.SaveChangesAsync();
                await UpdatePatientNka();
            }
            else
            {
                var allergyInDb = await context.PatientDrugAllergies.FirstOrDefaultAsync(x => x.Id == id);
                allergyInDb = mapper.Map(allergy, allergyInDb);
                context.PatientDrugAllergies.Update(allergyInDb);
                await context.SaveChangesAsync();
            }

            return await Result.SuccessAsync("Allergy has been recorded.");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    public async Task<DrugAllergyDto> GetPatientDrugAllergy(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var allergyInDb = await context.PatientDrugAllergies.FirstOrDefaultAsync(x => x.Id == id);
        if (allergyInDb is null)
        {
            return new DrugAllergyDto();
        }

        var mapped = mapper.Map<DrugAllergyDto>(allergyInDb);
        return mapped;
    }

    public async Task<IResult> RemovePatientDrugAllergy(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var allergyInDb = await context.PatientDrugAllergies.FirstOrDefaultAsync(x => x.Id == id);
        if (allergyInDb is null)
        {
            return await Result.FailAsync("item not found");
        }

        context.PatientDrugAllergies.Remove(allergyInDb);
        await context.SaveChangesAsync();
        await UpdatePatientNka();
        return await Result.SuccessAsync("Allergy has been deleted.");
    }

    private async Task UpdatePatientNka()
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var patient = await context.Patients.AsNoTracking()
                .Include(x => x.PatientAccount)
                .FirstOrDefaultAsync(x => x.Id == ApplicationState.GetSelectPatientId());

            var allergyCount = await context.PatientAllergies.CountAsync(x =>
                x.PatientId == ApplicationState.GetSelectPatientId());

            var drugAllergyCount = await context.PatientDrugAllergies.CountAsync(x =>
                x.PatientId == ApplicationState.GetSelectPatientId());

            if (allergyCount > 0 || drugAllergyCount > 0)
            {
                patient.NkaFlag = false;
            }
            else
            {
                patient.NkaFlag = true;
            }

            patient.PatientAccount!.PatientId = ApplicationState.GetSelectPatientId();
            context.ChangeTracker.Clear();
            context.Patients.Update(patient);
            await context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await Result.FailAsync(e.Message);
        }
    }

    #endregion
}