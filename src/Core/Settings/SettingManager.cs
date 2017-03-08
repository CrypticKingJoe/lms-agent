﻿namespace Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Abp;
    using Abp.Dependency;
    using Abp.Extensions;
    using Abp.Threading;
    using Castle.Core.Logging;
    using Common.Client;
    using Common.Extensions;
    using Newtonsoft.Json;
    using NLog;
    using NLog.Config;
    using ILogger = Castle.Core.Logging.ILogger;
    using NullLogger = Castle.Core.Logging.NullLogger;

    public class SettingManager : ISettingManager, IShouldInitialize
    {
        private readonly string _settingFileName = "Settings.json";
        private Setting _settingFile;
        private string _settingFilePath;

        public SettingManager()
        {
            Logger = NullLogger.Instance;
        }

        public ILogger Logger { get; set; }

        public void Create(int accoundId = 0)
        {
            // guard
            if (Exists())
            {
                return;
            }

            // create new file and set account id if possible
            _settingFile = new Setting();
            if (accoundId != 0)
            {
                _settingFile.AccountId = accoundId;
            }

            string settings = JsonConvert.SerializeObject(_settingFile, Formatting.Indented);

            try
            {
                Logger.Debug("Creating the Settings file.");
                File.WriteAllText(_settingFilePath, settings);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Error("You do not have the required permissions to create the Settings file.");
                throw;
            }
            catch (Exception)
            {
                Logger.Error("A general error occurred while trying to create the Settings file.");
                throw;
            }

            Logger.Debug("Success.");
        }

        public bool Exists()
        {
            var exists = File.Exists(_settingFilePath);
            Logger.Debug(exists ? "Settings.json file found" : "Settings.json file not found");

            return exists;
        }

        public void SetLogLevel(LoggerLevel loggerLevel, string target)
        {
            switch (loggerLevel)
            {
                case LoggerLevel.Off:
                    SetLogLevel(LogLevel.Off, target);
                    break;
                case LoggerLevel.Fatal:
                    SetLogLevel(LogLevel.Fatal, target);
                    break;
                case LoggerLevel.Error:
                    SetLogLevel(LogLevel.Error, target);
                    break;
                case LoggerLevel.Warn:
                    SetLogLevel(LogLevel.Warn, target);
                    break;
                case LoggerLevel.Info:
                    SetLogLevel(LogLevel.Info, target);
                    break;
                case LoggerLevel.Debug:
                    SetLogLevel(LogLevel.Debug, target);
                    break;
            }
        }

        public int GetAccountId()
        {
            var id = GetAccountIdFromSettingFile();
            if (id != null)
            {
                return Convert.ToInt32(id);
            }
            else
            {
                var deviceId = GetDeviceId();
                return GetAccountIdFromApi(deviceId);
            }
        }

        public string GetServiceUrl()
        {
            return _settingFile?.ServiceUrl;
        }

        public void SetAccountId(int accountId)
        {
            GetSettings();

            _settingFile.AccountId = accountId;

            UpdateSettings();
        }

        public void FirstRun()
        {
            // check settings file is present
            var exists = Exists();

            if (!exists)
            {
                Create();
            }

            if (!_settingFile.ResetOnStartUp)
            {
                return;
            }

            // gets/sets device id
            var deviceId = GetDeviceId();

            if (deviceId == null)
            {
                throw new AbpException("Unable to obtain the centrastage device id. Execution cannot continue. Please update the settings.json file with the correct centrastage device id.");
            }

            SetDeviceId(deviceId);

            // gets/sets account id
            var accountId = GetAccountId();

            if (accountId == null)
            {
                throw new AbpException("Unable to obtain the autotask account id. Execution cannot continue. Please update the settings.json file with the correct autotask acccount id.");
            }

            SetAccountId(accountId);

            ResetOnStartUp(false);
        }

        public List<Monitor> GetMonitors()
        {
            List<Monitor> monitors = new List<Monitor>();
            if (_settingFile != null && _settingFile.Monitor.Any())
            {
                foreach (var monitor in _settingFile.Monitor)
                {
                    monitors.Add(monitor.ToEnum<Monitor>());
                }
            }

            return monitors;
        }

        public void SetDeviceId(Guid deviceId)
        {
            GetSettings();

            _settingFile.DeviceId = deviceId;

            UpdateSettings();
        }

        public void ResetOnStartUp(bool value)
        {
            GetSettings();

            _settingFile.ResetOnStartUp = value;

            UpdateSettings();
        }

        public Guid GetDeviceId()
        {
            GetSettings();
            var id = GetDeviceIdFromSettingFile();
            return id ?? GetDeviceIdFromRegistry();
        }

        public void ClearCache()
        {
            GetSettings();

            _settingFile.AccountId = null;
            _settingFile.DeviceId = null;

            UpdateSettings();
        }

        public LoggerLevel GetLogLevel(string target)
        {
            IList<LoggingRule> rules = LogManager.Configuration.LoggingRules;
            Regex validator = new Regex(target);

            foreach (var rule in rules.Where(r => validator.IsMatch(r.Targets[0].Name)))
            {
                if (rule.IsLoggingEnabledForLevel(LogLevel.Debug))
                {
                    return LoggerLevel.Debug;
                }

                if (rule.IsLoggingEnabledForLevel(LogLevel.Info))
                {
                    return LoggerLevel.Info;
                }

                if (rule.IsLoggingEnabledForLevel(LogLevel.Warn))
                {
                    return LoggerLevel.Warn;
                }

                if (rule.IsLoggingEnabledForLevel(LogLevel.Error))
                {
                    return LoggerLevel.Error;
                }

                if (rule.IsLoggingEnabledForLevel(LogLevel.Fatal))
                {
                    return LoggerLevel.Fatal;
                }
            }

            return LoggerLevel.Info;
        }

        public void Initialize()
        {
            _settingFilePath = AppDomain.CurrentDomain.BaseDirectory + _settingFileName;
            GetSettings();
        }

        private int GetAccountIdFromApi(Guid deviceId)
        {
            using (var client = IocManager.Instance.ResolveAsDisposable<ProfileClient>())
            {
                // ReSharper disable once AccessToDisposedClosure
                return AsyncHelper.RunSync(() => client.Object.GetAccountByDeviceId(deviceId));
            }
        }

        private int? GetAccountIdFromSettingFile()
        {
            Logger.Debug("Attempting to read the autotask account id from the settings file.");
            GetSettings();
            var id = _settingFile?.AccountId;

            if (id != null)
            {
                Logger.DebugFormat("Autotask account id is currently set to: {0}", id);
            }
            else
            {
                Logger.Debug("Autotask account id is not currently set");
            }

            return id;
        }

        private Guid GetDeviceIdFromRegistry()
        {
            byte[] id;

            try
            {
                id = Encoding.UTF8.GetBytes(RegistryExtentions.GetRegistryValue(Setting.DeviceIdKeyPath, Setting.DeviceIdKeyName).ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to obtain the centrastage device id from the registry. Please manually enter this in the settings.json file.");
                Logger.DebugFormat("Exception: ", ex);
                throw;
            }

            if (id == null)
            {
                throw new AbpException("Unable to obtain the centrastage device id from the registry. Please manually enter this in the settings.json file.");
            }

            var registryValue = Encoding.UTF8.GetString(id);
            Guid deviceId;
            bool valid = Guid.TryParse(registryValue, out deviceId);
            if (valid)
            {
                return deviceId;
            }

            throw new AbpException("Unable to validate the centrastage device id from the registry.");
        }

        private Guid? GetDeviceIdFromSettingFile()
        {
            Logger.Debug("Attempting to read the CentraStage DeviceID from the Settings file.");
            var id = _settingFile?.DeviceId;

            if (id != null)
            {
                Logger.DebugFormat("CentraStage device id is currently set to: {0}", id);
            }
            else
            {
                Logger.Debug("CentraStage device id is not currently set");
            }

            return id;
        }

        private void GetSettings()
        {
            try
            {
                Logger.Debug("Reading the Settings file.");

                bool exists = Exists();
                if (exists)
                {
                    _settingFile = JsonConvert.DeserializeObject<Setting>(File.ReadAllText(_settingFilePath));
                }
                else
                {
                    Create();
                }
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Error("You do not have the required permissions to read the Settings file.");
                throw;
            }
            catch (Exception)
            {
                Logger.Error("A general error occurred while trying to read the Settings file.");
                throw;
            }
        }

        private void SetLogLevel(LogLevel logLevel, string regex)
        {
            IList<LoggingRule> rules = LogManager.Configuration.LoggingRules;
            Regex validator = new Regex(regex);

            foreach (var rule in rules.Where(r => validator.IsMatch(r.Targets[0].Name)))
            {
                if (!rule.IsLoggingEnabledForLevel(logLevel))
                {
                    rule.EnableLoggingForLevel(logLevel);
                }
            }
        }

        private void UpdateSettings()
        {
            string settings = JsonConvert.SerializeObject(_settingFile, Formatting.Indented);

            try
            {
                Logger.Debug("Updating the Settings file...");
                File.WriteAllText(_settingFilePath, settings);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Error("You do not have the required permissions to update the Settings file.");
            }
            catch (Exception)
            {
                Logger.Error("A general error occurred while trying to update the Settings file.");
            }

            Logger.Debug("Success.");
        }

        public void LoadSettings()
        {
            Logger.Info("Loading the settings file.");
            bool settingsFileExists = Exists();

            if (!settingsFileExists)
            {
                Logger.Warn("Settings file missing.");
                Logger.Warn("Setting up a new file");
                Create();
            }

            GetSettings();
            Logger.Info("done.");
        }
    }
}