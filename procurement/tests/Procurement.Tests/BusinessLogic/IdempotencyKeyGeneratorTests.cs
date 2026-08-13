using Procurement.Core.BusinessLogic;
using Xunit;

namespace Procurement.Tests.BusinessLogic
{
    public class IdempotencyKeyGeneratorTests
    {
        [Fact]
        public void Generate_FollowsSpecFormat()
        {
            var key = IdempotencyKeyGenerator.Generate(2026, 42, "digikey");
            Assert.Equal("PROC-2026-000042-DIGIKEY", key);
        }

        [Fact]
        public void Generate_SameInputs_ProducesSameKey()
        {
            var key1 = IdempotencyKeyGenerator.Generate(2026, 1, "FARNELL");
            var key2 = IdempotencyKeyGenerator.Generate(2026, 1, "FARNELL");
            Assert.Equal(key1, key2);
        }
    }
}
