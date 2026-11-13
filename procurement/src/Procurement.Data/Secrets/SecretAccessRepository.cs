using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Procurement.Core.Models;
using Procurement.Core.Session;

namespace Procurement.Data.Secrets
{
    /// <summary>
    /// CRUD around SharedSecrets.dbo.SecretAccess — who may let this app decrypt/use which
    /// Procurement secret. Port of UniPro2026's Data/SecretAccessRepository.cs (same queries, same
    /// behavior), scoped to <see cref="AppSecretsRepository.ApplicationName"/>.
    /// </summary>
    public class SecretAccessRepository
    {
        private const string ApplicationName = AppSecretsRepository.ApplicationName;

        public List<SecretAccessEntry> GetAll()
        {
            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                return cn.Query<SecretAccessEntry>(
                    @"SELECT SecretName, UserName, CanRead, GrantedBy, GrantedDate
                      FROM dbo.SecretAccess
                      WHERE ApplicationName = @ApplicationName
                      ORDER BY SecretName, UserName",
                    new { ApplicationName }
                ).ToList();
            }
        }

        /// <summary>One upsert per row.</summary>
        public void Save(IEnumerable<SecretAccessEntry> entries)
        {
            const string sql = @"
            IF EXISTS (
                SELECT 1 FROM dbo.SecretAccess
                WHERE ApplicationName = @ApplicationName AND SecretName = @SecretName AND UserName = @UserName
            )
                UPDATE dbo.SecretAccess
                SET CanRead = @CanRead,
                    GrantedBy = @GrantedBy,
                    GrantedDate = GETDATE()
                WHERE ApplicationName = @ApplicationName AND SecretName = @SecretName AND UserName = @UserName
            ELSE
                INSERT INTO dbo.SecretAccess (ApplicationName, SecretName, UserName, CanRead, GrantedBy, GrantedDate)
                VALUES (@ApplicationName, @SecretName, @UserName, @CanRead, @GrantedBy, GETDATE())";

            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.UserName))
                        continue;

                    cn.Execute(sql, new
                    {
                        ApplicationName,
                        entry.SecretName,
                        UserName = entry.UserName.Trim(),
                        entry.CanRead,
                        GrantedBy = ProcurementSession.UserName
                    });
                }
            }
        }

        /// <summary>Copies all of fromUserName's rights to toUserName (overwrites overlapping rows, leaves toUserName's other rows alone). Returns false if fromUserName has no rights at all.</summary>
        public bool CopyAccess(string fromUserName, string toUserName, string grantedBy)
        {
            var sourceEntries = GetAll()
                .Where(e => string.Equals(e.UserName, fromUserName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!sourceEntries.Any())
                return false;

            var copies = sourceEntries.Select(e => new SecretAccessEntry
            {
                SecretName = e.SecretName,
                UserName = toUserName,
                CanRead = e.CanRead,
                GrantedBy = grantedBy
            });

            Save(copies);
            return true;
        }

        public void Delete(string secretName, string userName)
        {
            using (var cn = new SqlConnection(ProcurementSession.SharedSecretsConnectionString))
            {
                cn.Execute(
                    @"DELETE FROM dbo.SecretAccess
                      WHERE ApplicationName = @ApplicationName AND SecretName = @SecretName AND UserName = @UserName",
                    new { ApplicationName, SecretName = secretName, UserName = userName });
            }
        }
    }
}
