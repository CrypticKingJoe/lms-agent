namespace Core.Common.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Abp.Timing;
    using Factory;
    using Models;
    using NLog;
    using OData;
    using Simple.OData.Client;

    public class SupportUploadClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public async Task<ManagedSupport> Add(ManagedSupport upload)
        {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                return await client.For<ManagedSupport>().Set(upload).InsertEntryAsync();
        }

        public async Task<ManagedSupport> Get(int id)
        {
            try
            {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                ManagedSupport upload = await client.For<ManagedSupport>().Key(id).Expand(s => s.Users).FindEntryAsync();
                return upload;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unable to get upload with id: {id}");
                Logger.Debug(ex.ToString());
                return null;
            }
        }

        public async Task<int> GetNewUploadId()
        {
            try
            {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                return await client.For<ManagedSupport>().Function("NewUploadId").ExecuteAsScalarAsync<int>();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get a new upload id");
                Logger.Debug(ex.ToString());

                // default return from the api
                return 0;
            }
        }

        public async Task<CallInStatus> GetStatusByDeviceId(Guid deviceId)
        {
            try
            {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                return await client.For<ManagedSupport>().Function("GetCallInStatus").Set(new {deviceId}).ExecuteAsScalarAsync<CallInStatus>();
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

        public async Task<int?> GetUploadIdByDeviceId(Guid deviceId)
        {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                return await client.For<ManagedSupport>().Function("GetUploadId").Set(new {deviceId}).ExecuteAsScalarAsync<int?>();
        }

        public async Task<List<LicenseUser>> GetUsers(int uploadId)
        {
            try
            {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                ManagedSupport upload = await client.For<ManagedSupport>()
                    .Key(uploadId)
                    .Expand(x => x.Users)
                    .FindEntryAsync();

                // return a new list if null, could just be the first check in
                return upload.Users ?? new List<LicenseUser>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get the users for upload: {uploadId}");
                Logger.Debug(ex.ToString());
                return null;
            }
        }

        public async Task Update(int id)
        {
            int uploadId = await GetNewUploadId();

            string version = SettingFactory.SettingsManager().GetClientVersion();
            Logger.Debug($"Current client version: {version}");

            try
            {
                var client = new ODataClient(new ODataPortalAuthenticationClientSettings());
                await client.For<ManagedSupport>().Key(id).Set(new
                {
                    CheckInTime = Clock.Now,
                    ClientVersion = version,
                    Hostname = Environment.MachineName,
                    Status = CallInStatus.CalledIn,
                    UploadId = uploadId
                }).UpdateEntryAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update upload: {id}");
                Logger.Debug(ex.ToString());
            }
        }
    }
}