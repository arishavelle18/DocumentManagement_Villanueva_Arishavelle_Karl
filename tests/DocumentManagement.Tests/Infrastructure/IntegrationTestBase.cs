using System.Net;
using System.Net.Http.Json;
using DocumentManagement.Api.Features.Documents.UploadDocument;
using FluentAssertions;

namespace DocumentManagement.Tests.Infrastructure;

/// <summary>
/// xUnit creates a new test-class instance per test, so every test gets its own
/// factory: a fresh SQLite database and a fresh temp storage folder.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected const string DocumentsUrl = "/api/documents";

    protected CustomWebApplicationFactory Factory { get; } = new();
    protected HttpClient Client { get; }

    protected IntegrationTestBase()
    {
        Client = Factory.CreateClient();
    }

    /// <summary>Uploads a small valid .txt file through the real endpoint and returns its metadata.</summary>
    protected async Task<UploadDocumentModel> UploadValidFileAsync(string fileName = "sample.txt")
    {
        using var content = TestFileHelper.ValidTextFile(fileName);
        var response = await Client.PostAsync(DocumentsUrl, content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<UploadDocumentResponse>();
        return body!.Data;
    }

    protected string StoredFilePath(string storedFileName) =>
        Path.Combine(Factory.StorageRootPath, storedFileName);

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
