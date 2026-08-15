using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

public sealed class StartupValidationTests
{
    [Fact]
    public void InvalidOptionsFailThroughTheStandardStartupValidator()
    {
        var services = new ServiceCollection();
        services.AddBs2Client(_ => { });

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IStartupValidator>();

        Should.Throw<OptionsValidationException>(validator.Validate);
    }

    [Fact]
    public void ValidOptionsPassThroughTheStandardStartupValidator()
    {
        var services = new ServiceCollection();
        services.AddBs2Client(options => { options.ClientId = "client"; options.ClientSecret = "secret"; });

        using var provider = services.BuildServiceProvider();

        Should.NotThrow(provider.GetRequiredService<IStartupValidator>().Validate);
    }
}
