using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using MAX50;
using Procurement.Core.Interfaces;
using Procurement.Core.Models;
using Procurement.Core.Session;

namespace Procurement.Services.Login
{
    /// <summary>
    /// The MAX side of logging in, shared by the desktop LoginForm logic and the web app (18 nov 2026):
    /// MAX's primary connection from the licence folder, the company list (ExactRMCompanies), the
    /// per-company connection, and from that this app's own databases (Unitron or Unitron_test,
    /// SharedSecrets). Same steps as UniPro2026's Login.cs.
    /// </summary>
    public static class MaxLogin
    {
        private static readonly object LogInitLock = new object();
        private static bool _logInitialized;

        /// <summary>
        /// MaxSQL reads MyLogManager.Instance() first and fails if it was never created; call once per
        /// process before any other method here. Safe to call again (no-op).
        /// </summary>
        public static void EnsureMaxLogging(string logFile, string logPath, bool debug)
        {
            lock (LogInitLock)
            {
                if (_logInitialized) return;
                MyLogManager.Create(logFile, logPath, debug).GetCurrentClassLogger();
                _logInitialized = true;
            }
        }

        public static string GetPrimaryConnectionString(string licensePath)
        {
            using (var connection = MaxSQL.GetPrimaryConnection(licensePath))
                return connection.ConnectionString;
        }

        public static IReadOnlyList<CompanyInfo> GetCompanies(string primaryConnectionString)
        {
            var companies = new List<CompanyInfo>();
            using (var connection = new SqlConnection(primaryConnectionString))
            using (var command = new SqlCommand("SELECT CompanyID, CompanyName FROM ExactRMCompanies ORDER BY CompanyID", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new CompanyInfo
                        {
                            CompanyID = int.Parse(reader["CompanyID"].ToString()),
                            CompanyName = reader["CompanyName"].ToString()
                        });
                    }
                }
            }
            return companies;
        }

        /// <summary>Builds the full session for one user and company. Throws when MAX refuses the company.</summary>
        public static UserSessionContext CreateSession(
            string licensePath, string primaryConnectionString, string userName, CompanyInfo company,
            bool testMode, string logPath, string logFile)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));

            string adminConnectionString;
            using (var primary = new SqlConnection(primaryConnectionString))
            using (var admin = MaxSQL.GetConnection(licensePath, primary, company.CompanyName))
            {
                adminConnectionString = admin.ConnectionString;
            }

            return new UserSessionContext
            {
                UserName = userName,
                CompanyId = company.CompanyID,
                CompanyName = company.CompanyName,
                TestMode = testMode,
                LicensePath = licensePath,
                LogPath = logPath,
                LogFile = logFile,
                PrimaryConnectionString = primaryConnectionString,
                AdminConnectionString = adminConnectionString,
                SharedConnectionString = ProcurementConnectionStringHelper.BuildSharedConnectionString(adminConnectionString, testMode),
                SharedSecretsConnectionString = ProcurementConnectionStringHelper.BuildSharedSecretsConnectionString(adminConnectionString)
            };
        }

        /// <summary>"DOMAIN\user" or "user" to "user", the form MAX and SecretAccess use.</summary>
        public static string ShortUserName(string identityName)
        {
            if (string.IsNullOrEmpty(identityName)) return identityName;
            var slash = identityName.LastIndexOf('\\');
            return slash >= 0 ? identityName.Substring(slash + 1) : identityName;
        }
    }

    /// <summary>MAX's own rights check (MaxSecurity.GetAccess) for a given session. Desktop wraps it as SecurityHelper.</summary>
    public static class MaxRights
    {
        public static bool HasAccess(ISessionContext session, string accessString, SecLevel requiredLevel)
        {
            using (var connection = new SqlConnection(session.AdminConnectionString))
            {
                connection.Open();
                return MaxSecurity.GetAccess(accessString, connection, null, session.UserName) >= requiredLevel;
            }
        }

        public static void DemandAccess(ISessionContext session, string accessString, SecLevel requiredLevel)
        {
            if (!HasAccess(session, accessString, requiredLevel))
                throw new UnauthorizedAccessException($"Geen rechten voor transactie: {accessString}");
        }
    }
}
