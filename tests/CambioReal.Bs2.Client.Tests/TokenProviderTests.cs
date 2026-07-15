using System.Net;
using CambioReal.Bs2.Auth;
using CambioReal.Bs2.Http;
using CambioReal.Bs2.Tests.Fakes;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace CambioReal.Bs2.Tests;

public sealed class TokenProviderTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthenticatesOnceAndCachesTheToken()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"));

        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-1");
        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-1");

        transport.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AuthRequestIsFormUrlEncodedWithoutBasicHeader()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"));

        await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null);

        var request = transport.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://apihmz.bancobonsucesso.com.br/auth/oauth/v2/token");

        // Confirmado no legado: credenciais sempre no corpo form-urlencoded, nunca em
        // Authorization: Basic (diferente da Ripple).
        request.Authorization.ShouldBeNull();
        request.Body.ShouldNotBeNull();
        request.Body!.ShouldContain("grant_type=client_credentials");
        request.Body!.ShouldContain("client_id=client-1");
        request.Body!.ShouldContain("client_secret=secret-1");
        request.Body!.ShouldContain("scope=pix.cambio.collection.order");
    }

    [Fact]
    public async Task DifferentScopesAuthenticateAndCacheIndependently()
    {
        var (provider, transport) = Build(
            new MutableTimeProvider(Epoch), TokenResponse("tok-collection"), TokenResponse("tok-payment"));

        var collectionToken = await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null);
        var paymentToken = await provider.GetAccessTokenAsync(Bs2Scope.PaymentOrder, null);

        collectionToken.Token.ShouldBe("tok-collection");
        paymentToken.Token.ShouldBe("tok-payment");
        transport.Requests.Count.ShouldBe(2);
        transport.Requests[0].Body!.ShouldContain("scope=pix.cambio.collection.order");
        transport.Requests[1].Body!.ShouldContain("scope=pix.cambio.payment.order");

        // Reusa o cache por escopo — nenhuma chamada extra.
        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-collection");
        (await provider.GetAccessTokenAsync(Bs2Scope.PaymentOrder, null)).Token.ShouldBe("tok-payment");
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RenewsAfterExpiryMinusSkew()
    {
        var clock = new MutableTimeProvider(Epoch);
        var (provider, transport) = Build(clock, TokenResponse("tok-1"), TokenResponse("tok-2"));

        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-1");

        // expires_in = 3600, skew = 60 → o token vale até Epoch + 3540s.
        clock.Advance(TimeSpan.FromSeconds(3539));
        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-1");

        clock.Advance(TimeSpan.FromSeconds(2));
        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-2");

        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task InvalidatedTokenForcesRenewalEvenIfNotExpired()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"), TokenResponse("tok-2"));

        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null)).Token.ShouldBe("tok-1");
        (await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, "tok-1")).Token.ShouldBe("tok-2");

        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ConcurrentInvalidationsShareASingleRefresh()
    {
        var (provider, transport) = Build(new MutableTimeProvider(Epoch), TokenResponse("tok-1"), TokenResponse("tok-2"));

        await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null);

        var refreshed = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, "tok-1").AsTask()));

        refreshed.ShouldAllBe(result => result.Token == "tok-2");
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FailedAuthenticationThrows()
    {
        var transport = new RecordingHttpMessageHandler();
        transport.RespondWith(HttpStatusCode.Forbidden, """{"error":"invalid_client","error_description":"invalid client"}""");

        var provider = NewProvider(transport, TestClient.NewOptions(), new MutableTimeProvider(Epoch));

        var error = await Should.ThrowAsync<Bs2AuthenticationException>(
            async () => await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null));

        error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DefaultsToBearerWhenTokenTypeMissing()
    {
        var (provider, _) = Build(
            new MutableTimeProvider(Epoch),
            """{"access_token":"tok-1","expires_in":1680}""");

        var result = await provider.GetAccessTokenAsync(Bs2Scope.CollectionOrder, null);
        result.TokenType.ShouldBe("Bearer");
    }

    private static string TokenResponse(string token, int expiresIn = 3600) =>
        $$"""{"access_token":"{{token}}","token_type":"Bearer","expires_in":{{expiresIn}},"scope":"pix.cambio.collection.order"}""";

    private static (IBs2TokenProvider Provider, RecordingHttpMessageHandler Transport) Build(
        TimeProvider clock,
        params string[] responses)
    {
        var transport = new RecordingHttpMessageHandler();

        foreach (var response in responses)
        {
            transport.RespondWith(HttpStatusCode.OK, response);
        }

        return (NewProvider(transport, TestClient.NewOptions(), clock), transport);
    }

    private static Bs2TokenProvider NewProvider(RecordingHttpMessageHandler transport, Bs2Options options, TimeProvider clock) =>
        new(new SingleHandlerHttpClientFactory(transport, options.ResolveBaseAddress()), Options.Create(options), clock);
}
