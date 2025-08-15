using AutoMapper;
using ClickATell.Requests;
using ClickATell.Services;
using Database;
using Domain.Entities.Messaging;
using Domain.Entities.PatientManagement.Options;
using Domain.Entities.UserAccounts;
using Microsoft.EntityFrameworkCore;
using PhoneNumbers;
using Services.Features.Messaging.Dtos.InstantChat;
using Services.Features.Messaging.Dtos.Sms;
using Services.Features.Messaging.Dtos.UserTasks;
using Services.Features.UserAccounts.Dtos.User;
using Services.State;
using Shared.Constants.Application;
using Shared.Constants.Module;
using Shared.Helper;
using Shared.Wrapper;

namespace Services.Features.Messaging.Service;

public class MessagingService(
    
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IMapper mapper,
    SmsEndpoints smsEndpoints)
    : IMessagingService
{
    #region User Tasks

    public async Task<List<UserTask>> GetUserTasksByPatient(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.UserTasks
            .Where(x => x.PatientId == patientId && x.Status == UserTaskConstants.TaskStatusConstant.Active)
            .OrderByDescending(x => x.TaskDate)
            .ToListAsync();
    }

    public async Task<List<UserTask>> GetAllUserTasks()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.UserTasks
            .Where(x => x.Status == UserTaskConstants.TaskStatusConstant.Active)
            .OrderByDescending(x => x.TaskDate)
            .ToListAsync();
    }

    public async Task<int> GetUserTasksCountByPatient(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.UserTasks
            .CountAsync(x => x.PatientId == patientId && x.Status == UserTaskConstants.TaskStatusConstant.Active);
    }

    public async Task<List<UserTask>> GetUserTaskList(string view = UserTaskConstants.All)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var startOfWeek = DateTime.Now.StartOfWeek(DayOfWeek.Monday);
        return view switch
        {
            UserTaskConstants.All => await context.UserTasks
                .Where(x => x.UserId == ApplicationState.Auth.CurrentUser.UserId &&
                            x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
                .Include(x => x.AssignedBy)
                .ToListAsync(),
            UserTaskConstants.DayView => await context.UserTasks
                .Where(x => x.UserId == ApplicationState.Auth.CurrentUser.UserId &&
                            x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
                .Where(x => x.TaskDate.Date == DateTime.Today.Date)
                .Include(x => x.AssignedBy)
                .ToListAsync(),

            UserTaskConstants.WeekView => await context.UserTasks
                .Where(x => x.UserId == ApplicationState.Auth.CurrentUser.UserId &&
                            x.TaskDate.Date < startOfWeek.AddDays(7) &&
                            x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
                .Include(x => x.AssignedBy)
                .ToListAsync(),
            UserTaskConstants.MonthView => await context.UserTasks
                .Where(x => x.UserId == ApplicationState.Auth.CurrentUser.UserId &&
                            x.TaskDate.Date.Month == DateTime.Today.Date.Month &&
                            x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
                .Include(x => x.AssignedBy)
                .ToListAsync(),

            _ => await context.UserTasks.Where(x => x.UserId == ApplicationState.Auth.CurrentUser.UserId)
                .Include(x => x.AssignedBy).ToListAsync(),
        };
    }

    public async Task<IResult> SaveTask(Guid id, UserTaskDto dto)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (id == Guid.Empty)
        {
            var healthCares = dto.SelectedHealthCares.ToList();

            var task = mapper.Map<UserTask>(dto);
            task.Id = Guid.NewGuid();
            task.CreatedAt = DateTime.Now;
            task.ClinicId = ApplicationState.Auth.CurrentUser.ClinicId;

            if (healthCares.Count == 0)
            {
                //Personal Tasks
                task.UserId = ApplicationState.Auth.CurrentUser.UserId;
                await context.UserTasks.AddAsync(task);
            }
            else
            {
                //Assigning Task to others
                foreach (var item in healthCares)
                {
                    task.UserId = item.Id;
                    task.AssignedById = ApplicationState.Auth.CurrentUser.UserId;
                    await context.UserTasks.AddAsync(task);
                }
            }

            await context.SaveChangesAsync();
        }
        else
        {
            var taskInDb = await context.UserTasks.FirstOrDefaultAsync(x => x.Id == id);
            if (taskInDb == null)
                return await Result.FailAsync("Task not found.");

            taskInDb = mapper.Map(dto, taskInDb);
            context.UserTasks.Update(taskInDb);
            await context.SaveChangesAsync();
        }

        return await Result.SuccessAsync("Task has been saved.");
    }

    public async Task<IResult> UpdateTaskStatus(Guid id, string status)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var taskInDb = await context.UserTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (taskInDb == null)
            return await Result.FailAsync("Task not found.");
        taskInDb.Status = status;
        context.UserTasks.Update(taskInDb);
        await context.SaveChangesAsync();
        context.Entry(taskInDb).State = EntityState.Detached;
        return await Result.SuccessAsync("Task has been updated.");
    }

    public async Task<UserTaskDto> GetTask(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var taskInDb = await context.UserTasks.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        var s = mapper.Map<UserTaskDto>(taskInDb);
        return s;
    }

    public async Task<IResult> DeleteTask(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var taskInDb = await context.UserTasks.FirstOrDefaultAsync(x => x.Id == id);
        if (taskInDb == null)
            return await Result.FailAsync("Task not found.");
        context.UserTasks.Remove(taskInDb);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync("Task has been deleted.");
    }

    #endregion

    #region Sms

    public async Task<IResult> SendBulkSms(BulkSmsDto request)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var sendRequest = new SmsRequest()
            {
                Content = request.Content,
                To = request.Patients.Select(x => x.MobilePhone).ToList()
            };
            var res = await smsEndpoints.SendMessage(sendRequest);
            if (res.ResponseCode == 202)
            {
                foreach (var item in request.Patients)
                {
                    var smsHistory = new SmsHistory()
                    {
                        Date = DateTime.Now,
                        PatientId = item.Id,
                        Mobile = item.MobilePhone,
                        Content = request.Content
                    };
                    await context.PatientSmsHistories.AddAsync(smsHistory);
                }

                await context.SaveChangesAsync();
                await Result.SuccessAsync();
                return await Result.SuccessAsync($"You Bulk SMS message has sent to {request.Patients.Count} Patients.");
            }

            return await Result.FailAsync("Message sending has failed.");
        }
        catch (Exception e)
        {
            await Result.FailAsync(e.Message);
        }

        return await Result.FailAsync();
    }

    public async Task<IResult> SendSms(SendMessageDto request)
    {
        try
        {
            
            var util = PhoneNumberUtil.GetInstance();
            var s = util.Parse(request.Mobile, ApplicationConstants.AppRegion);
            var n = util.Format(s, PhoneNumberFormat.E164);
            var sendRequest = new SmsRequest()
            {
                Content = request.Content,
                To = [n]
            };
            var res = await smsEndpoints.SendMessage(sendRequest);
            if (res.ResponseCode == 202)
            {
                var smsHistory = new SmsHistory()
                {
                    Date = DateTime.Now,
                    Mobile = request.Mobile,
                    PatientId = request.PatientId,
                    Content = request.Content
                };
                await AddMessageInPatietnHistory(smsHistory);
                await Result.SuccessAsync();
            }
            else
            {
                return await Result.FailAsync(res.Messages.First().ErrorDescription.ToString());
            }
        }
        catch (Exception e)
        {
            await Result.FailAsync(e.Message);
        }

        return await Result.FailAsync("Something went wrong.");
    }

    public async Task<List<SmsHistory>> GetSmsHistory(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.PatientSmsHistories.Where(x => x.PatientId == patientId)
            .ToListAsync();
    }

    public async Task<int> GetSmsHistoryCount(Guid patientId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.PatientSmsHistories.CountAsync(x => x.PatientId == patientId);
    }

    public  Task<List<SmsHistory>> GetSmsHistory(Guid patientId, DateTime from, DateTime to)
    {
        throw new NotImplementedException();
    }

    public async Task<List<SmsHistory>> FilterSmsHistory(Guid patientId, DateTime from, DateTime to)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.PatientSmsHistories
            .Where(x => x.PatientId == patientId && x.Date >= from && x.Date <= to)
            // .Where(m => m.Content.Contains(message, StringComparison.OrdinalIgnoreCase))
            .ToListAsync();
    }

    public async Task<IResult> AddMessageInPatietnHistory(SmsHistory smsHistory)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        await context.PatientSmsHistories.AddAsync(smsHistory);
        await context.SaveChangesAsync();
        return await Result.SuccessAsync();
    }

    #endregion

    #region Instant Messaging

    public async Task<List<UserResponseDto>> GetUsers()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var users = await context.UserClinics
            .AsNoTracking()
            .Where(x => x.ClinicId == ApplicationState.Auth.CurrentUser.ClinicId)
            .Select(x => x.User)
            .ToListAsync();
        var allUsers = users.Where(user => user.Id != ApplicationState.Auth.CurrentUser.UserId).ToList();

        return mapper.Map<List<UserResponseDto>>(allUsers);
    }

    public async Task<IResult> SaveMessageAsync(ChatMessage message)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            message.CreatedDate = DateTime.Now;
            message.FromUserId = message.FromUserId;
            message.ToUserId = message.ToUserId;
            await context.ChatMessages.AddAsync(message);
            await context.SaveChangesAsync();

            await AddChatHistory(message.Id);

            return await Result.SuccessAsync("");
        }
        catch (Exception e)
        {
            return await Result.FailAsync(e.Message);
        }
    }

    private async Task AddChatHistory(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var chatInDb = await context
            .ChatMessages
            .Include(a => a.FromUser)
            .Include(a => a.ToUser)
            .FirstOrDefaultAsync(x => x.Id == id);
        var chatHistoryList = new List<ChatHistory>()
        {
            new ChatHistory
            {
                Date = DateTime.Now,
                Message = chatInDb.Message,
                UserId = chatInDb.FromUserId,
                Type = "Sent",
                UserName = chatInDb.ToUser.FullName,
            },
            new ChatHistory
            {
                Date = DateTime.Now,
                Message = chatInDb.Message,
                UserId = chatInDb.ToUserId,
                Type = "Received",
                UserName = chatInDb.FromUser.FullName,
            }
        };
        await context.ChatHistories.AddRangeAsync(chatHistoryList);
        await context.SaveChangesAsync();
    }

    public async Task<List<ChatMessage>> GetConversationAsync(Guid contactId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var userId = ApplicationState.Auth.CurrentUser.UserId;

        var chats = context.ChatMessages
            .Where(h => (h.FromUserId == contactId && h.ToUserId == userId) ||
                        (h.FromUserId == userId && h.ToUserId == contactId))
            .OrderBy(a => a.CreatedDate)
            .Include(a => a.FromUser)
            .Include(a => a.ToUser)
            .AsNoTracking()
            .Select(x => new ChatMessage
            {
                FromUserId = x.FromUserId,
                Message = x.Message,
                CreatedDate = x.CreatedDate,
                Id = x.Id,
                ToUserId = x.ToUserId,
                ToUser = x.ToUser,
                FromUser = x.FromUser
            }).ToList();

        return chats;
    }

    public async Task<List<ChatHistoryDto>> GetConversationHistoryAsync(Guid userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var messages = await context.ChatHistories
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(c => new ChatHistoryDto
            {
                Date = c.Date,
                Message = c.Message,
                Type = c.Type,
                UserName = c.UserName,
            })
            .ToListAsync();
        return messages;
    }


    public async Task<User> GetUserDetailsAsync(Guid userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId).FirstOrDefaultAsync();
    }

    #endregion
}