# CLAUDE.md — DocumentManagement (Backend Technical Assessment)

## Context
Technical assessment from P&A Grant Thornton (Practice Services Support Division / Technology Solutions Group).
Build a Document Management REST API in C# / ASP.NET Core with a relational DB (SQL Server).
Expected effort: 3–4 hours. Goal is a functional, understandable solution, not over-engineered.
AI tools are allowed, but every design decision must be explainable in a follow-up interview.

GitHub repo name (required format): `DocumentManagement_Villanueva_Arishavelle_Karl`
Only the repo name is mandated; solution/projects use `DocumentManagement` (no personal name in namespaces).

## Working style
- Build in small chunks, one step at a time, happy path first.
- Explain the "why" behind each decision (interview prep).
- Conversation in English.

## Tech stack / decisions
- .NET 10 (current LTS), scaffolded from the user's own boilerplate (aspnetcore-minimalapi-boilerplate-kit: Minimal API, vertical slice, Carter, MediatR).
- Keep vertical slices + Carter. MediatR is OK but must be justified in README (standard slice structure, testable handlers). Remove unused boilerplate features.
- EF Core + SQL Server (LocalDB default connection string; real secrets via User Secrets only).
- File contents on local disk, metadata in DB (no BLOBs).
- Tests: xUnit + WebApplicationFactory (+ FluentAssertions); mock `IFileStorageService` for failure scenarios.

## Actual structure
```
DocumentManagement.slnx
├── src/DocumentManagement.Api/             (Program.cs, Extensions/, Features/Documents/...)
├── src/DocumentManagement.Common/          (FileStorage/ IFileStorageService + FileStorageOptions, FileValidator/, Exceptions/, Models/)
├── src/DocumentManagement.Infrastructure/  (Persistence/ AppDbContext + Entities + Configurations, Migrations/, FileStorage/LocalFileStorageService, Behaviors/)
└── tests/DocumentManagement.Tests/         (Infrastructure/, Integration/Documents/, Unit/Handlers/, Unit/FileStorage/)
```
Feature slices under `Features/Documents/`: UploadDocument, ListDocuments, RetrieveDocumentDetails, DownloadDocuments, DeleteDocuments.

Housekeeping: done (stray-space filename fixed, `Product` boilerplate removed). Still unused from boilerplate: `Common/Models/Result`, `ValidationBehavior` (no validators registered).

## Requirements checklist

### Endpoints
| Endpoint | Purpose | Status codes |
|---|---|---|
| POST /api/documents | Upload (multipart/form-data) | 201, 400, 413, 415 |
| GET /api/documents | List metadata (no file contents) | 200 |
| GET /api/documents/{id} | Get one document's metadata | 200, 404 |
| GET /api/documents/{id}/download | Download file | 200, 404 (DB row OR physical file missing) |
| DELETE /api/documents/{id} | Delete DB row + stored file | 204, 404 |

### Metadata (minimum)
Id, OriginalFileName, StoredFileName, ContentType, FileSize, UploadedAt.

### Validation / security
- Max 10 MB per file.
- Validate required inputs; reject invalid/unsupported uploads.
- Never use user-supplied filenames as disk paths; protect against collisions and path traversal.
- Useful error responses (ProblemDetails) with correct status codes.
- Document assumptions about supported file types. No auth required.

### Consistency on failure
- Upload: save file first, then DB row; if DB save fails → delete the file (no orphans), rethrow.
- Delete: delete in a defensible order; explain what happens if the second step fails (log / cleanup). If file already missing, still remove DB row.

### Required tests (minimum)
- Successful upload
- Retrieve existing document
- Nonexistent document → 404
- Upload over size limit rejected
- Document deletion
- Bonus: validation failures, storage errors, DB failures.

### README must include
Overview; prerequisites (.NET 10 SDK, SQL Server/LocalDB); DB setup (use `dotnet ef` CLI commands); how to run; how to run tests; endpoint list; architecture / data access / file storage explanation; known limitations & assumptions; AI usage section (tools used, how, what was reviewed/modified, least-confident areas). No secrets committed. Document anything incomplete and how it would be finished.

## Progress
- [x] Analyzed assessment PDF
- [x] Scaffolded solution with boilerplate (.NET 10)
- [x] Step 1: Document entity, AppDbContext, SQL Server registration, InitialCreate migration (commit Migrations/)
- [x] Step 2: IFileStorageService + LocalFileStorageService (+ FileStorageOptions, appsettings section, Singleton registration, Storage/ in .gitignore)
- [x] Step 3a: FileValidator + server request limits
- [x] Step 3b: Upload handler + Carter endpoint (save file → save DB → cleanup on failure)
- [x] Step 4: List + Get by id endpoints
- [x] Step 5: Download endpoint
- [x] Step 6: Delete endpoint
- [x] Step 7: Tests (29 passing)
- [x] Step 8a: README
- [ ] Step 8b: push to GitHub

## Implementation notes

### Step 1 — Entity & DbContext
- Packages: Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools (PMC) / .Design (CLI).
- `Document { Guid Id; string OriginalFileName; string StoredFileName; string ContentType; long FileSize; DateTime UploadedAt; }`
- Config: OriginalFileName max 255, StoredFileName max 100 + unique index (extra collision guard), ContentType max 100.
- Connection string `DefaultConnection` → LocalDB (Windows auth, safe to commit).
- PMC: `Add-Migration InitialCreate`, `Update-Database`. CLI: `dotnet ef migrations add InitialCreate`, `dotnet ef database update`.

### Step 2 — File storage
- appsettings.json: `"FileStorage": { "RootPath": "Storage/Documents" }`
- `IFileStorageService`: `SaveAsync(Stream, extension, ct)` → storedFileName, `OpenRead(storedFileName)` → `Stream?`, `Delete(storedFileName)` → bool.
- `LocalFileStorageService`:
  - Root path resolved to absolute via `Path.GetFullPath(Path.Combine(env.ContentRootPath, RootPath))`; folder created on startup.
  - Stored name = `{Guid:N}{extension}` — user filename never touches disk path.
  - `FileMode.CreateNew` → never overwrites.
  - `OpenRead` returns null if missing (→ 404); `FileShare.Read` allows concurrent downloads; streams avoid loading whole file in memory.
  - `GetSafePath`: `Path.GetFileName` strips `..`/folders, then `StartsWith(_rootPath + DirectorySeparatorChar)` check (defense in depth vs path traversal; separator prevents sibling-folder bypass like `Documents_Evil`).
- Registered as Singleton: stateless (only a readonly root path from config). Never inject Scoped services (e.g. DbContext) into it. DbContext stays Scoped (change tracker + connection are per-request, not thread-safe).
- Known limitation: OrdinalIgnoreCase path comparison suits Windows; Linux is case-sensitive (low risk since names are generated).

### Step 3a — Validation
- `FileValidator.MaxFileSize` = 10 MB; allowlist extension → trusted content type: .pdf, .docx, .xlsx, .txt, .png, .jpg, .jpeg.
- Don't trust client Content-Type; derive it from the allowlist.
- Errors via `Results.Problem`: 400 (missing/empty), 413 (too large), 415 (unsupported type).
- Server limits in Program.cs set to 11 MB (Kestrel `MaxRequestBodySize` + `FormOptions.MultipartBodyLengthLimit`) to block huge bodies early; the app-level 10 MB check gives the exact rule and is what tests hit (TestServer ignores Kestrel limits).
- Known limitation (README): extension-based validation only; magic-byte/signature checking is out of scope.

### Steps 3b–6 — Endpoints
- Upload: file first, then DB row; on DB failure delete the file and rethrow.
- Delete: DB row first, then file; file missing → warning, file delete throws → error log; both still 204 (worst case = harmless orphan file).
- Download: 404 if row missing OR file missing (latter logged as inconsistency); `Results.File` streams.

### Step 7 — Tests
- SQLite in-memory: factory removes `DbContextOptions<AppDbContext>` AND `IDbContextOptionsConfiguration<AppDbContext>` (EF Core 9+), keeps one open connection, `EnsureCreated` in `CreateHost`.
- Fresh factory per test (xUnit makes a new class instance per test) → clean DB + temp storage folder each time.
- `AppDbContext` is sealed → DB failure simulated with `FailingSaveChangesInterceptor`.
- FluentAssertions pinned to 7.x (8.x is commercial). .NET 10 `Program` is public, so no `partial class Program` needed.
- Bug found by tests: `BadHttpRequestException` (malformed multipart, Kestrel 413) fell through `GlobalExceptionHandler` → 500. Fixed by mapping it to its own `StatusCode`.

## Interview one-liners
- Delete order: "The DB is the source of truth. Deleting the row first means the worst case is an invisible, logged orphan file instead of a visible document with a broken download."
- Test DB: "SQLite in-memory gives a real relational engine with zero setup; one open connection per factory keeps the DB alive, and a new factory per test keeps tests isolated."
- Storage: "Stateless service with a single root folder. Guid filenames keep user input off the disk path; GetSafePath strips directory segments and verifies the path stays in the storage root; CreateNew prevents overwrites; streams avoid loading large files into memory."
- Singleton: "It's stateless — the only field is a read-only root path from config, so it's safe across concurrent requests."
- Server limits: "Kestrel/FormOptions at 11 MB stop abusive bodies before they're buffered; the 10 MB FileValidator check is the business rule and returns a clean 413 ProblemDetails."
