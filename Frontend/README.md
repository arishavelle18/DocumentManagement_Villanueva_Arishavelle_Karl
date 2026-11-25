# Frontend

The API is headless. Build the UI as a separate app (React/Vite, Next.js, or Blazor WebAssembly) in this folder or its own repository, and talk to the API over HTTP.

## Running against the API

1. Start the API: `dotnet run --project src/DocumentManagement.Api` (the URL is shown in the console and in `src/DocumentManagement.Api/Properties/launchSettings.json`).
2. Point the frontend at that base URL, e.g. `VITE_API_URL=https://localhost:<port>` or `NEXT_PUBLIC_API_URL=...`.
3. Add the frontend's dev origin to `AllowedOrigins` in `src/DocumentManagement.Api/appsettings.json`. Defaults:
   - `http://localhost:3000` (Next.js / CRA)
   - `http://localhost:5173` (Vite)

   For Blazor WASM, add the origin it runs on (e.g. `https://localhost:7xxx`). Origins must match exactly (scheme, host, port); wildcards are not allowed.

## Errors

All errors come back as RFC 7807 `application/problem+json`. Validation failures (400) include an `errors` object keyed by property name.

## API docs

In Development, the OpenAPI document is at `/openapi/v1.json` and the Scalar UI at `/scalar`.
