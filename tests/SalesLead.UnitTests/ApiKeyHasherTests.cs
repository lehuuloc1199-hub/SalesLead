using SalesLead.Infrastructure.Security;

namespace SalesLead.UnitTests;

public sealed class ApiKeyHasherTests
{
    [Fact]
    public void Hash_IsDeterministic_ForSameInput()
    {
        // Arrange
        var key = "ingest_sk_test_123";

        // Act
        var a = ApiKeyHasher.Hash(key);
        var b = ApiKeyHasher.Hash(key);

        // Assert
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_ProducesLowercaseHexSha256()
    {
        // Arrange
        const string input = "x";

        // Act
        var h = ApiKeyHasher.Hash(input);

        // Assert
        Assert.Equal(64, h.Length);
        Assert.True(h.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public void Hash_Differs_ForDifferentInputs()
    {
        // Arrange

        // Act
        var hashA = ApiKeyHasher.Hash("a");
        var hashB = ApiKeyHasher.Hash("b");

        // Assert
        Assert.NotEqual(hashA, hashB);
    }
}
