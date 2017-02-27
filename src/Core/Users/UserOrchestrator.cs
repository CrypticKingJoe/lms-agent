﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Users
{
    using Common.Client;
    using Common.Extensions;
    using Models;

    public class UserOrchestrator : LicenseMonitoringBase, IUserOrchestrator
    {
        private readonly ISupportUploadClient _uploadClient;
        private readonly ILicenseUserClient _licenseUserClient;
        private readonly IUserManager _userManager;
        public UserOrchestrator(ISupportUploadClient uploadClient, ILicenseUserClient licenseUserClient, IUserManager userManager)
        {
            _uploadClient = uploadClient;
            _licenseUserClient = licenseUserClient;
            _userManager = userManager;
        }
        public async Task<int> ProcessUpload()
        {
            var deviceId = SettingManager.GetDeviceId();

            var status = await _uploadClient.GetStatusByDeviceId(deviceId);

            switch (status)
            {
                case CallInStatus.CalledIn:
                    Logger.Info("You are currently called in. Nothing to process");
                    return 0;
                case CallInStatus.NotCalledIn:
                    Logger.Error("You are not currently called in.");
                    break;
                case CallInStatus.NeverCalledIn:
                    Logger.Error("You have never called in");
                    Logger.Info("Calling in...");
                    var upload = await _uploadClient.Add(new SupportUpload
                    {
                        CheckInTime = DateTime.Now,
                        DeviceId = deviceId,
                        Hostname = Environment.MachineName,
                        IsActive = true,
                        Status = CallInStatus.CalledIn
                    });
                    Logger.Info($"Call in successfull: {upload.Id}");
                    return upload.Id;
            }

            var id = await _uploadClient.GetUploadIdByDeviceId(deviceId);
            Logger.Debug($"Upload id: {id}");
            return id;
        }

        public async Task<List<LicenseUser>> ProcessUsers(int uploadId)
        {
            var localUsers = _userManager.GetUsersAndGroups();
            Logger.Info($"{localUsers.Count} local users have been found.");

            var remoteUsers = await _uploadClient.GetUsers(uploadId);
            Logger.Info($"{remoteUsers.Count} users from the api have been found.");

            var usersToCreate = remoteUsers.FilterCreate(localUsers);
            Logger.Info($"{usersToCreate.Count} users need creating.");
            if (usersToCreate.Count > 0)
            {
                usersToCreate = usersToCreate.ApplyUploadId(uploadId);
                Logger.Debug($"Applying the upload id to the users.");

                await _licenseUserClient.Add(usersToCreate);
            }

            var usersToUpdate = remoteUsers.FilterUpdate(localUsers);
            Logger.Info($"{usersToUpdate.Count} users need updating.");
            if (usersToUpdate.Count > 0)
            {
                await _licenseUserClient.Update(usersToUpdate);
            }

            var usersToDelete = remoteUsers.FilterDelete(localUsers);
            Logger.Info($"{usersToDelete.Count} users need deleting.");
            if (usersToDelete.Count > 0)
            {
                await _licenseUserClient.Remove(usersToDelete);
            }

            return localUsers;
        }

        public async Task ProcessGroups(List<LicenseUser> users)
        {
            throw new NotImplementedException();
        }

        public async Task ProcessUserGroups(List<LicenseUser> users)
        {
            throw new NotImplementedException();
        }

        public async Task CallIn(int uploadId)
        {
            await _uploadClient.Update(uploadId);

            Logger.Info($"You are now called in.");
        }
    }
}
