# Homework Hero

This repository contains a minimal ASP.NET Core Web API and a Blazor WebAssembly front end for managing students, teachers, conditions, homework, and homework-related student activity. The solution uses Entity Framework Core with a SQLite database for local development and is structured for code-first migrations.

## Projects
- **HomeworkHero.Api**: ASP.NET Core API exposing endpoints under `/api/*` for students, teachers, conditions, homework, actions, prompts, results, users, permissions, and login.
- **HomeworkHero.Client**: Blazor WebAssembly app that talks to the API with `HttpClient`.
- **HomeworkHero.Shared**: Shared models used by both the API and the client.

## Running locally
1. Restore dependencies and build the solution.
2. Run the API (`HomeworkHero.Api`) to expose `https://localhost:5001`.
3. Run the Blazor client (`HomeworkHero.Client`) and configure `ApiBaseUrl` if you change the API port.

## Entity Framework CLI
The repo includes a local `dotnet-ef` tool manifest so you do not need a global install. If you receive authorization errors wh
en installing tools, the bundled `NuGet.Config` limits package sources to `nuget.org`.

From the repository root:

1. Restore the local EF Core CLI:
   ```bash
   dotnet tool restore
   ```
2. Generate a migration (example):
   ```bash
   dotnet ef migrations add InitialSqlServerMigration --project HomeworkHero.Api --startup-project HomeworkHero.Api
   ```
3. Apply the migration to the configured database:
   ```bash
   dotnet ef database update --project HomeworkHero.Api --startup-project HomeworkHero.Api
   ```

Default authentication is a lightweight email/password login stored in the `Users` table with roles for students, teachers, and admins. Use the `/api/users` endpoint to create accounts (passwords are SHA-256 hashed), then sign in from the `/login` page in the Blazor client.

HTTPS is enabled for the API via the `launchSettings.json` profile; update certificates as needed for your environment.
