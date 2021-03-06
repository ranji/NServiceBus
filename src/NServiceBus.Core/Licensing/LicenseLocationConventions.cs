namespace NServiceBus.Licensing
{
    using System;
    using System.Configuration;
    using System.IO;
    using Logging;
    using Microsoft.Win32;

    static class LicenseLocationConventions
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(LicenseLocationConventions));

        public static void StoreLicenseInRegistry(string license)
        {
            var keyPath = String.Format(@"SOFTWARE\NServiceBus\{0}", NServiceBusVersion.MajorAndMinor);

            try
            {
                using (var registryKey = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (registryKey == null)
                    {
                        var failureMessage = string.Format("CreateSubKey for HKCU '{0}' returned null. Do you have permission to write to this key", keyPath);

                        throw new Exception(failureMessage);
                    }

                    registryKey.SetValue("License", license, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                var failureMessage = string.Format("Failed to access HKCU '{0}'. Do you have permission to write to this key?", keyPath);
                Logger.Debug(failureMessage, exception);
                throw new Exception(failureMessage, exception);
            }
        }

        public static string TryFindLicenseText()
        {
            var appConfigLicenseString = ConfigurationManager.AppSettings["NServiceBus/License"];
            if (!String.IsNullOrEmpty(appConfigLicenseString))
            {
                Logger.Info(@"Using embedded license supplied via config file AppSettings/NServiceBus/License.");
                return appConfigLicenseString;
            }

            var appConfigLicenseFile = ConfigurationManager.AppSettings["NServiceBus/LicensePath"];
            if (!String.IsNullOrEmpty(appConfigLicenseFile))
            {
                if (File.Exists(appConfigLicenseFile))
                {
                    Logger.InfoFormat(@"Using license supplied via config file AppSettings/NServiceBus/LicensePath ({0}).", appConfigLicenseFile);
                    return NonLockingFileReader.ReadAllTextWithoutLocking(appConfigLicenseFile);
                }
                //TODO: should we throw if file does not exist?
                throw new Exception(string.Format("You have a configured licensing via AppConfigLicenseFile to use the file at '{0}'. However this file does not exist. Either place a valid license at this location or remove the app setting.", appConfigLicenseFile));
            }

            var localLicenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"NServiceBus\License.xml");
            if (File.Exists(localLicenseFile))
            {
                Logger.InfoFormat(@"Using license in current folder ({0}).", localLicenseFile);
                return NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFile);
            }

            var oldLocalLicenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"License\License.xml");
            if (File.Exists(oldLocalLicenseFile))
            {
                Logger.InfoFormat(@"Using license in current folder ({0}).", oldLocalLicenseFile);
                return NonLockingFileReader.ReadAllTextWithoutLocking(oldLocalLicenseFile);
            }

            var hkcuLicense = GetHKCULicense();
            if (!String.IsNullOrEmpty(hkcuLicense))
            {
                Logger.InfoFormat(@"Using embedded license found in registry [HKEY_CURRENT_USER\Software\NServiceBus\{0}\License].", NServiceBusVersion.MajorAndMinor);
                return hkcuLicense;
            }

            var hklmLicense = GetHKLMLicense();
            if (!String.IsNullOrEmpty(hklmLicense))
            {
                Logger.InfoFormat(@"Using embedded license found in registry [HKEY_LOCAL_MACHINE\Software\NServiceBus\{0}\License].", NServiceBusVersion.MajorAndMinor);
                return hklmLicense;
            }
            return null;
        }

        public static string GetHKCULicense()
        {
            using (var registryKey = Registry.CurrentUser.OpenSubKey(String.Format(@"SOFTWARE\NServiceBus\{0}", NServiceBusVersion.MajorAndMinor)))
            {
                if (registryKey != null)
                {
                    return (string) registryKey.GetValue("License", null);
                }
            }
            return null;
        }

        public static string GetHKLMLicense()
        {
            try
            {
                using (var registryKey = Registry.LocalMachine.OpenSubKey(String.Format(@"SOFTWARE\NServiceBus\{0}", NServiceBusVersion.MajorAndMinor)))
                {
                    if (registryKey != null)
                    {
                        return (string) registryKey.GetValue("License", null);
                    }
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
                //Swallow exception if we can't read HKLM
            }
            return null;
        }
    }
}