# Document Management API

> **Summary**
> - **What:** REST API to upload, list, view, download and delete documents (max 10 MB; PDF, DOCX, XLSX, TXT, PNG, JPG).
> - **Stack:** .NET 10 Minimal API · vertical slices (Carter + MediatR) · EF Core + SQL Server · files on local disk, metadata in the DB.
> - **Safety:** generated Guid file names (user input never becomes a path) · path-traversal guard · allow-listed types · RFC 7807 errors with correct status codes.
> - **Consistency:** upload saves the file first and removes it if the DB save fails · delete removes the row first and logs any orphan file.
> - **Tests:** 29 xUnit tests (integration + unit) on SQLite in-memory. Just run `dotnet test`, no database or setup needed.
> - **Run:** `dotnet ef database update --project src/DocumentManagement.Infrastructure --startup-project src/DocumentManagement.Api`, then `dotnet run --project src/DocumentManagement.Api`.

A REST API for uploading, listing, inspecting, downloading and deleting documents, built with
**C# / ASP.NET Core (.NET 10)**, **EF Core** and **SQL Server**.

File **contents** are stored on the local disk; file **metadata** is stored in the database.
Built as the Backend Developer Technical Assessment for P&A Grant Thornton.

---

## Table of contents

1. [Features](#features)
2. [Prerequisites](#prerequisites)
3. [Getting started](#getting-started)
4. [Running the tests](#running-the-tests)
5. [API reference](#api-reference)
6. [Architecture](#architecture)
7. [Projects and packages](#projects-and-packages)
8. [Component reference (each class and function)](#component-reference)
9. [Data access](#data-access)
10. [File storage](#file-storage)
11. [Validation and error handling](#validation-and-error-handling)
12. [Consistency on failure](#consistency-on-failure)
13. [Testing strategy](#testing-strategy)
14. [Assumptions and known limitations](#assumptions-and-known-limitations)
15. [AI usage](#ai-usage)

---

## Features

- Upload a document (`multipart/form-data`, max **10 MB**, allow-listed file types).
- List all documents (metadata only, never file contents).
- Get one document's metadata by id.
- Download a document's file (streamed, with its original file name).
- Delete a document (database row + stored file).
- Consistent **RFC 7807 ProblemDetails** error responses with correct status codes.
- Path-traversal-safe storage: user-supplied file names never become disk paths.
- 29 automated tests (integration + unit) that need **no SQL Server and no setup**.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** | `dotnet --version` should print `10.x` |
| SQL Server | LocalDB, Express, Developer or full | LocalDB ships with Visual Studio. Only needed to *run* the API, not the tests. |
| `dotnet-ef` CLI | 10.x | `dotnet tool install --global dotnet-ef` |

---

## Getting started

### 1. Clone

```bash
git clone https://github.com/arishavelle18/DocumentManagement_Villanueva_Arishavelle_Karl.git
cd DocumentManagement_Villanueva_Arishavelle_Karl
```

### 2. Configure the database connection

The default connection string in `src/DocumentManagement.Api/appsettings.json` points to LocalDB with
Windows authentication, so it contains **no secrets** and works out of the box on a Windows machine
with Visual Studio:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=DocumentManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

To use a different SQL Server (for example one with a username/password), **do not edit
`appsettings.json`**. Override it with User Secrets or an environment variable instead:

```bash
# Option A: User Secrets (stored outside the repo)
dotnet user-secrets init --project src/DocumentManagement.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=DocumentManagement;User Id=...;Password=...;TrustServerCertificate=True" --project src/DocumentManagement.Api

# Option B: environment variable (note the double underscore)
export ConnectionStrings__DefaultConnection="Server=...;..."
```

### 3. Create the database

The migrations live in the Infrastructure project; the API project is the startup project
(it holds the configuration and the DI registration).

```bash
dotnet ef database update \
  --project src/DocumentManagement.Infrastructure \
  --startup-project src/DocumentManagement.Api
```

This applies the `InitialCreate` migration, which creates the `Documents` table.

To add a migration after changing the model:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/DocumentManagement.Infrastructure \
  --startup-project src/DocumentManagement.Api
```

### 4. Run the API

```bash
dotnet run --project src/DocumentManagement.Api --launch-profile https
```

- API: `https://localhost:7054` (HTTPS profile) or `http://localhost:5016` (HTTP profile)
- Interactive API docs (Development only): `https://localhost:7054/scalar`
- OpenAPI document (Development only): `https://localhost:7054/openapi/v1.json`

Uploaded files are written to `src/DocumentManagement.Api/Storage/Documents/` (configurable through
`FileStorage:RootPath`). This folder is git-ignored.

### Quick try with curl

```bash
# Upload
curl -k -F "file=@./sample.pdf" https://localhost:7054/api/documents

# List
curl -k https://localhost:7054/api/documents

# Get metadata / download / delete
curl -k https://localhost:7054/api/documents/{id}
curl -k -OJ https://localhost:7054/api/documents/{id}/download
curl -k -X DELETE https://localhost:7054/api/documents/{id}
```

---

## Running the tests

```bash
dotnet test
```

No database server, no configuration and no running API are needed. Each test gets its own
SQLite in-memory database and its own temporary storage folder, both thrown away afterwards.

---

## API reference

Base path: `/api/documents`. All error responses use `application/problem+json`.

| Method | Route | Purpose | Success | Errors |
|---|---|---|---|---|
| `POST` | `/api/documents` | Upload a file (`multipart/form-data`, field name **`file`**) | `201 Created` + `Location` header | `400`, `413`, `415` |
| `GET` | `/api/documents` | List all documents' metadata | `200 OK` | — |
| `GET` | `/api/documents/{id}` | Get one document's metadata | `200 OK` | `404` |
| `GET` | `/api/documents/{id}/download` | Download the file | `200 OK` (file stream) | `404` |
| `DELETE` | `/api/documents/{id}` | Delete the row and the stored file | `204 No Content` | `404` |

`{id}` is a GUID. A non-GUID id does not match the route (`{id:guid}` constraint) and returns `404`.

### `POST /api/documents`: upload

Request: `multipart/form-data` with a single file in the `file` field.

Response `201 Created`:

```json
{
  "data": {
    "id": "0193c3a2-5b7e-7c1d-9a4f-2f1e3b8c9d10",
    "originalFileName": "report.pdf",
    "storedFileName": "6f9619ff8b86d011b42d00c04fc964ff.pdf",
    "contentType": "application/pdf",
    "fileSize": 48213,
    "uploadedAt": "2026-11-25T10:15:30.123Z"
  }
}
```

| Status | When |
|---|---|
| `400 Bad Request` | No file, empty (0-byte) file, or a malformed multipart body |
| `413 Payload Too Large` | File larger than 10 MB |
| `415 Unsupported Media Type` | Extension not in the allow-list |

### `GET /api/documents`: list

Response `200 OK`: `{ "data": [ { ...same metadata fields as above... } ] }`. File contents are never included.

### `GET /api/documents/{id}`: metadata

Response `200 OK`: `{ "data": { ...metadata... } }`. `404` if no row exists.

### `GET /api/documents/{id}/download`: download

Response `200 OK`: the raw file stream, with:
- `Content-Type`: the trusted type stored at upload time (for example `application/pdf`)
- `Content-Disposition: attachment; filename=<original file name>`

`404` if the row does not exist **or** the row exists but the physical file is missing
(the second case is also logged as a warning, because it signals an inconsistency).

### `DELETE /api/documents/{id}`: delete

Response `204 No Content`. `404` if no row exists.

### Error response example

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.14",
  "title": "File too large",
  "status": 413,
  "detail": "Maximum allowed size is 10 MB.",
  "traceId": "0HN8..."
}
```

Every ProblemDetails response includes a `traceId` so a client error can be matched to the server logs.

---

## Architecture

### Style: vertical slices

The API is organised **by feature** instead of by technical layer. Each endpoint is a self-contained
folder (a "slice") holding everything that endpoint needs:

```
Features/Documents/UploadDocument/
├── Endpoint.cs                 # HTTP: route, input binding, status codes (Carter module)
├── UploadDocumentCommand.cs    # the request message
├── UploadDocumentHandler.cs    # the business logic
└── UploadDocumentResponse.cs   # the response shape
```

**Why:** changing one feature touches one folder. There are no big `DocumentService` /
`DocumentController` classes that every feature has to share, and each handler stays small
and easy to test on its own.

### Request flow

```
HTTP request
   │
   ▼
Carter Endpoint ── validates the upload (FileValidator) ──► 400/413/415 ProblemDetails
   │
   ▼  sender.Send(command)
MediatR pipeline
   ├─ LoggingBehavior     (logs the request name + elapsed time)
   ├─ ValidationBehavior  (runs any FluentValidation validators)
   ▼
Handler ──► AppDbContext (EF Core → SQL Server)      metadata
        └─► IFileStorageService (local disk)          file bytes
   │
   ▼
IResult / response DTO ──► JSON or file stream

Any unhandled exception ──► GlobalExceptionHandler ──► ProblemDetails (400/404/413/500)
```

### Solution layout

```
DocumentManagement.slnx
├── src/
│   ├── DocumentManagement.Api/              # Host: Program.cs, DI, middleware, feature slices
│   ├── DocumentManagement.Common/           # Shared contracts: IFileStorageService, FileValidator, exceptions
│   └── DocumentManagement.Infrastructure/   # EF Core, migrations, local file storage, MediatR behaviors
├── tests/
│   └── DocumentManagement.Tests/            # xUnit integration + unit tests
└── Frontend/                                # placeholder notes for a separate UI (not part of this assessment)
```

**Dependency direction:** `Api → Infrastructure → Common` and `Api → Common`. `Common` depends on
nothing in the solution, so the storage contract (`IFileStorageService`) can be referenced
everywhere without pulling in EF Core or disk I/O.

### Why MediatR?

MediatR is used as a thin dispatcher between endpoints and handlers:

- **One standard slice shape:** every feature is `Command → Handler → Response`, which makes the codebase predictable.
- **Testable handlers:** handlers are plain classes with constructor-injected dependencies. The unit tests create them directly with a mocked `IFileStorageService` and a SQLite `AppDbContext`, with no HTTP involved.
- **Cross-cutting behaviors:** logging and validation are written once as pipeline behaviors instead of being repeated in every handler.

The trade-off is one extra level of indirection. For five endpoints the API could call handler
classes directly, but the slice structure stays the same and scales better as features are added.

### Why Carter?

Carter discovers every `ICarterModule` and calls its `AddRoutes`, so each slice registers its own
route inside its own folder and `Program.cs` only needs `app.MapCarter()`. Under the hood these are
ordinary Minimal API endpoints.

---

## Projects and packages

### `DocumentManagement.Api`: web host and feature slices

| Package | Version | Why |
|---|---|---|
| `Carter` | 10.0.0 | Per-slice Minimal API route registration (`ICarterModule`) |
| `MediatR` | 15.0.0 | Command → handler dispatch + pipeline behaviors |
| `FluentValidation.DependencyInjectionExtensions` | 12.2.0 | Auto-registers validators from the assembly |
| `Microsoft.AspNetCore.OpenApi` | 10.0.15 | Generates the OpenAPI document |
| `Scalar.AspNetCore` | 3.2.0 | Interactive API docs UI at `/scalar` (Development only) |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.15 | `dotnet ef` / Package Manager Console migration commands (design-time only) |

### `DocumentManagement.Infrastructure`: persistence, storage, behaviors

| Package | Version | Why |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.15 | EF Core provider for SQL Server |
| `MediatR` | 15.0.0 | `IPipelineBehavior` for the logging/validation behaviors |
| `FluentValidation` | 12.2.0 | Used by `ValidationBehavior` |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.15 | `ILogger<T>` in the behaviors |

### `DocumentManagement.Common`: shared contracts

| Package / reference | Version | Why |
|---|---|---|
| `Microsoft.AspNetCore.App` (framework reference) | .NET 10 | `IFormFile`, `IResult`, `ProblemDetails`, `IExceptionHandler` |
| `FluentValidation` | 12.2.0 | `ValidationException` mapping in `GlobalExceptionHandler` |

### `DocumentManagement.Tests`: automated tests

| Package | Version | Why |
|---|---|---|
| `xunit` / `xunit.runner.visualstudio` | 2.9.3 / 3.1.4 | Test framework and runner |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | `dotnet test` host |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | `WebApplicationFactory<Program>` boots the real API in memory |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.15 | In-memory relational database for tests |
| `Moq` | 4.20.72 | Mocks `IFileStorageService` to simulate storage failures |
| `FluentAssertions` | 7.2.0 | Readable assertions. Kept on 7.x because 8.x requires a paid commercial license. |
| `coverlet.collector` | 6.0.4 | Code coverage (`dotnet test --collect:"XPlat Code Coverage"`) |

---

## Component reference

### API host: `src/DocumentManagement.Api`

#### `Program.cs`
Application entry point.
- Sets Kestrel `MaxRequestBodySize` and `FormOptions.MultipartBodyLengthLimit` to **11 MB**. This server-level guard rejects abusive bodies before they are buffered. It is slightly above the 10 MB business limit so that multipart boundaries and headers fit, and the exact 10 MB rule stays in `FileValidator`.
- Calls `AddApplicationServices`, `AddCarter`, `UseApplicationMiddleware` and `MapCarter`.

#### `Extensions/ServiceCollectionExtensions.AddApplicationServices(services, configuration)`
Registers everything in the DI container:
- `AppDbContext` with SQL Server (**Scoped**, one per request). It throws at startup if `DefaultConnection` is missing.
- MediatR handlers from the API assembly, plus `LoggingBehavior` and `ValidationBehavior`.
- `FileStorageOptions` bound from the `FileStorage` section.
- `IFileStorageService → LocalFileStorageService` as a **Singleton**. This is safe because the service is stateless (its only field is a read-only root path).
- FluentValidation validators, ProblemDetails (adds `traceId` to every response), `GlobalExceptionHandler`, the CORS policy `Frontend` (origins from `AllowedOrigins`) and OpenAPI.

#### `Extensions/MiddlewareExtensions.UseApplicationMiddleware(app)`
Builds the middleware pipeline in this order: exception handler → status-code pages → HSTS
(outside Development) → HTTPS redirection → CORS → OpenAPI + Scalar (Development only).

#### Feature: `Features/Documents/UploadDocument`

| File | Responsibility |
|---|---|
| `Endpoint.AddRoutes` | Maps `POST /api/documents`. Binds `IFormFile? file`, runs `FileValidator.Validate(file)` and returns its ProblemDetails if the file is invalid. Otherwise it sends `UploadDocumentCommand` and returns `201 Created` with a `Location: /api/documents/{id}` header. Antiforgery is disabled because this is a token-less API with no cookie authentication. |
| `UploadDocumentCommand(IFormFile? File)` | MediatR request carrying the uploaded file. |
| `UploadDocumentHandler.Handle` | 1) Reads the extension from the client file name (lower-cased). 2) Streams the file to storage → gets a generated `storedFileName`. 3) Builds the `Document` entity: new `Guid.CreateVersion7()` id (time-ordered, so it is index-friendly), `Path.GetFileName` of the original name (strips any client path), **trusted** content type from the allow-list, size and `UtcNow`. 4) Saves the row. **If the save fails it deletes the stored file and rethrows**, so no orphan files are left. 5) Returns the metadata. |
| `UploadDocumentResponse` / `UploadDocumentModel` | `{ data: { id, originalFileName, storedFileName, contentType, fileSize, uploadedAt } }` |

#### Feature: `Features/Documents/ListDocuments`

| File | Responsibility |
|---|---|
| `Endpoint.AddRoutes` | Maps `GET /api/documents` and returns `200 OK`. |
| `ListDocumentsCommand()` | Parameterless MediatR request. |
| `ListDocumentsHandler.Handle` | Queries `DocumentsView` (no tracking) and **projects to `DocumentDto` in SQL**, so only metadata columns are read. |
| `ListDocumentsResponse` / `DocumentDto` | `{ data: [ ...metadata... ] }` |

#### Feature: `Features/Documents/RetrieveDocumentDetails`

| File | Responsibility |
|---|---|
| `Endpoint.AddRoutes` | Maps `GET /api/documents/{id:guid}` and returns `200 OK`. |
| `RetrieveDocumentDetailsCommand(Guid Id)` | MediatR request. |
| `RetrieveDocumentDetailsHandler.Handle` | Looks the row up with no tracking. If it is missing it throws `NotFoundException`, which `GlobalExceptionHandler` turns into `404`. |
| `RetrieveDocumentDetailsResponse` / `RetrieveDocumentDetailsModel` | `{ data: { ...metadata... } }` |

#### Feature: `Features/Documents/DownloadDocuments`

| File | Responsibility |
|---|---|
| `Endpoint.AddRoutes` | Maps `GET /api/documents/{id:guid}/download`. |
| `DownloadDocumentsCommand(Guid Id)` | MediatR request. |
| `DownloadDocumentsHandler.Handle` | 1) Looks the row up, returning `404 "Document not found"` if it is missing. 2) `storage.OpenRead(storedFileName)`. If that returns `null`, it logs a warning (the DB and the disk are out of sync) and returns `404 "File not found"`. 3) Returns `Results.File(stream, contentType, originalFileName)`, which **streams** the file (it is never fully loaded into memory) and sets `Content-Disposition` to the original name. ASP.NET disposes the stream after the response. |

#### Feature: `Features/Documents/DeleteDocuments`

| File | Responsibility |
|---|---|
| `Endpoint.AddRoutes` | Maps `DELETE /api/documents/{id:guid}`. |
| `DeleteDocumentsCommand(Guid Id)` | MediatR request. |
| `DeleteDocumentsCommandHandler.Handle` | 1) Loads the row, throwing `NotFoundException` (→ `404`) if it is missing. 2) **Removes the DB row first** and saves. 3) Deletes the file. If the file was already missing it logs a warning; if the delete throws (for example the file is locked) it logs an error with the document id and stored name for later cleanup. 4) Returns `204 No Content` in both cases, because the document no longer exists for clients. See [Consistency on failure](#consistency-on-failure). |

### Shared: `src/DocumentManagement.Common`

#### `FileStorage/IFileStorageService`
The storage contract. The handlers depend only on this interface, which makes them testable and
lets you swap local disk for blob storage later.

| Method | Contract |
|---|---|
| `Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)` | Saves the stream under a **generated** name and returns that name. |
| `Stream? OpenRead(string storedFileName)` | Opens a read stream, or returns `null` if the file does not exist. |
| `bool Delete(string storedFileName)` | Deletes the file. Returns `true` if a file was deleted and `false` if it was already missing. |

#### `FileStorage/FileStorageOptions`
`RootPath` (default `Storage/Documents`), bound from the `FileStorage` config section.

#### `FileValidator/FileValidator`
- `MaxFileSize`: 10 MB.
- `AllowedTypes`: a case-insensitive allow-list mapping **extension → trusted content type**:

  | Extension | Content type |
  |---|---|
  | `.pdf` | `application/pdf` |
  | `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
  | `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
  | `.txt` | `text/plain` |
  | `.png` | `image/png` |
  | `.jpg`, `.jpeg` | `image/jpeg` |

- `Validate(IFormFile? file)` returns `null` when the file is valid, or a ProblemDetails `IResult`: `400` (missing/empty) → `413` (too large) → `415` (extension not allowed), checked in that order.

#### `Exceptions/GlobalExceptionHandler.TryHandleAsync`
An `IExceptionHandler` that turns exceptions into ProblemDetails:

| Exception | Status |
|---|---|
| `ValidationException` (FluentValidation) | `400`, with `errors` grouped by property |
| `BadHttpRequestException` (framework: malformed form body, body over the Kestrel limit, …) | the exception's own `StatusCode` (`400` / `413`) |
| `NotFoundException` | `404` |
| anything else | `500` (details hidden from the client, full exception logged) |

5xx errors are logged as errors; 4xx errors are logged as warnings.

#### `Exceptions/NotFoundException`
Thrown by handlers when a document does not exist; mapped to `404`.

#### `Models/Result`
A generic success/failure result type from the original boilerplate. It is not used by the current features.

### Infrastructure: `src/DocumentManagement.Infrastructure`

#### `Persistence/Entities/Document`

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key (Version 7, time-ordered) |
| `OriginalFileName` | `string` (max 255) | Display/download name only. **Never used as a path.** |
| `StoredFileName` | `string` (max 100, **unique index**) | Generated `{guid}{ext}` name on disk |
| `ContentType` | `string` (max 100) | Trusted type from the allow-list, not the client's header |
| `FileSize` | `long` | Bytes |
| `UploadedAt` | `DateTime` | UTC |

#### `Persistence/AppDbContext`
- `Documents`: a tracked `DbSet<Document>` for writes.
- `DocumentsView`: `Documents.AsNoTracking()` for read-only queries (faster, with no change-tracker overhead).
- `OnModelCreating` applies every `IEntityTypeConfiguration` in the assembly.

#### `Persistence/Configurations/DocumentConfiguration`
Fluent configuration: table `Documents`, key, max lengths, required columns, and a unique index on `StoredFileName` (an extra database-level guard against name collisions).

#### `Migrations/`
`InitialCreate` creates the `Documents` table. The migrations are committed so the database can be recreated with `dotnet ef database update`.

#### `FileStorage/LocalFileStorageService`

| Member | Behaviour |
|---|---|
| constructor | Resolves `RootPath` against the app's `ContentRootPath` to an **absolute** path and creates the folder. |
| `SaveAsync` | Name = `{Guid:N}{extension}`. Opens with `FileMode.CreateNew` (**never overwrites**; it throws if the name somehow exists) and copies the stream to disk without buffering the whole file in memory. |
| `OpenRead` | Returns a `FileStream` opened with `FileShare.Read` (allows concurrent downloads), or `null` if the file is missing. |
| `Delete` | Returns `false` if the file is missing; otherwise deletes it and returns `true`. |
| `GetSafePath` (private) | **Path-traversal guard** used by every method: `Path.GetFileName` strips any `../` or folder segments, `Path.GetFullPath` normalises the result, then it checks that the result starts with `rootPath + separator`. The separator stops a sibling-folder bypass such as `Documents_Evil`. It throws `InvalidOperationException` otherwise. |

#### `Behaviors/LoggingBehavior<TRequest, TResponse>`
A MediatR pipeline step that logs `Handling {Request}` and `Handled {Request} in {ms} ms` around every handler.

#### `Behaviors/ValidationBehavior<TRequest, TResponse>`
A MediatR pipeline step that runs every registered FluentValidation validator for the request and
throws `ValidationException` (→ `400`) if any of them fail. No validators are registered for the
current commands, because uploads are validated by `FileValidator` at the endpoint, so this
behavior currently passes straight through. It is kept as the extension point for future commands.

---

## Data access

- **EF Core 10 + SQL Server.** One table, `Documents`, defined by `DocumentConfiguration` and created by the committed migration.
- **`AppDbContext` is Scoped:** its change tracker and connection are per request and not thread-safe.
- **Reads use `AsNoTracking`** (`DocumentsView`); the list endpoint also **projects to a DTO** so EF selects only the needed columns.
- **No BLOBs:** the database holds metadata only, so it stays small and fast to back up, and files are streamed directly from disk.

## File storage

- Files live under `FileStorage:RootPath` (default `src/DocumentManagement.Api/Storage/Documents`, git-ignored).
- **The user's file name never touches the disk path.** Files are stored as `{Guid:N}{ext}`; the original name is kept only in the database and used for `Content-Disposition` on download.
- **Collisions:** Guid names, `FileMode.CreateNew` (never overwrite) and a unique DB index on `StoredFileName`.
- **Path traversal:** every operation goes through `GetSafePath` (it strips directory segments and verifies the path stays inside the root). Covered by unit tests using `../../appsettings.json` and `..\..\secret.txt`.
- **Memory:** uploads and downloads are streamed, so files are never fully loaded into memory.

## Validation and error handling

Two size limits, on purpose:

1. **Server limit, 11 MB** (Kestrel + `FormOptions`): stops huge bodies before they are buffered. If it is exceeded, the framework throws `BadHttpRequestException(413)` and `GlobalExceptionHandler` returns a `413` ProblemDetails.
2. **Business rule, 10 MB** (`FileValidator`): the exact limit, with a clear message.

The content type is **derived from the allow-listed extension**, never trusted from the client's
`Content-Type` header.

---

## Consistency on failure

The database and the disk can't share one transaction, so each operation runs in the order that
leaves the **least harmful** state if its second step fails.

### Upload: file first, then DB row

1. Save the file to disk.
2. Insert the DB row.
3. **If step 2 fails:** delete the just-saved file, log the error and rethrow (→ `500`).

*Why this order:* if the order were reversed and the file write failed, a row would point to a file
that doesn't exist. With file-first, a DB failure is cleaned up straight away. Covered by
`UploadDocumentHandlerTests.Handle_SaveChangesFails_DeletesStoredFileAndRethrows`.

### Delete: DB row first, then file

1. Remove the DB row and save.
2. Delete the file.
3. **If the file is already missing:** log a warning and still return `204`.
4. **If the file delete throws:** log an error with the document id and stored file name and still return `204`.

*Why this order:* the database is the source of truth for what exists. If the row were deleted
second and that failed, clients would still see a document whose file is gone (a broken download).
With row-first, the worst case is an **orphan file** on disk: invisible to clients, harmless and
logged with enough detail to clean it up later. Covered by
`DeleteDocumentsHandlerTests` (delete throws / file already missing).

*How it would be finished in production:* a scheduled cleanup job that lists the files in the storage
root, compares them with `StoredFileName` values in the database and removes the orphans.

---

## Testing strategy

```
tests/DocumentManagement.Tests/
├── Infrastructure/
│   ├── CustomWebApplicationFactory.cs   # real API in memory, SQLite instead of SQL Server, temp storage folder
│   ├── IntegrationTestBase.cs           # fresh factory + HttpClient per test, upload helper
│   ├── SqliteTestDatabase.cs            # in-memory DB for handler tests + FailingSaveChangesInterceptor
│   └── TestFileHelper.cs                # multipart bodies: valid, >10 MB, .exe, empty, no file, malformed
├── Integration/Documents/               # real HTTP calls through the full pipeline
│   ├── UploadDocumentTests.cs
│   ├── ListDocumentsTests.cs
│   ├── RetrieveDocumentTests.cs
│   ├── DownloadDocumentTests.cs
│   └── DeleteDocumentTests.cs
└── Unit/
    ├── Handlers/                        # handlers called directly; storage mocked with Moq
    │   ├── UploadDocumentHandlerTests.cs
    │   ├── DownloadDocumentsHandlerTests.cs
    │   └── DeleteDocumentsHandlerTests.cs
    └── FileStorage/
        └── LocalFileStorageServiceTests.cs   # real temp folder, no mocks
```

**Key decisions**

- **SQLite in-memory instead of SQL Server:** the tests run on any machine with no setup. The factory removes the SQL Server registration (both `DbContextOptions<AppDbContext>` and EF Core 9+'s `IDbContextOptionsConfiguration<AppDbContext>`, otherwise two providers would be registered), keeps one `SqliteConnection` open for the factory's lifetime (an in-memory DB only lives while its connection is open) and calls `EnsureCreated` (the migrations are specific to SQL Server).
- **A clean state for every test:** xUnit creates a new test-class instance per test, and each instance builds its own factory, so every test gets a fresh database and a fresh temp storage folder, deleted on dispose. The real `Storage/` folder is never touched.
- **Simulating DB failures:** `AppDbContext` is `sealed` and can't be mocked, so a `SaveChangesInterceptor` throws `DbUpdateException` inside the real EF pipeline.
- **Checking what was persisted:** handler tests read results through a *fresh* `AppDbContext` on the same connection, so they see the database rather than the handler's change tracker.
- **Naming:** `MethodOrEndpoint_Scenario_ExpectedResult`, with Arrange / Act / Assert in every test.

**Coverage (29 tests)**

| Area | Scenarios |
|---|---|
| Upload | valid file → 201 + metadata + file on disk; >10 MB → 413; no file → 400; empty file → 400; malformed multipart → 400; `.exe` → 415 |
| Retrieve | existing → 200 with matching metadata; nonexistent → 404 |
| Download | existing → 200, correct content type, original name, identical bytes; nonexistent → 404; row exists but file missing → 404 |
| Delete | existing → 204, then GET → 404 and the file is gone; nonexistent → 404 |
| List | returns all uploads with metadata fields only (raw JSON checked: no content field); empty → 200 `[]` |
| Upload handler | DB save fails → stored file deleted + exception rethrown + no row |
| Download handler | `OpenRead` returns null → 404 problem |
| Delete handler | storage throws → row removed + 204; file already missing → row removed + 204 |
| Local storage | Guid name (not original); distinct names; open missing → null; open/delete existing; traversal (`../../`, `..\..\`, `../../appsettings.json`) never escapes the root |

**A bug the tests found:** a malformed multipart body (no `Content-Disposition`) made the framework throw
`BadHttpRequestException(400)`, but `GlobalExceptionHandler` mapped every unknown exception to `500`.
I added a `BadHttpRequestException` case that keeps the exception's own status code. This also makes
the real Kestrel 11 MB limit return `413` instead of `500`. The test
`UploadDocument_MalformedMultipartBody_Returns400` guards against a regression.

**What the tests do not cover:** SQL Server–specific behaviour (SQLite is a relational stand-in, so
the SQL Server migration itself is not exercised) and Kestrel's 11 MB limit (TestServer does not
enforce Kestrel limits, so the tests hit the 10 MB `FileValidator` rule instead).

---

## Assumptions and known limitations

**Assumptions**
- Supported types are **PDF, DOCX, XLSX, TXT, PNG, JPG/JPEG**: common business documents and images. Executables, scripts, archives and legacy Office formats (`.doc`, `.xls`) are rejected.
- One file per upload request, sent in the `file` form field.
- No authentication or authorisation, per the assessment brief. Every client can see and delete every document.
- Single-server deployment with a local disk.

**Limitations and how I would address them**

| Limitation | Next step |
|---|---|
| Type validation is **extension-based only**: a renamed `.exe` → `.pdf` would be accepted | Check file signatures (magic bytes) for each allowed type and/or run antivirus scanning |
| `GET /api/documents` has no paging, filtering or sorting | Add `?page=&pageSize=` and sort by `UploadedAt` (the Version 7 Guid ids are already time-ordered) |
| Orphan files can remain if a delete's file step fails (logged, not retried) | Scheduled orphan-cleanup job (see [Consistency on failure](#consistency-on-failure)) |
| Local disk storage doesn't scale across multiple servers | Implement `IFileStorageService` on Azure Blob Storage / S3. The handlers don't change. |
| Path comparison uses `OrdinalIgnoreCase` (matches Windows; Linux is case-sensitive) | Low risk because names are generated. Could switch to an OS-dependent comparison. |
| Duplicate uploads of the same content are stored twice | Optional content hash (SHA-256) column with dedup |
| List/Retrieve endpoints don't pass the request `CancellationToken` to MediatR | Add a `CancellationToken ct` parameter and pass it to `Send` |
| `Common/Models/Result` and `ValidationBehavior` come from the boilerplate and are unused/no-ops today | Remove, or adopt them when validators are added |
| MediatR v13+ is commercially licensed (free community licence for individuals/small companies) and may log a licence warning without a key | Acceptable for an assessment. Alternatively, call the handler classes directly. |

---

## AI usage

**Tools used:** Claude Code (Anthropic) as a pair-programming assistant in the terminal/IDE.

**How it was used**
- To analyse the assessment brief and turn it into a step-by-step checklist (kept in `CLAUDE.md`).
- Step by step, happy path first: entity and DbContext → file storage → validation → each endpoint slice → tests → README. I asked for the reasoning behind each decision so I could explain it.
- To write the test suite (SQLite factory, integration tests, handler unit tests with Moq, storage tests) and this README.

**What I reviewed, changed or decided myself**
- The overall design: vertical slices + Carter + MediatR (from my own boilerplate), metadata in SQL Server and files on disk, and the operation order for upload and delete.
- I reviewed every generated file before accepting it and ran `dotnet test` after each test file.
- When a test exposed the `BadHttpRequestException → 500` bug, the AI reported it **without** changing production code; I reviewed the cause and approved the fix.
- Removed unused boilerplate (the sample `Product` feature) and fixed housekeeping issues.

**Areas I'm least confident about**
- Cross-resource consistency: the DB and the disk can't share a transaction. The chosen order limits the damage, but orphan files are still possible until a cleanup job exists.
- Extension-based validation: it is a clear, explainable rule, but it is not real content inspection.
- SQLite in tests vs SQL Server in production: small behavioural differences (for example `DateTime` precision or case sensitivity) would not be caught by the test suite.
