using GuessWord.Client.Infrastructure.Storage;
using GuessWord.Client.State;
using System.Net;
using System.Net.Http.Json;

namespace GuessWord.Client.Infrastructure.Http;

public class ApiRequestService
{
    private sealed record ApiProblem(string? Code, string? Title);

    private static readonly string[] UserStorageKeys = ["userId", "token", "login", "name"];

    private readonly HttpClient _httpClient;
    private readonly LocalStorageService _localStorageService;
    private readonly UserStateService _userStateService;

    public ApiRequestService(
        HttpClient httpClient,
        LocalStorageService localStorageService,
        UserStateService userStateService)
    {
        _httpClient = httpClient;
        _localStorageService = localStorageService;
        _userStateService = userStateService;
    }

    public async Task<TResponse?> GetAsync<TResponse>(string url, CancellationToken cancellationToken = default)
    {
        return await SendAsync<TResponse>(() => _httpClient.GetAsync(url, cancellationToken));
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest data)
    {
        return await SendAsync<TResponse>(() => _httpClient.PostAsJsonAsync(url, data), throwProblem: url.StartsWith("api/game", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TResponse?> PostAsync<TResponse>(string url)
    {
        return await SendAsync<TResponse>(() => _httpClient.PostAsync(url, content: null), throwProblem: url.StartsWith("api/game", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> PostAsync<TRequest>(string url, TRequest data)
    {
        return await SendAsync(() => _httpClient.PostAsJsonAsync(url, data));
    }

    public async Task<bool> PostAsync(string url, bool throwProblem = false)
    {
        return await SendAsync(() => _httpClient.PostAsync(url, content: null), throwProblem);
    }

    private async Task HandleUnauthorizedAsync()
    {
        await ClearSavedUserAsync();
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _userStateService.ClearUser();
    }

    private async Task<TResponse?> SendAsync<TResponse>(Func<Task<HttpResponseMessage>> sendRequest, bool throwProblem = false)
    {
        try
        {
            using var response = await sendRequest();

            if (!await EnsureAuthorizedAsync(response))
                return default;

            if (!response.IsSuccessStatusCode)
            {
                if (throwProblem &&
                    response.Content.Headers.ContentType?.MediaType == "application/problem+json")
                {
                    var error = await response.Content.ReadFromJsonAsync<ApiProblem>();
                    throw new ApiRequestException(error?.Code ?? "request_failed", error?.Title ?? "Запрос отклонён.", (int)response.StatusCode);
                }
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (ApiRequestException) { throw; }
        catch
        {
            return default;
        }
    }

    private async Task<bool> SendAsync(Func<Task<HttpResponseMessage>> sendRequest, bool throwProblem = false)
    {
        try
        {
            using var response = await sendRequest();

            if (!await EnsureAuthorizedAsync(response))
                return false;

            if (!response.IsSuccessStatusCode && throwProblem &&
                response.Content.Headers.ContentType?.MediaType == "application/problem+json")
            {
                var error = await response.Content.ReadFromJsonAsync<ApiProblem>();
                throw new ApiRequestException(error?.Code ?? "request_failed", error?.Title ?? "Запрос отклонён.", (int)response.StatusCode);
            }
            return response.IsSuccessStatusCode;
        }
        catch (ApiRequestException) { throw; }
        catch
        {
            return false;
        }
    }

    private async Task<bool> EnsureAuthorizedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return true;

        await HandleUnauthorizedAsync();
        return false;
    }

    private async Task ClearSavedUserAsync()
    {
        foreach (var key in UserStorageKeys)
        {
            await _localStorageService.RemoveItemAsync(key);
        }
    }
}
