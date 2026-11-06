using BloodLink.Api.Services;

namespace BloodLink.Api.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = _hasher.HashPassword("S3cret!123");
        Assert.True(_hasher.Verify("S3cret!123", hash, salt));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var (hash, salt) = _hasher.HashPassword("S3cret!123");
        Assert.False(_hasher.Verify("wrong-password", hash, salt));
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ProducesDifferentHashes()
    {
        var first = _hasher.HashPassword("S3cret!123");
        var second = _hasher.HashPassword("S3cret!123");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}
