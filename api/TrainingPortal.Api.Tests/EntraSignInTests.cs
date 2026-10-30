using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Tests;

/// <summary>
/// Accepts tokens of the form "oid|email|name" so the account-matching rules can be
/// tested without Microsoft. EntraTokenValidatorTests covers real token validation.
/// </summary>
public sealed class FakeEntraValidator : IEntraTokenValidator
{
    public Task<EntraIdentity?> ValidateAsync(string accessToken)
    {
        var parts = accessToken.Split('|');
        return Task.FromResult(parts.Length == 3 ? new EntraIdentity(parts[0], parts[1], parts[2]) : null);
    }
}

public class EntraPortalFactory : PortalFactory
{
    protected override void Customize(IWebHostBuilder builder)
    {
        builder.UseSetting("Entra:TenantId", "contoso.onmicrosoft.com");
        builder.UseSetting("Entra:ClientId", "client-id");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEntraTokenValidator>();
            services.AddSingleton<IEntraTokenValidator, FakeEntraValidator>();
        });
    }
}

/// <summary>Microsoft sign-in only: password sign-in off, no automatic accounts.</summary>
public sealed class EntraOnlyPortalFactory : EntraPortalFactory
{
    protected override void Customize(IWebHostBuilder builder)
    {
        base.Customize(builder);
        builder.UseSetting("Auth:LocalLogin", "false");
        builder.UseSetting("Entra:AutoProvision", "false");
    }
}

file static class Http
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<HttpResponseMessage> EntraLogin(this HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/auth/entra", new { accessToken = token });
}

public sealed class EntraSignInTests(EntraPortalFactory factory) : IClassFixture<EntraPortalFactory>
{
    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private async Task<HttpClient> Admin()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "AdminPass1!" });
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>(Http.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    [Fact]
    public async Task Config_offers_both_sign_in_methods()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<AuthConfig>("/api/auth/config", Http.Json);
        Assert.True(config!.LocalLogin);
        Assert.Equal("client-id", config.Entra!.ClientId);
        Assert.Equal("https://login.microsoftonline.com/contoso.onmicrosoft.com", config.Entra.Authority);
        Assert.Equal("api://client-id/access_as_user", config.Entra.Scope);
    }

    [Fact]
    public async Task First_sign_in_creates_a_learner_and_later_sign_ins_reuse_it()
    {
        var id = Unique();
        var client = factory.CreateClient();
        var first = await (await client.EntraLogin($"{id}|{id}@contoso.com|Pat Lee")).Content.ReadFromJsonAsync<LoginResponse>(Http.Json);
        Assert.Equal(Data.UserRole.Learner, first!.User.Role);
        Assert.True(first.User.SignsInWithMicrosoft);
        Assert.False(first.User.HasPassword);

        // The portal token works for the rest of the API.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/dashboard")).StatusCode);

        // Same Microsoft account, new email in the directory: same portal account, email updated.
        var second = await (await factory.CreateClient().EntraLogin($"{id}|{id}.new@contoso.com|Pat Lee"))
            .Content.ReadFromJsonAsync<LoginResponse>(Http.Json);
        Assert.Equal(first.User.Id, second!.User.Id);
        Assert.Equal($"{id}.new@contoso.com", second.User.Email);
    }

    [Fact]
    public async Task An_account_an_admin_created_is_linked_by_email_and_keeps_its_role()
    {
        var id = Unique();
        var admin = await Admin();
        var created = await (await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = $"{id}@contoso.com", displayName = "Robin", role = "Admin", isActive = true, password = (string?)null,
        })).Content.ReadFromJsonAsync<UserDto>(Http.Json);
        Assert.False(created!.HasPassword);

        var login = await (await factory.CreateClient().EntraLogin($"oid-{id}|{id}@contoso.com|Robin K"))
            .Content.ReadFromJsonAsync<LoginResponse>(Http.Json);
        Assert.Equal(created.Id, login!.User.Id);
        Assert.Equal(Data.UserRole.Admin, login.User.Role);

        // A second Microsoft account cannot take over that email.
        var other = await factory.CreateClient().EntraLogin($"other-{id}|{id}@contoso.com|Someone");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task Deactivated_people_and_invalid_tokens_are_refused()
    {
        var id = Unique();
        var admin = await Admin();
        var created = await (await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = $"{id}@contoso.com", displayName = "Gone", role = "Learner", isActive = false, password = (string?)null,
        })).Content.ReadFromJsonAsync<UserDto>(Http.Json);
        Assert.NotNull(created);

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClient().EntraLogin($"{id}|{id}@contoso.com|Gone")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().EntraLogin("garbage")).StatusCode);
    }

    [Fact]
    public async Task Microsoft_accounts_have_no_password_to_change()
    {
        var id = Unique();
        var client = factory.CreateClient();
        var login = await (await client.EntraLogin($"{id}|{id}@contoso.com|Pat")).Content.ReadFromJsonAsync<LoginResponse>(Http.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        var res = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "x", newPassword = "whatever123" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

public sealed class EntraOnlySignInTests(EntraOnlyPortalFactory factory) : IClassFixture<EntraOnlyPortalFactory>
{
    [Fact]
    public async Task Password_sign_in_is_off()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<AuthConfig>("/api/auth/config", Http.Json);
        Assert.False(config!.LocalLogin);
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "admin@example.com", password = "AdminPass1!" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task People_not_added_by_an_admin_are_refused_and_the_seeded_admin_is_linked()
    {
        var stranger = await factory.CreateClient().EntraLogin("x1|stranger@contoso.com|Stranger");
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);

        var admin = await (await factory.CreateClient().EntraLogin("admin-oid|admin@example.com|Admin"))
            .Content.ReadFromJsonAsync<LoginResponse>(Http.Json);
        Assert.Equal(Data.UserRole.Admin, admin!.User.Role);
    }
}
