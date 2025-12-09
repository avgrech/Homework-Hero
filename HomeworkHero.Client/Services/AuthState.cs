using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HomeworkHero.Client.Services;

public class AuthState
{
    private const string StorageKey = "homeworkhero.auth";
    private readonly IJSRuntime jsRuntime;

    public AuthState(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    public bool IsAuthenticated { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        var storedValue = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return;
        }

        var snapshot = JsonSerializer.Deserialize<AuthSnapshot>(storedValue);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Email) || string.IsNullOrWhiteSpace(snapshot.Role))
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            return;
        }

        IsAuthenticated = true;
        Email = snapshot.Email;
        Role = snapshot.Role;
        NotifyStateChanged();
    }

    public async Task SetUserAsync(string email, string role)
    {
        IsAuthenticated = true;
        Email = email;
        Role = role;

        await PersistAsync();
        NotifyStateChanged();
    }

    public async Task ClearAsync()
    {
        IsAuthenticated = false;
        Email = null;
        Role = null;

        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        NotifyStateChanged();
    }

    private async Task PersistAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Role))
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new AuthSnapshot(Email, Role));
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, payload);
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    private record AuthSnapshot(string Email, string Role);
}
