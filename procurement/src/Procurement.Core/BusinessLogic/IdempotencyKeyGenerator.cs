namespace Procurement.Core.BusinessLogic
{
    /// <summary>Spec §10: PROC-{jaar}-{volgnummer}-{SUPPLIERCODE}.</summary>
    public static class IdempotencyKeyGenerator
    {
        public static string Generate(int year, int sequenceNumber, string supplierCode)
        {
            return $"PROC-{year}-{sequenceNumber:D6}-{supplierCode?.ToUpperInvariant()}";
        }
    }
}
