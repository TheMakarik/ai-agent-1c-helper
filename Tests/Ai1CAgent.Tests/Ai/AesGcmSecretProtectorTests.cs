using Ai1CAgent.Modules.Ai;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Tests.Ai;

public sealed class AesGcmSecretProtectorTests : IDisposable
{
    private readonly string _keyDirectory = Path.Join(
        Path.GetTempPath(),
        "Ai1CAgent.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string _keyFile;

    public AesGcmSecretProtectorTests()
    {
        _keyFile = Path.Join(_keyDirectory, "secret.key");
    }

    [Fact]
    public void Protect_WhenUnprotected_ReturnsOriginalPlaintext()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        const string secret = "sk-super-secret-key";

        // Act
        var protectedValue = systemUnderTests.Protect(secret);
        var unprotectResult = systemUnderTests.TryUnprotect(protectedValue, out var restored);

        // Assert
        unprotectResult.Should().BeTrue();
        restored.Should().Be(secret);
        protectedValue.Should().NotBe(secret);
    }

    [Fact]
    public void Protect_WhenCalledTwice_ProducesDifferentCiphertextWithSamePlaintext()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var first = systemUnderTests.Protect("secret");
        var second = systemUnderTests.Protect("secret");

        // Assert
        first.Should().NotBe(second);
        systemUnderTests.TryUnprotect(first, out var restoredFirst).Should().BeTrue();
        systemUnderTests.TryUnprotect(second, out var restoredSecond).Should().BeTrue();
        restoredFirst.Should().Be(restoredSecond);
    }

    [Fact]
    public void Protect_WhenKeyFileExists_ReusesStoredKey()
    {
        // Arrange
        var protectedValue = CreateSystemUnderTests().Protect("secret");
        var anotherInstance = CreateSystemUnderTests();

        // Act
        var unprotectResult = anotherInstance.TryUnprotect(protectedValue, out var restored);

        // Assert
        unprotectResult.Should().BeTrue();
        restored.Should().Be("secret");
        File.Exists(_keyFile).Should().BeTrue();
    }

    [Fact]
    public void TryUnprotect_WhenValueIsGarbage_ReturnsFalse()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var unprotectResult = systemUnderTests.TryUnprotect("not-a-base64-payload", out var restored);

        // Assert
        unprotectResult.Should().BeFalse();
        restored.Should().BeEmpty();
    }

    [Fact]
    public void TryUnprotect_WhenPayloadIsTampered_ReturnsFalse()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var protectedValue = systemUnderTests.Protect("secret");
        var payload = Convert.FromBase64String(protectedValue);
        payload[^1] ^= 0xFF;

        // Act
        var unprotectResult = systemUnderTests.TryUnprotect(Convert.ToBase64String(payload), out var restored);

        // Assert
        unprotectResult.Should().BeFalse();
        restored.Should().BeEmpty();
    }

    [Fact]
    public void Protect_WhenPlaintextIsEmpty_ReturnsEmpty()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var protectedValue = systemUnderTests.Protect(string.Empty);
        var unprotectResult = systemUnderTests.TryUnprotect(protectedValue, out var restored);

        // Assert
        protectedValue.Should().BeEmpty();
        unprotectResult.Should().BeTrue();
        restored.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, recursive: true);
    }

    private AesGcmSecretProtector CreateSystemUnderTests() =>
        new(A.Dummy<ILogger<AesGcmSecretProtector>>(), _keyFile);
}
