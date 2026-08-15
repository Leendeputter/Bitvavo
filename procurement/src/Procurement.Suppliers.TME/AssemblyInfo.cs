using System.Runtime.CompilerServices;

// Lets Procurement.Tests call TmeHttpClientWrapper.ComputeSignature directly (see TmeSignatureTests).
[assembly: InternalsVisibleTo("Procurement.Tests")]
