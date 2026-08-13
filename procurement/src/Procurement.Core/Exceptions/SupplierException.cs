using System;
using Procurement.Core.Enums;

namespace Procurement.Core.Exceptions
{
    /// <summary>The only exception type a supplier adapter should let escape — every adapter-specific error is translated into a <see cref="SupplierErrorCode"/> (spec §11).</summary>
    public class SupplierException : Exception
    {
        public SupplierErrorCode ErrorCode { get; }
        public string SupplierCode { get; }

        public SupplierException(string supplierCode, SupplierErrorCode errorCode, string message)
            : base(message)
        {
            SupplierCode = supplierCode;
            ErrorCode = errorCode;
        }

        public SupplierException(string supplierCode, SupplierErrorCode errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            SupplierCode = supplierCode;
            ErrorCode = errorCode;
        }
    }
}
