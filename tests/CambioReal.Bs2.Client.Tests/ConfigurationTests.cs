using CambioReal.Bs2.Tests.Fakes;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ValidOptionsPassValidation()
        => Should.NotThrow(() => TestClient.NewOptions().Validate());

    [Theory]
    [InlineData("", "secret-1")]
    [InlineData("client-1", "")]
    public void MissingRequiredFieldThrows(string clientId, string clientSecret)
    {
        var options = TestClient.NewOptions();
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void BaseAddressWithoutTrailingSlashThrows()
    {
        // Uma URI só-host (sem path) é normalizada pelo próprio Uri para AbsolutePath == "/",
        // então o caso que exercita a checagem precisa de um path explícito sem barra final.
        var options = TestClient.NewOptions();
        options.BaseAddress = new Uri("https://apihmz.bancobonsucesso.com.br/v1", UriKind.Absolute);

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ZeroPollTriesThrows()
    {
        var options = TestClient.NewOptions();
        options.CollectionOrderPollTries = 0;

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void SandboxResolvesToHomologHost()
        => Bs2Environment.Sandbox.GetBaseAddress().ToString().ShouldBe("https://apihmz.bancobonsucesso.com.br/");

    [Fact]
    public void ProductionResolvesToProductionHost()
        => Bs2Environment.Production.GetBaseAddress().ToString().ShouldBe("https://api.bs2.com/");
}
