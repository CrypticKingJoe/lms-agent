﻿namespace LMS.Users.Managers
{
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.DirectoryServices.ActiveDirectory;
    using System.Linq;
    using System.Net.NetworkInformation;
    using Abp;
    using Abp.Domain.Services;
    using Abp.Extensions;
    using Common.Extensions;
    using Dto;
    using Extensions;
    using global::Hangfire.Server;

    public class ActiveDirectoryManager : DomainService, IActiveDirectoryManager
    {
        /// <param name="performContext"></param>
        /// <inheritdoc />
        public IEnumerable<LicenseUserDto> GetUsers(PerformContext performContext)
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
                                if (principal.Guid == null)
                                {
                                    Logger.Debug(performContext, $"Cannot process {principal.Name} because the Id is null. Please check this manually in Active Directory.");
                                    continue;
                                }

                                bool validId = Guid.TryParse(principal.Guid.ToString(), out Guid principalId);
                                if (!validId)
                                {
                                    Logger.Debug(performContext, $"Cannot process {principal.Name} because the Id is not valid. Please check this manually in Active Directory.");
                                    continue;
                                }

                                if (!(principal is UserPrincipal user))
                                {
                                    continue;
                                }

                                Logger.Debug(performContext, $"Retrieving {user.GetDisplayText()} from Active Directory.");

                                LicenseUserDto localUser = GetUser(performContext, principalId);
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

        /// <inheritdoc />
        public LicenseUserDto GetUser(PerformContext performContext, Guid userId)
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                UserPrincipal user = UserPrincipal.FindByIdentity(principalContext, IdentityType.Guid, userId.ToString());
                if (user == null)
                {
                    throw new AbpException($"Cannot find User Principal with Guid {userId}");
                }

                bool isAccountDisabled;
                try
                {
                    var dirEntry = user.GetUnderlyingObject() as DirectoryEntry;
                    isAccountDisabled = !dirEntry.IsAccountDisabled();
                }
                catch (Exception ex)
                {
                    isAccountDisabled = true;
                    Logger.Error(performContext, $"Failed to determine whether {user.GetDisplayText()} is enabled or not. Therefore we have to assumed they are enabled.");
                    Logger.Debug(performContext, "Exception getting DirectoryEntry status", ex);
                }

                DateTimeOffset? lastLogon = null;
                if (user.LastLogon != null)
                {
                    bool validLastLogon = DateTimeOffset.TryParse(user.LastLogon.ToString(), out DateTimeOffset lastLogonValue);
                    if (validLastLogon)
                    {
                        lastLogon = lastLogonValue;
                    }
                    else
                    {
                        Logger.Debug(performContext, $"Failed to determine the last logon date for {user.GetDisplayText()}. Therefore we have to assume they have never logged on.");
                    }
                }

                DateTimeOffset whenCreated;
                try
                {
                    string getWhenCreated = user.GetProperty("whenCreated");
                    if (getWhenCreated.IsNullOrEmpty())
                    {
                        throw new NullReferenceException($"WhenCreated property for {user.GetDisplayText()} is null or empty. Please make sure the service is running with correct permissions to access Active Directory.");
                    }

                    whenCreated = DateTimeOffset.Parse(getWhenCreated);
                }
                catch (NullReferenceException nullRef)
                {
                    Logger.Error(performContext, nullRef.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(performContext, $"Failed to determine the when created date for {user.GetDisplayText()}. Task cannot continue.");
                    Logger.Debug(performContext, "Exception getting WhenCreated UserPrincipal property.", ex);
                    throw;
                }

                return new LicenseUserDto
                {
                    DisplayName = user.DisplayName,
                    Email = user.EmailAddress,
                    Enabled = isAccountDisabled,
                    FirstName = user.GivenName,
                    Id = userId,
                    LastLogon = lastLogon,
                    SamAccountName = user.SamAccountName,
                    Surname = user.Surname,
                    WhenCreated = whenCreated
                };
            }
        }

        /// <param name="performContext"></param>
        /// <inheritdoc />
        public IEnumerable<LicenseGroupDto> GetGroups(PerformContext performContext)
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
                                    Logger.Debug(performContext, $"Cannot process {principal.Name} because the Id is null. Please check this manually in Active Directory.");
                                    continue;
                                }

                                bool validId = Guid.TryParse(principal.Guid.ToString(), out Guid principalId);
                                if (!validId)
                                {
                                    Logger.Debug(performContext, $"Cannot process {principal.Name} because the Id is not valid. Please check this manually in Active Directory.");
                                    continue;
                                }

                                if (!(principal is GroupPrincipal group))
                                {
                                    continue;
                                }

                                Logger.Debug(performContext, $"Retrieving {group.GetDisplayText()} from Active Directory.");

                                LicenseGroupDto localGroup = GetGroup(performContext, principalId);
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

        /// <inheritdoc />
        public LicenseGroupDto GetGroup(PerformContext performContext, Guid groupId)
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                GroupPrincipal group = GroupPrincipal.FindByIdentity(principalContext, IdentityType.Guid, groupId.ToString());
                if (group == null)
                {
                    throw new NullReferenceException($"Cannot find Group Principal with Guid {groupId}");
                }

                if (group.IsSecurityGroup == null)
                {
                    Logger.Warn($"Cannot tell if {group.GetDisplayText()} is a security group or not.");
                    return null;
                }

                bool isValidSecurityGroup = bool.TryParse(group.IsSecurityGroup.ToString(), out bool isSecurityGroup);
                if (!isValidSecurityGroup)
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
                        throw new NullReferenceException($"WhenCreated property for {group.GetDisplayText()} is null or empty. Please make sure the service is running with correct permissions to access Active Directory.");
                    }

                    whenCreated = DateTimeOffset.Parse(getWhenCreated);
                }
                catch (NullReferenceException nullRef)
                {
                    Logger.Error(performContext, nullRef.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(performContext, $"Failed to determine the when created date for {group.GetDisplayText()}. Task cannot continue.");
                    Logger.Debug(performContext, "Exception getting WhenCreated GroupPrincipal property.", ex);
                    throw;
                }

                return new LicenseGroupDto
                {
                    Id = groupId,
                    Name = group.Name,
                    WhenCreated = whenCreated
                };
            }
        }

        /// <inheritdoc />
        public LicenseGroupUsersDto GetGroupMembers(PerformContext performContext, Guid groupId)
        {
            using (var principalContext = new PrincipalContext(ContextType.Domain))
            {
                GroupPrincipal group = GroupPrincipal.FindByIdentity(principalContext, IdentityType.Guid, groupId.ToString());
                if (group == null)
                {
                    throw new NullReferenceException($"Cannot find Group Principal with Guid {groupId}");
                }

                var licenseGroupUsers = new LicenseGroupUsersDto(groupId, group.Name);

                PrincipalSearchResult<Principal> members = group.GetMembers();
                if (!members.Any())
                {
                    return licenseGroupUsers;
                }

                foreach (Principal principal in members)
                {
                    if (principal.Guid == null)
                    {
                        Logger.Debug(performContext, $"Cannot process {principal.Name} because the Id is null. Please check this manually in Active Directory.");
                        continue;
                    }

                    bool validId = Guid.TryParse(principal.Guid.ToString(), out Guid principalId);
                    if (!validId)
                    {
                        Logger.Debug(performContext, $"Cannot process {principal.Name} because the Id is not valid. Please check this manually in Active Directory.");
                        continue;
                    }

                    if (!(principal is UserPrincipal user))
                    {
                        continue;
                    }

                    LicenseUserDto localUser = GetUser(performContext, principalId);
                    if (localUser == null)
                    {
                        continue;
                    }

                    licenseGroupUsers.Users.Add(localUser);
                }

                return licenseGroupUsers;
            }
        }

        public bool IsOnDomain(PerformContext performContext)
        {
            try
            {
                using (var principalContext = new PrincipalContext(ContextType.Domain))
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
    }
}