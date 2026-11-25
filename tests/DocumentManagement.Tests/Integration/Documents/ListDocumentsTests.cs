using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentManagement.Api.Features.Documents.ListDocuments;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;

namespace DocumentManagement.Tests.Integration.Documents;

public sealed class ListDocumentsTests : IntegrationTestBase
{
    private static readonly string[] MetadataFields =
        ["id", "originalFileName", "storedFileName", "contentType", "fileSize", "uploadedAt"];

    [Fact]
    public async Task ListDocuments_AfterUploads_Returns200WithAllDocumentsAndNoFileContents()
    {
        // Arrange
        var first = await UploadValidFileAsync("first.txt");
        var second = await UploadValidFileAsync("second.txt");

        // Act
        var response = await Client.GetAsync(DocumentsUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ListDocumentsResponse>();
        body!.Data.Select(d => d.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        body.Data.Select(d => d.OriginalFileName).Should().BeEquivalentTo(["first.txt", "second.txt"]);

        // Inspect the raw JSON: every item must carry metadata only, with no content/bytes field.
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var item in json.RootElement.GetProperty("data").EnumerateArray())
        {
            item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(MetadataFields);
        }
    }

    [Fact]
    public async Task ListDocuments_NoDocuments_Returns200WithEmptyList()
    {
        // Act
        var response = await Client.GetAsync(DocumentsUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListDocumentsResponse>();
        body!.Data.Should().BeEmpty();
    }
}
