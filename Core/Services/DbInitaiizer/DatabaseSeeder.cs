using Bogus;
using Database;
using Domain.Entities.Appointments;
using Domain.Entities.Common;
using Domain.Entities.PatientManagement;
using Domain.Entities.PatientManagement.Alert;
using Domain.Entities.PatientManagement.Details;
using Domain.Entities.Settings.Clinic;
using Domain.Entities.Settings.Templates;
using Domain.Entities.Settings.Templates.Dms;
using Domain.Entities.UserAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhoneNumbers;
using Services.Features.Settings.Service;
using Shared.Constants.Application;
using Shared.Constants.Module;
using Shared.Constants.Permission;
using Shared.Constants.Role;
using Shared.Helper;

namespace Services.DbInitaiizer;

public class DatabaseSeeder(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ILogger<DatabaseSeeder> logger,
    IFileManagerService fileManagerService) : IDatabaseSeeder
{
    public void Initialize()
    {
        SeedApplicationNavs();
        SeedUserTypes();
        SeedClinics();
        SeedRoles();
        SeedUsers();
        SeedAppointmentTypes();
        SeedAppointmentCancelReasons();
        SeedAlertCategory();
        SeedFakePatientData();
        SeedFakeUserData();
        SeedSketchCategories();
        SeedDmsCategory();
    }

    private void SeedUserTypes()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.UserTypes.Any())
                return;

            var roles = new List<UserType>()
            {
                new()
                {
                    Name = "Nurse",
                    IsDefualt = true
                },
                new()
                {
                    Name = "Doctor",
                    IsDefualt = true
                }
            };

            await context.UserTypes.AddRangeAsync(roles);
            await context.SaveChangesAsync();
            logger.LogInformation("User Types seeded");
        }).GetAwaiter().GetResult();
    }

    private void SeedApplicationNavs()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.AppModules.Any())
                return;

            var parentMenus = new List<AppModule>()
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Dashboard",
                    Href = "/home",
                    Icon = "far fa-home",
                    Order = 1,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Appointment",
                    Href = "/appointments",
                    Icon = "far fa-calendar-check",
                    Order = 2,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Waiting Room",
                    Href = "/waiting-room",
                    Icon = "far fa-hourglass-start",
                    Order = 3,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Consultation",
                    Href = "/consultation",
                    Icon = "far fa-user-doctor-message",
                    Order = 4,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Patient Manager",
                    Href = "/patient-management",
                    Icon = "far fa-hospital-user",
                    Order = 5,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Reporting",
                    Href = "/reporting",
                    Icon = "far fa-file-invoice",
                    Order = 6,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Messaging",
                    Href = "/messaging",
                    Icon = "far fa-message-dots",
                    Order = 7,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Settings",
                    Href = "/settings",
                    Icon = "far fa-gear",
                    Order = 8
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "User Manager",
                    Href = "/user-manager",
                    Icon = "fas fa-users-medical",
                    Order = 9
                },
            };

            var appointment = new List<AppModule>()
            {
            };

            var messaging = new List<AppModule>()
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "User Tasks",
                    Href = "",
                    Icon = "user-task-icon",
                    Order = 0,
                    ParentId = parentMenus.FirstOrDefault(x => x.Name == "Messaging")!.Id,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Messaging",
                    Href = "",
                    Icon = "sms-message-icon",
                    Order = 1,
                    ParentId = parentMenus.FirstOrDefault(x => x.Name == "Messaging")!.Id,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Instant Messaging History",
                    Href = "",
                    Icon = "text-message-icon",
                    Order = 2,
                    ParentId = parentMenus.FirstOrDefault(x => x.Name == "Messaging")!.Id,
                },
            };
            var userManagerMenus = new List<AppModule>()
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Users",
                    Href = "",
                    Icon = "users-icon",
                    Order = 0,
                    ParentId = parentMenus.FirstOrDefault(x => x.Name == "User Manager")!.Id,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "User Type",
                    Href = "",
                    Icon = "users-icon",
                    Order = 1,
                    ParentId = parentMenus.FirstOrDefault(x => x.Name == "User Manager")!.Id,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Permissions",
                    Href = "",
                    Icon = "permissions-icon",
                    Order = 2,
                    ParentId = parentMenus.FirstOrDefault(x => x.Name == "User Manager")!.Id,
                },
            };


            await context.AppModules.AddRangeAsync(parentMenus);
            await context.AppModules.AddRangeAsync(userManagerMenus);
            await context.AppModules.AddRangeAsync(messaging);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedDmsCategory()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.DocumentCategories.Any())
                return;

            var c = new DocumentCategory()
            {
                Name = "General"
            };
            context.DocumentCategories.Add(c);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedSketchCategories()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            if (context.SketchCategories.Any())
                return;

            var parts = new List<SketchCategory>()
            {
                new()
                {
                    Name = "Arm"
                },
                new()
                {
                    Name = "Body"
                },
                new()
                {
                    Name = "Brain"
                },
                new()
                {
                    Name = "Breast"
                },
                new()
                {
                    Name = "Colon"
                },
                new()
                {
                    Name = "Foot"
                },
                new()
                {
                    Name = "Hand"
                },
                new()
                {
                    Name = "Head"
                },
                new()
                {
                    Name = "Hip"
                },
                new()
                {
                    Name = "Knee"
                },
                new()
                {
                    Name = "Leg"
                }
            };
            await context.SketchCategories.AddRangeAsync(parts);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedAlertCategory()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.AlertCategories.Any())
                return;
            var c = new AlertCategory()
            {
                Name = "General"
            };
            context.AlertCategories.Add(c);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedAppointmentCancelReasons()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.AppointmentCancellationReasons.Any())
                return;
            var c = new AppointmentCancellationReason()
            {
                Reason = "Unable to attend",
                IsDefault = true
            };
            context.AppointmentCancellationReasons.Add(c);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedAppointmentTypes()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.AppointmentTypes.Any())
                return;
            var c = new AppointmentType()
            {
                Name = "General",
                Duration = 15,
                Active = true,
            };
            context.AppointmentTypes.Add(c);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedFakeUserData()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var userTypeIds = context.UserTypes.ToList().Select(x => x.Id);
            var roleIds = context.Roles.Select(x => x.Id).ToList();
            if (context.Users.Count() == 2)
            {
                var fakeUsers = new Faker<User>()
                    .RuleFor(x => x.CreatedDate,
                        x => x.Date.Between(new DateTime(2020, 1, 1), new DateTime(2025, 1, 1)))
                    .RuleFor(x => x.FirstName, x => x.Person.FirstName)
                    .RuleFor(x => x.LastName, x => x.Person.LastName)
                    .RuleFor(x => x.FullName, x => x.Person.FullName)
                    .RuleFor(x => x.Username, x => x.Person.UserName)
                    .RuleFor(x => x.Email, x => x.Person.Email)
                    .RuleFor(x => x.ModifiedDate, DateTime.Today)
                    .RuleFor(x => x.Mcn, x => x.Phone.PhoneNumber())
                    .RuleFor(x => x.UserTypeId, x => x.PickRandom(userTypeIds))
                    .RuleFor(x => x.RoleId, x => x.PickRandom(roleIds))
                    .RuleFor(x => x.RoleId, x => x.PickRandom(roleIds))
                    .RuleFor(x => x.IsActive, true)
                    .RuleFor(x => x.PasswordHash,
                        SecurePasswordHasher.HashPassword(ApplicationConstants.DefaultPassword))
                    .RuleFor(x => x.MobileNumber, x => x.Person.Phone);
                var users = fakeUsers.Generate(ApplicationConstants.SeedFakeUsersCount);
                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
                foreach (var item in users)
                {
                    var userClinic = new UserClinic()
                    {
                        ClinicId = 1,
                        UserId = item.Id
                    };
                    await context.UserClinics.AddAsync(userClinic);
                }

                await context.SaveChangesAsync();
            }
        }).GetAwaiter().GetResult();
    }

    private void SeedFakePatientData()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.Patients.Any())
                return;

            if (!context.Patients.Any())
            {
                var address = new PatientAddress() {AddressLine1 = "Testing London"};
                var medical = new MedicalCardDetail() {GmsStatus = "Active", GmsPatientNumber = "M567890A"};
                var clinicId = context.Clinics.FirstOrDefault(x => x.Name == "Clinic")!.Id;
                var hcpId = context.Users.FirstOrDefault(x => x.FirstName == "User")!.Id;
                var util = PhoneNumberUtil.GetInstance();
                var num = util.GetExampleNumber("US");
                var fakePatients = new Faker<Patient>()
                    .RuleFor(x => x.Id, x => Guid.NewGuid())
                    .RuleFor(x => x.FamilyName, x => "Test")
                    .RuleFor(x => x.FirstName, x => x.Person.FirstName)
                    .RuleFor(x => x.LastName, x => x.Person.LastName)
                    .RuleFor(x => x.FullName, x => x.Person.FullName)
                    .RuleFor(x => x.DateOfBirth,
                        x => x.Date.Between(new DateTime(1994, 1, 1), new DateTime(2025, 1, 1)))
                    .RuleFor(x => x.RegistrationDate,
                        x => x.Date.Between(new DateTime(1994, 1, 1), new DateTime(2025, 1, 1)))
                    .RuleFor(x => x.Gender, x => x.Person.Gender.ToString())
                    .RuleFor(x => x.Address, x => address)
                    .RuleFor(x => x.MobilePhone, x => util.Format(num, PhoneNumberFormat.E164))
                    .RuleFor(x => x.Email, x => x.Person.Email)
                    .RuleFor(x => x.ClinicId, clinicId)
                    .RuleFor(x => x.HcpId, hcpId)
                    .RuleFor(x => x.MedicalCardDetails, medical)
                    .RuleFor(x => x.UniqueNumber, CryptographyHelper.GetUniqueKey(8))
                    .RuleFor(x => x.MedicalRecordNumber, CryptographyHelper.GenerateMrNumber())
                    .RuleFor(x => x.Status, PatientStatus.Active.ToString())
                    .RuleFor(x => x.PatientType, PatientType.Private.ToString())
                    .RuleFor(x => x.IhiNumber, CryptographyHelper.GetUniqueKey(17))
                    .RuleFor(x => x.CreatedBy, Guid.NewGuid());
                var patients = fakePatients.Generate(ApplicationConstants.SeedFakePatientsCount);
                foreach (var patient in patients)
                {
                    await context.Patients.AddAsync(patient);
                    await context.SaveChangesAsync();
                    fileManagerService.CreatePatientDefualtDirectories(patient.Id);
                }
            }
        }).GetAwaiter().GetResult();
    }


    private async Task SeedRolePermissions(string roleName, bool allowed = false)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var role = await context.Roles.FirstOrDefaultAsync(x => x.Name == roleName);

        var modules = await context.AppModules.OrderBy(x => x.Order).ToListAsync();

        var list = (from module in modules
            from claim in PermissionConstants.AllClaims
            select new PermissionClaim
            {
                ModuleName = module.Name,
                ModuleId = module.Id,
                RoleId = role.Id,
                ClaimName = claim,
                Allowed = allowed
            }).ToList();

        if (role.Name is RoleConstants.Nurse or RoleConstants.Doctor or RoleConstants.Receptionist)
        {
            foreach (var item in list.Where(x => x.ClaimName == PermissionConstants.Read))
            {
                item.Allowed = true;
            }
        }

        await context.PermissionClaims.AddRangeAsync(list);
        await context.SaveChangesAsync();
        logger.LogInformation("{Count} are Permission added for {RoleName}", list.Count, roleName);
    }

    private void SeedClinics()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            if (context.Clinics.Any())
                return;
            var c = new Clinic()
            {
                Address = "Not set",
                Name = "Clinic",
            };
            context.Clinics.Add(c);
            await context.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private void SeedUsers()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var userTypeInDb = await context.UserTypes.FirstOrDefaultAsync(x => x.Name == UserTypeConstants.Doctor);

            if (context.Users.Any())
                return;
            var passHash = SecurePasswordHasher.HashPassword(ApplicationConstants.DefaultPassword);
            var users = new List<User>()
            {
                new()
                {
                    Username = "admin@dexterity",
                    Email = "admin@dexterity.com",
                    FirstName = "Admin",
                    LastName = "Dexterity",
                    MobileNumber = "123589641",
                    IsActive = true,
                    Mcn = "00000000",
                    Ban = "00000000",
                    UserTypeId = userTypeInDb.Id,
                    PasswordHash = passHash,
                    RoleId = context.Roles.FirstOrDefault(x => x.Name == RoleConstants.AdministratorRole)!.Id,
                },
                new()
                {
                    Username = "user@dexterity",
                    Email = "user@dexterity.com",
                    FirstName = "User",
                    LastName = "Dexterity",
                    IsActive = true,
                    MobileNumber = "123589641",
                    Mcn = "00000000",
                    Ban = "00000000",
                    UserTypeId = userTypeInDb.Id,
                    PasswordHash = passHash,
                    RoleId = context.Roles.FirstOrDefault(x => x.Name == RoleConstants.Nurse)!.Id
                },
            };

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
            foreach (var user in users)
            {
                var clinic = new UserClinic()
                {
                    UserId = user.Id,
                    ClinicId = 1
                };
                context.UserClinics.Add(clinic);
                await context.SaveChangesAsync();
            }

            logger.LogInformation("Users seeded");
        }).GetAwaiter().GetResult();
    }

    private void SeedRoles()
    {
        Task.Run(async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            if (context.Roles.Any())
                return;
            var roles = new List<Role>()
            {
                new()
                {
                    CreatedDate = DateTime.Today,
                    Name = RoleConstants.AdministratorRole,
                    IsDefualt = true
                },
                new()
                {
                    CreatedDate = DateTime.Today,
                    Name = RoleConstants.Nurse,
                    IsDefualt = true
                },
                new()
                {
                    CreatedDate = DateTime.Today,
                    Name = RoleConstants.Doctor,
                    IsDefualt = true
                },
                new()
                {
                    CreatedDate = DateTime.Today,
                    Name = RoleConstants.Receptionist,
                    IsDefualt = true
                }
            };
            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
            await SeedRolePermissions(RoleConstants.AdministratorRole, true);
            await SeedRolePermissions(RoleConstants.Nurse);
            await SeedRolePermissions(RoleConstants.Doctor);
            await SeedRolePermissions(RoleConstants.Receptionist);

            logger.LogInformation("Roles seeded");
        }).GetAwaiter().GetResult();
    }
}