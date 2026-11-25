using System.Net;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;

namespace DocumentManagement.Tests.Integration.Documents;

public sealed class DownloadDocumentTests : IntegrationTestBase
{
    [Fact]
    public async Task DownloadDocument_ExistingId_Returns200WithContentTypeAndOriginalBytes()
    {
        // Arrange
        var uploaded = await UploadValidFileAsync("notes.txt");

        // Act
        var response = await Client.GetAsync($"{DocumentsUrl}/{uploaded.Id}/download");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("notes.txt");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal(TestFileHelper.SmallTextBytes);
    }

    [Fact]
    public async Task DownloadDocument_NonexistentId_Returns404()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{DocumentsUrl}/{missingId}/download");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task DownloadDocument_FileMissingFromStorage_Returns404()
    {
        // Arrange: DB row exists, but the physical file has been removed out-of-band.
        var uploaded = await UploadValidFileAsync();
        File.Delete(StoredFilePath(uploaded.StoredFileName));

        // Act
        var response = await Client.GetAsync($"{DocumentsUrl}/{uploaded.Id}/download");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
