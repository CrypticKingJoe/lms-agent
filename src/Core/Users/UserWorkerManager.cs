﻿namespace LMS.Users
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Abp.Logging;
    using Common.Extensions;
    using Common.Interfaces;
    using Common.Managers;
    using Dto;
    using global::Hangfire.Console;
    using global::Hangfire.Server;
    using Managers;
    using Models;
    using OData;
    using Portal.LicenseMonitoringSystem.Users.Entities;
    using Startup;

    public class UserWorkerManager : WorkerManagerBase, IUserWorkerManager
    {
        private readonly IActiveDirectoryManager _activeDirectoryManager;
        private readonly IGroupManager _groupManager;
        private readonly IManagedSupportManager _managedSupportManager;
        private readonly IPortalManager _portalManager;
        private readonly IUserGroupManager _userGroupManager;
        private readonly IUserManager _userManager;
        private readonly IStartupManager _startupManager;

        public UserWorkerManager(
            IPortalManager portalManager,
            IActiveDirectoryManager activeDirectoryManager,
            IUserManager userManager,
            IGroupManager groupManager,
            IManagedSupportManager managedSupportManager,
            IUserGroupManager userGroupManager,
            IStartupManager startupManager
        )
        {
            _portalManager = portalManager;
            _activeDirectoryManager = activeDirectoryManager;
            _userManager = userManager;
            _groupManager = groupManager;
            _managedSupportManager = managedSupportManager;
            _userGroupManager = userGroupManager;
            _startupManager = startupManager;
        }

        public void ProcessGroups(PerformContext performContext, ManagedSupport managedSupport)
        {
            Console.WriteLine(Environment.NewLine);
            Logger.Info(performContext,"--------------- PROCESS GROUPS BEGIN ---------------");

            IEnumerable<LicenseGroupDto> groups = _activeDirectoryManager.GetGroups(performContext);
            List<LicenseGroupSummary> remoteGroups = _portalManager.ListAllGroupIds();
            var localGroupIds = new List<Guid>();
            foreach (LicenseGroupDto group in groups)
            {
                localGroupIds.Add(group.Id);

                bool existingGroup = remoteGroups.Any(ru => ru.Id == group.Id);
                if (existingGroup)
                {
                    _groupManager.Update(performContext, group);
                    continue;
                }

                _groupManager.Add(performContext, group, managedSupport.TenantId);
            }

            List<LicenseGroupSummary> activeRemoteGroups = _portalManager.ListAllGroupIds(g => !g.IsDeleted);
            IEnumerable<LicenseGroupSummary> groupsToDelete = activeRemoteGroups.Where(ru => localGroupIds.All(u => u != ru.Id));
            foreach (LicenseGroupSummary group in groupsToDelete)
            {
                _groupManager.Delete(performContext, group.Id);
            }

            Logger.Info(performContext,"--------------- PROCESS GROUPS END ---------------");
        }

        public void ProcessUserGroups(PerformContext performContext)
        {
            Console.WriteLine(Environment.NewLine);
            Logger.Info(performContext,"--------------- PROCESS GROUP MEMBERSHIP BEGIN ---------------");

            IEnumerable<LicenseGroupDto> groups = _activeDirectoryManager.GetGroups(performContext);
            foreach (LicenseGroupDto group in groups)
            {
                Logger.Info(performContext,$"** {group.Name} **");
                LicenseGroupUsersDto localMembers = _activeDirectoryManager.GetGroupMembers(performContext, group.Id);

                _userGroupManager.AddUsersToGroup(performContext, localMembers);
                _userGroupManager.DeleteUsersFromGroup(performContext, localMembers);
            }

            Logger.Info(performContext,"--------------- PROCESS GROUP MEMBERSHIP END ---------------");
        }

        /// <summary>
        ///     Decides whether a License User object should be Added, Updated or Deleted from the API.
        /// </summary>
        /// <param name="performContext"></param>
        /// <param name="managedSupport"></param>
        public void ProcessUsers(PerformContext performContext, ManagedSupport managedSupport)
        {
            Logger.Info(performContext, "--------------- PROCESS USERS BEGIN ---------------");

            IEnumerable<LicenseUserDto> users = _activeDirectoryManager.GetUsers(performContext);
            List<LicenseUserSummary> remoteUsers = _portalManager.ListAllUserIds();
            var localUserIds = new List<Guid>();
            foreach (LicenseUserDto user in users)
            {
                localUserIds.Add(user.Id);

                bool existingUser = remoteUsers.Any(ru => ru.Id == user.Id);
                if (existingUser)
                {
                    _userManager.Update(performContext, user);
                    continue;
                }

                _userManager.Add(performContext, user, managedSupport.Id, managedSupport.TenantId);
            }

            List<LicenseUserSummary> activeRemoteUsers = _portalManager.ListAllUserIds(u => !u.IsDeleted);
            IEnumerable<LicenseUserSummary> usersToDelete = activeRemoteUsers.Where(ru => localUserIds.All(u => u != ru.Id));
            foreach (LicenseUserSummary user in usersToDelete)
            {
                _userManager.Delete(performContext, user.Id);
            }

            Logger.Info(performContext, " ---------------PROCESS USERS END ---------------");
        }

        public override void Start(PerformContext performContext)
        {
            Execute(performContext, () =>
            {
                _startupManager.ValidateCredentials(performContext);

                Logger.Info(performContext, "Getting account details from the api.");
                ManagedSupport managedSupport = _managedSupportManager.Get() ?? _managedSupportManager.Add(performContext);
                _portalManager.Detach(managedSupport);

                ProcessUsers(performContext, managedSupport);
                ProcessGroups(performContext, managedSupport);
                ProcessUserGroups(performContext);

                // let the api know we have completed the task
                _managedSupportManager.Update(managedSupport);
            });
        }
    }
}