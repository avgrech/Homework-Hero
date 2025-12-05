using System;

namespace HomeworkHero.Client.Services;

public class AuthState
{
    public bool IsAuthenticated { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }

    public event Action? OnChange;

    public void SetUser(string email, string role)
    {
        IsAuthenticated = true;
        Email = email;
        Role = role;
        NotifyStateChanged();
    }

    public void Clear()
    {
        IsAuthenticated = false;
        Email = null;
        Role = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
