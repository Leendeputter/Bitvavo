using System.Data;
using System.Data.SqlClient;

namespace Procurement.Erp
{
    /// <summary>
    /// MAX stores part and order numbers as (var)char. AddWithValue sends a .NET string as
    /// nvarchar, and comparing an nvarchar parameter with a varchar column makes SQL Server convert
    /// the column for every row, so the index on PRTNUM/ORDNUM can't be used (19 nov 2026: the
    /// 1716-value IN lists on Part_Master/Part_Vendor took 3 to 4 seconds each). Sending varchar
    /// keeps the index seek.
    /// </summary>
    internal static class SqlParams
    {
        public static void AddVarChar(SqlCommand cmd, string name, string value)
        {
            cmd.Parameters.Add(new SqlParameter(name, SqlDbType.VarChar, 8000) { Value = (object)value ?? System.DBNull.Value });
        }
    }
}
