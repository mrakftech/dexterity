using Services.Features.Appointments.Dtos;
using Services.Features.Appointments.Service;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace Dexterity.Adapters
{
    public class AppointmentDataAdaptor(IAppointmentService unitOfWork) : DataAdaptor
    {
        private List<GetAppointmentDto> _eventData;

        //Performs Read operation
        public override async Task<object> ReadAsync(DataManagerRequest dataManagerRequest, string key = null)
        {
            var @params = dataManagerRequest.Params;
            var start = DateTime.Parse((string)@params["StartDate"]);
            var end = DateTime.Parse((string)@params["EndDate"]);
            _eventData = await unitOfWork.GetAllAppointments(start, end);
            return dataManagerRequest.RequiresCounts ? new DataResult() { Result = _eventData, Count = _eventData.Count() } : _eventData;
        }

        //Performs Insert operation
        public override async Task<object> InsertAsync(DataManager dataManager, object data, string key)
        {
            await unitOfWork.CreateAppointment(data as UpsertAppointmentDto );
            return data;
        }

        //Performs Update operation
        public override async Task<object> UpdateAsync(DataManager dataManager, object data, string keyField, string key)
        {
            await unitOfWork.UpdateAppointment(data as UpsertAppointmentDto );
            return data;
        }

        //Performs Delete operation
        public override async Task<object> RemoveAsync(DataManager dataManager, object data, string keyField, string key)
        {
            var id = (Guid)data;
            await unitOfWork.DeleteAppointment(id);
            return data;
        }
        //Performs Batch update operations
        public override async Task<object> BatchUpdateAsync(DataManager dataManager, object changedRecords, object addedRecords, object deletedRecords, string keyField, string key, int? dropIndex)
        {
            var records = deletedRecords;
            if (deletedRecords is List<UpsertAppointmentDto> deleteData)
            {
                foreach (var data in deleteData)
                {
                    await unitOfWork.DeleteAppointment(data.Id);
                }
            }

            if (addedRecords is List<UpsertAppointmentDto> addData)
            {
                foreach (var data in addData)
                {
                    await unitOfWork.CreateAppointment(data);
                    records = addedRecords;
                }
            }

            if (changedRecords is List<UpsertAppointmentDto> updateData)
            {
                foreach (var data in updateData)
                {
                    await unitOfWork.UpdateAppointment(data);
                    records = changedRecords;
                }
            }
            return records;
        }
    }
}
