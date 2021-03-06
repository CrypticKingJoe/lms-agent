﻿namespace LMS.Core.Users.Managers
{
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.DirectoryServices.ActiveDirectory;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Security.Authentication;
    using Abp;
    using Abp.Domain.Services;
    using Abp.Extensions;
    using Abp.UI;
    using Core.Extensions;
    using Extensions;
    using global::Hangfire.Server;
    using Portal.LicenseMonitoringSystem.Users.Entities;

    public class ActiveDirectoryManager : DomainService, IActiveDirectoryManager
    {
        public LicenseUser GetUserByPrincipalName(string principalName)
        {
            return GetUser(IdentityType.UserPrincipalName, principalName);
        }

        public LicenseUser GetUser(IdentityType type, string key)
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                UserPrincipal user = UserPrincipal.FindByIdentity(principalContext, type, key);
                if (user == null)
                {
                    throw new UserFriendlyException($"Cannot find User Principal with {type} {key}");
                }

                user.Validate();

                Logger.Debug($"Retrieving {user.GetDisplayText()} from Active Directory.");

                bool enabled = GetUserStatus(user);
                DateTimeOffset? lastLogon = GetLastLogonDate(user);
                DateTimeOffset whenCreated = GetWhenCreated(user);

                if (user.Guid != null)
                {
                    return new LicenseUser
                    {
                        DisplayName = user.DisplayName,
                        Email = user.EmailAddress,
                        Enabled = enabled,
                        FirstName = user.GivenName,
                        Id = user.Guid.Value,
                        LastLoginDate = lastLogon,
                        SamAccountName = user.SamAccountName,
                        Surname = user.Surname,
                        WhenCreated = whenCreated
                    };
                }

                return null;
            }
        }

        public IEnumerable<LicenseUser> GetAllUsers()
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                using (var userPrincipal = new UserPrincipal(principalContext))
                {
                    using (var principalSearcher = new PrincipalSearcher(userPrincipal))
                    {
                        using (PrincipalSearchResult<Principal> results = principalSearcher.FindAll())
                        {
                            foreach (Principal principal in results)
                            {
                                if (!principal.Guid.HasValue)
                                {
                                    continue;
                                }

                                LicenseUser localUser = GetUserById(principal.Guid.Value);
                                if (localUser == null)
                                {
                                    continue;
                                }

                                yield return localUser;
                            }
                        }
                    }
                }
            }
        }

        public List<LicenseUser> GetAllUsersList()
        {
            return GetAllUsers().ToList();
        }

        public List<LicenseGroup> GetAllGroupsList()
        {
            return GetAllGroups().ToList();
        }

        public LicenseUser GetUserById(Guid userId)
        {
            return GetUser(IdentityType.Guid, userId.ToString());
        }

        public IEnumerable<LicenseGroup> GetAllGroups()
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                using (var groupPrincipal = new GroupPrincipal(principalContext))
                {
                    using (var principalSearcher = new PrincipalSearcher(groupPrincipal))
                    {
                        using (PrincipalSearchResult<Principal> results = principalSearcher.FindAll())
                        {
                            foreach (Principal principal in results)
                            {
                                if (principal.Guid == null)
                                {
                                    Logger.Debug($"Cannot process {principal.Name} because the Id is null. Please check this manually in Active Directory.");
                                    continue;
                                }

                                bool validId = Guid.TryParse(principal.Guid.ToString(), out Guid principalId);
                                if (!validId)
                                {
                                    Logger.Debug($"Cannot process {principal.Name} because the Id is not valid. Please check this manually in Active Directory.");
                                    continue;
                                }

                                if (!(principal is GroupPrincipal group))
                                {
                                    continue;
                                }

                                Logger.Debug($"Retrieving {group.GetDisplayText()} from Active Directory.");

                                LicenseGroup localGroup = GetGroup(principalId);
                                if (localGroup == null)
                                {
                                    continue;
                                }

                                yield return localGroup;
                            }
                        }
                    }
                }
            }
        }

        public LicenseGroup GetGroup(Guid groupId)
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                GroupPrincipal group = GetGroupPrincipal(principalContext, groupId);

                if (group.IsSecurityGroup == null)
                {
                    Logger.Warn($"Cannot tell if {group.GetDisplayText()} is a security group or not.");
                    return null;
                }

                if (!bool.TryParse(group.IsSecurityGroup.ToString(), out bool _))
                {
                    Logger.Warn($"Cannot process {group.GetDisplayText()} because the IsSecurityGroup value is not valid");
                    return null;
                }

                DateTimeOffset whenCreated;
                try
                {
                    string getWhenCreated = group.GetProperty("whenCreated");
                    if (getWhenCreated.IsNullOrEmpty())
                    {
                        throw new Exception($"WhenCreated property for {group.GetDisplayText()} is null or empty. Please make sure the service is running with correct permissions to access Active Directory.");
                    }

                    whenCreated = DateTimeOffset.Parse(getWhenCreated);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to determine the when created date for {group.GetDisplayText()}.");
                    Logger.Debug("Exception getting WhenCreated GroupPrincipal property.", ex);
                    throw new UserFriendlyException(ex.Message);
                }

                return new LicenseGroup
                {
                    Id = groupId,
                    Name = group.Name,
                    WhenCreated = whenCreated
                };
            }
        }

        public List<LicenseUserGroup> GetGroupMembers(Guid groupId)
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                GroupPrincipal group = GetGroupPrincipal(principalContext, groupId);

                var licenseGroupUsers = new List<LicenseUserGroup>();

                try
                {
                    using (PrincipalSearchResult<Principal> members = group.GetMembers())
                    {
                        if (!members.Any())
                        {
                            return licenseGroupUsers;
                        }

                        foreach (Principal principal in members)
                        {
                            UserPrincipal user;
                            try
                            {
                                user = principal.Validate();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex.Message);
                                continue;
                            }

                            if (!user.Guid.HasValue)
                            {
                                continue;
                            }

                            LicenseUser localUser = GetUserById(user.Guid.Value);
                            if (localUser == null)
                            {
                                continue;
                            }

                            licenseGroupUsers.Add(new LicenseUserGroup
                            {
                                GroupId = groupId,
                                UserId = localUser.Id
                            });
                        }

                        return licenseGroupUsers;
                    }
                }
                catch (COMException ex)
                {
                    Logger.Warn($"There was a problem getting the members of group: {groupId}");
                    Logger.Error(ex.Message, ex);
                    return licenseGroupUsers;
                }
                catch (AuthenticationException ex)
                {
                    Logger.Warn($"There was a problem getting the members of group: {groupId}");
                    Logger.Error(ex.Message, ex);
                    return licenseGroupUsers;
                }
                catch (PrincipalOperationException ex)
                {
                    Logger.Error($"Group: {group.Name} has some invalid members. This will need to be manually corrected in Active Directory.");
                    Logger.Debug(ex.Message, ex);
                    return licenseGroupUsers;
                }
            }
        }

        public bool IsOnDomain(PerformContext performContext)
        {
            try
            {
                using (new PrincipalContext(ContextType.Domain))
                {
                    return true;
                }
            }
            catch (ActiveDirectoryOperationException ex)
            {
                Logger.Debug(performContext, ex.Message, ex);
                return false;
            }
        }

        public bool IsPrimaryDomainController(PerformContext performContext)
        {
            try
            {
                Domain domain = Domain.GetCurrentDomain();
                DomainController primaryDomainController = domain.PdcRoleOwner;

                string currentMachine = $"{Environment.MachineName}.{IPGlobalProperties.GetIPGlobalProperties().DomainName}";

                return primaryDomainController.Name.Equals(currentMachine, StringComparison.OrdinalIgnoreCase);
            }
            catch (ActiveDirectoryOperationException ex)
            {
                Logger.Debug(performContext, ex.Message, ex);
                return false;
            }
        }

        private long ConvertActiveDirectoryLargeIntegerToLong(object adsLargeInteger)
        {
            try
            {
                var highPart = (int) adsLargeInteger.GetType().InvokeMember("HighPart", System.Reflection.BindingFlags.GetProperty, null, adsLargeInteger, null);
                var lowPart = (int) adsLargeInteger.GetType().InvokeMember("LowPart", System.Reflection.BindingFlags.GetProperty, null, adsLargeInteger, null);
                return highPart * ((long) uint.MaxValue + 1) + lowPart;
            }
            catch (Exception ex)
            {
                Logger.Debug("Error converting active directory DateTime to Epoch Time.", ex);
                return default(long);
            }
        }

        private GroupPrincipal GetGroupPrincipal(PrincipalContext principalContext, Guid id)
        {
            GroupPrincipal group = GroupPrincipal.FindByIdentity(principalContext, IdentityType.Guid, id.ToString());
            if (group == null)
            {
                throw new AbpException($"Cannot find Group Principal with Guid {id}");
            }

            return group;
        }

        private DateTimeOffset? GetLastLogonDate(UserPrincipal user)
        {
            try
            {
                if (!(user.GetUnderlyingObject() is DirectoryEntry dirEntry))
                {
                    return null;
                }

                if (dirEntry.Properties["lastLogon"].Value != null)
                {
                    var adDateTime = ConvertActiveDirectoryLargeIntegerToLong(dirEntry.Properties["lastLogon"].Value);
                    if (adDateTime != default(long))
                    {
                        return DateTimeOffset.FromFileTime(adDateTime);
                    }
                }

                if (DateTimeOffset.TryParse(user.LastLogon.ToString(), out DateTimeOffset lastLogonValue))
                {
                    return lastLogonValue;
                }

                Logger.Debug($"Failed to determine the last logon date for {user.GetDisplayText()}. Therefore we have to assume they have never logged on.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
                return null;
            }
        }

        private bool GetUserStatus(UserPrincipal user)
        {
            try
            {
                if (!(user.GetUnderlyingObject() is DirectoryEntry dirEntry))
                {
                    return true;
                }

                return !dirEntry.IsAccountDisabled();
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to determine the account status for {user.GetDisplayText()}. Therefore we have to assume they are Enabled.", ex);

                // always assume they are enabled
                return true;
            }
        }

        private DateTimeOffset GetWhenCreated(UserPrincipal user)
        {
            try
            {
                if (!(user.GetUnderlyingObject() is DirectoryEntry dirEntry))
                {
                    throw new AbpException($"Failed to determine the when created date for {user.GetDisplayText()}.");
                }

                if (dirEntry.Properties["whenCreated"].Value != null)
                {
                    if (DateTimeOffset.TryParse(dirEntry.Properties["whenCreated"].Value.ToString(), out DateTimeOffset whenCreated))
                    {
                        return whenCreated;
                    }
                }

                throw new AbpException($"Failed to determine the when created date for {user.GetDisplayText()}.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
                throw;
            }
        }
    }
}