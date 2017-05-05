namespace Core.Common.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Abp.Domain.Services;
    using Abp.Timing;
    using Extensions;
    using Models;
    using OData;
    using Simple.OData.Client;

    public class SupportUploadClient : DomainService, ISupportUploadClient
    {
        public async Task<CallInStatus> GetStatusByDeviceId(Guid deviceId)
        {
            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                return await client.For<SupportUpload>().Function("GetCallInStatus").Set(new {deviceId}).ExecuteAsScalarAsync<CallInStatus>();
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
                return CallInStatus.NotCalledIn;
            }
            catch (Exception ex)
            {
                Logger.Error($"Status: {ex.Message}");
                Logger.Error($"Failed to get the call in status for device: {deviceId}");
                Logger.Debug(ex.ToString());

                // by default return not called in, its not the end of the world if they call in twice
                return CallInStatus.NotCalledIn;
            }
        }

        public async Task<int> GetUploadIdByDeviceId(Guid deviceId)
        {
            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                return await client.For<SupportUpload>().Function("GetUploadId").Set(new {deviceId}).ExecuteAsScalarAsync<int>();
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get the upload id for device: {deviceId}");
                Logger.Debug(ex.ToString());
                throw;
            }
        }

        public async Task Update(int id)
        {
            int uploadId = await GetNewUploadId();

            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                await client.For<SupportUpload>().Key(id).Set(new
                {
                    CheckInTime = Clock.Now,
                    Hostname = Environment.MachineName,
                    Status = CallInStatus.CalledIn,
                    UploadId = uploadId
                }).UpdateEntryAsync();
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update upload: {id}");
                Logger.Debug(ex.ToString());
            }
        }

        public async Task<SupportUpload> Add(SupportUpload upload)
        {
            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                return await client.For<SupportUpload>().Set(upload).InsertEntryAsync();
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to add upload");
                Logger.Debug(ex.ToString());
                return null;
            }
        }

        public async Task<SupportUpload> Get(int id)
        {
            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                SupportUpload upload = await client.For<SupportUpload>().Key(id).Expand(s => s.Users).FindEntryAsync();
                return upload;
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to find upload: {id}");
                Logger.Debug(ex.ToString());
                return null;
            }
        }

        public async Task<List<LicenseUser>> GetUsers(int uploadId)
        {
            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                SupportUpload upload = await client.For<SupportUpload>()
                    .Key(uploadId)
                    .Expand(x => x.Users)
                    .FindEntryAsync();

                // return a new list if null, could just be the first check in
                return upload.Users ?? new List<LicenseUser>();
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get the users for upload: {uploadId}");
                Logger.Debug(ex.ToString());
                return null;
            }
        }

        public async Task<int> GetNewUploadId()
        {
            try
            {
                var client = new ODataClient(new ODataLicenseClientSettings());
                return await client.For<SupportUpload>().Function("NewUploadId").ExecuteAsScalarAsync<int>();
            }
            catch (WebRequestException ex)
            {
                ex.FormatWebRequestException();
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get a new upload id");
                Logger.Debug(ex.ToString());

                // default return from the api
                return 0;
            }
        }
    }
}