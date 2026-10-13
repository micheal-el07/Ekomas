using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Xunit;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public AuthTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class RegisterTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public RegisterTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_new_user_returns_201()
    {
        var request = new RegisterRequest
        {
            Name = "Mike",
            Email = "test@a.com",
            Password = "password@123"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

}

public class LoginTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public LoginTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_user_returns_200()
    {
        // Arrange: Create the user first
        var registerRequest = new RegisterRequest
        {
            Name = "Mike",
            Email = $"test-{Guid.NewGuid()}@a.com",
            Password = "password@123"
        };

        var registerResponse = await _client.PostAsJsonAsync(
            "/auth/register",
            registerRequest
        );

        Assert.Equal(
            HttpStatusCode.Created,
            registerResponse.StatusCode
        );

        // Act: Login using the registered user
        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        var response = await _client.PostAsJsonAsync(
            "/auth/login",
            loginRequest
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }
}