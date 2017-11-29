﻿namespace LMS.OData
{
    using System;
    using System.Collections.Generic;
    using Abp.Domain.Services;
    using Actions;
    using Portal.LicenseMonitoringSystem.Users.Entities;
    using Portal.LicenseMonitoringSystem.Veeam.Entities;
    using Users.Models;

    public interface IPortalManager : IDomainService
    {
        List<LicenseUser> ListAllUsersByGroupId(Guid groupId);
        Container Container { get; set; }
        void AddGroup(LicenseGroup licenseGroup);
        void AddGroupToUser(LicenseUser licenseUser, LicenseGroup licenseGroup);
        void AddManagedSupport(ManagedSupport managedSupport);
        void AddUser(LicenseUser licenseUser);
        void AddVeeam(Veeam veeam);
        void DeleteGroup(Guid id);
        void DeleteGroupFromUser(LicenseUser licenseUser, LicenseGroup licenseGroup);
        void DeleteUser(Guid id);
        void Detach(object entity);
        int GenerateUploadId();
        int GetAccountIdByDeviceId(Guid deviceId);
        int GetManagedSupportId(Guid deviceId);
        List<LicenseGroupSummary> ListAllActiveGroupIds();
        List<LicenseUserSummary> ListAllActiveUserIds();
        List<LicenseGroup> ListAllGroups();
        List<LicenseUser> ListAllUsers();
        LicenseGroup ListGroupById(Guid id);
        ManagedSupport ListManagedSupportById(int id);
        LicenseUser ListUserById(Guid id);
        Veeam ListVeeamById(Guid id);
        void SaveChanges(bool isBatch = false);
        void UpdateGroup(LicenseGroup licenseGroup);
        void UpdateManagedSupport(ManagedSupport managedSupport);
        void UpdateUser(LicenseUser licenseUser);
        void UpdateVeeam(Veeam veeam);
    }
}