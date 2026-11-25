using System.Net;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;

namespace DocumentManagement.Tests.Integration.Documents;

public sealed class DeleteDocumentTests : IntegrationTestBase
{
    [Fact]
    public async Task DeleteDocument_ExistingId_Returns204AndRemovesRowAndFile()
    {
        // Arrange
        var uploaded = await UploadValidFileAsync();
        var storedPath = StoredFilePath(uploaded.StoredFileName);
        File.Exists(storedPath).Should().BeTrue("precondition: the upload stored the file");

        // Act
        var response = await Client.DeleteAsync($"{DocumentsUrl}/{uploaded.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"{DocumentsUrl}/{uploaded.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        File.Exists(storedPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDocument_NonexistentId_Returns404()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"{DocumentsUrl}/{missingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
