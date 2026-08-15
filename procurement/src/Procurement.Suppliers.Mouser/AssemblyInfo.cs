using System.Runtime.CompilerServices;

// Lets Procurement.Tests call the internal parsing helpers directly (see MouserAdapter.ParseLeadingInt/ParseCurrency).
[assembly: InternalsVisibleTo("Procurement.Tests")]
