﻿namespace Core.Users
{
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Abp.Extensions;
    using Common.Extensions;
    using Dto;
    using Models;
    using NLog;
    using ServiceStack.Text;

    public class UserManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Returns a list of all the users from Active Directory.
        /// </summary>
        /// <returns></returns>
        private List<LicenseUser> AllUsers()
        {
            var localUsers = new List<LicenseUser>();

            try
            {
                var context = new PrincipalContext(ContextType.Domain);
                Logger.Debug("Context created.");

                // get the groups first

                using (var groupSearch = new PrincipalSearcher(new GroupPrincipal(context)))
                {
                    Logger.Debug("Searching groups.");

                    var groups = new List<GroupPrincipalOutput>();
                    foreach (Principal found in groupSearch.FindAll())
                    {
                        try
                        {
                            var adGroup = found as GroupPrincipal;

                            if (adGroup?.Guid == null)
                            {
                                continue;
                            }
                            if (adGroup.IsSecurityGroup == null || (bool) !adGroup.IsSecurityGroup)
                            {
                                continue;
                            }

                            var members = new List<Guid>();
                            try
                            {
                                members = adGroup.Members.Where(m => m.Guid != null).Select(m => (Guid) m.Guid).ToList();
                            }
                            catch (PrincipalOperationException ex)
                            {
                                Logger.Error($"Failed to get members from group: {(adGroup.DisplayName.IsNullOrEmpty() ? adGroup.SamAccountName : adGroup.DisplayName)}. " +
                                    "Most likely cause is that the group contains members which are no longer part of the domain. " +
                                    "Please investigate the group members in Active Directory.");
                                Logger.Debug(ex);
                            }

                            groups.Add(new GroupPrincipalOutput
                            {
                                Id = (Guid) adGroup.Guid,
                                Members = members,
                                Name = adGroup.Name,
                                WhenCreated = DateTime.Parse(adGroup.GetProperty("whenCreated"))
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error processing: {found.Guid.Dump()}. Please use the following powershell command to find the group. Get-ADGroup -Identity {found.Guid}");
                            Logger.Debug(ex);
                        }
                    }

                    // then process the users
                    using (var userSearch = new PrincipalSearcher(new UserPrincipal(context)))
                    {
                        Logger.Debug("Searching users.");

                        List<UserPrincipal> users = userSearch.FindAll()
                            .Cast<UserPrincipal>()
                            .Where(u => u.Guid != null)
                            .ToList();

                        foreach (UserPrincipal user in users)
                        {
                            var dirEntry = user.GetUnderlyingObject() as DirectoryEntry;

                            try
                            {
                                localUsers.Add(new LicenseUser
                                {
                                    DisplayName = user.DisplayName,
                                    Email = user.EmailAddress,
                                    Enabled = !dirEntry.IsAccountDisabled(),
                                    FirstName = user.GivenName,
                                    Groups = groups.Where(g => g.Members.Any(m => m == user.Guid))
                                        .Select(g => new LicenseGroup
                                        {
                                            Id = g.Id,
                                            Name = g.Name,
                                            WhenCreated = g.WhenCreated
                                        }).ToList(),
                                    Id = Guid.Parse(user.Guid.ToString()),
                                    LastLoginDate = user.LastLogon,
                                    SamAccountName = user.SamAccountName,
                                    Surname = user.Surname,
                                    WhenCreated = DateTime.Parse(user.GetProperty("whenCreated"))
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"There was an error getting: {user.DisplayName}");
                                Logger.Debug(ex);
                            }
                        }
                    }
                }
            }
            catch (COMException ex)
            {
                Logger.Error("There was a problem getting the users from Active Directory");
                Logger.Debug(ex);
                return new List<LicenseUser>();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                Logger.Debug(ex);
                return new List<LicenseUser>();
            }

            return localUsers;
        }

        public List<LicenseUser> GetUsersAndGroups()
        {
            return AllUsers();
        }
    }
}