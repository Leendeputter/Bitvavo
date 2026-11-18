using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Procurement.Core.Interfaces;

namespace Procurement.Data.Locking
{
    /// <summary>
    /// <see cref="IAppLock"/> on SQL Server's sp_getapplock (web app plan, 18 nov 2026). The lock lives
    /// on its own open connection to the given database, owned by that session, and is released when
    /// the returned handle is disposed (or, if the process dies, when SQL Server drops the connection).
    /// No table or schema change needed. Desktop and web point at the same database, so they share the lock.
    /// </summary>
    public class SqlAppLock : IAppLock
    {
        private readonly string _connectionString;

        public SqlAppLock(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<IDisposable> AcquireAsync(string resource, TimeSpan timeout)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (var command = new SqlCommand("sp_getapplock", connection) { CommandType = CommandType.StoredProcedure })
                {
                    command.CommandTimeout = (int)Math.Ceiling(timeout.TotalSeconds) + 15;
                    command.Parameters.AddWithValue("@Resource", resource);
                    command.Parameters.AddWithValue("@LockMode", "Exclusive");
                    command.Parameters.AddWithValue("@LockOwner", "Session");
                    command.Parameters.AddWithValue("@LockTimeout", (int)timeout.TotalMilliseconds);
                    var result = command.Parameters.Add("@Result", SqlDbType.Int);
                    result.Direction = ParameterDirection.ReturnValue;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                    // 0 = granted, 1 = granted after waiting; negative = timeout/cancelled/deadlock/error.
                    if ((int)result.Value < 0)
                        throw new TimeoutException(
                            $"Iemand anders is hier al mee bezig ({resource}). Probeer het over een minuut opnieuw.");
                }
                return new Handle(connection, resource);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private sealed class Handle : IDisposable
        {
            private SqlConnection _connection;
            private readonly string _resource;

            public Handle(SqlConnection connection, string resource)
            {
                _connection = connection;
                _resource = resource;
            }

            public void Dispose()
            {
                var connection = _connection;
                _connection = null;
                if (connection == null) return;
                try
                {
                    using (var command = new SqlCommand("sp_releaseapplock", connection) { CommandType = CommandType.StoredProcedure })
                    {
                        command.Parameters.AddWithValue("@Resource", _resource);
                        command.Parameters.AddWithValue("@LockOwner", "Session");
                        command.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // Closing the connection below releases a session lock anyway.
                }
                finally
                {
                    connection.Dispose();
                }
            }
        }
    }
}
