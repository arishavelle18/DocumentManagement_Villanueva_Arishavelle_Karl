using System.Net;
using System.Net.Http.Json;
using DocumentManagement.Api.Features.Documents.RetrieveDocumentDetails;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Tests.Integration.Documents;

public sealed class RetrieveDocumentTests : IntegrationTestBase
{
    [Fact]
    public async Task GetDocument_ExistingId_Returns200WithMatchingMetadata()
    {
        // Arrange
        var uploaded = await UploadValidFileAsync("report.txt");

        // Act
        var response = await Client.GetAsync($"{DocumentsUrl}/{uploaded.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RetrieveDocumentDetailsResponse>();
        var document = body!.Data;

        document.Id.Should().Be(uploaded.Id);
        document.OriginalFileName.Should().Be("report.txt");
        document.StoredFileName.Should().Be(uploaded.StoredFileName);
        document.ContentType.Should().Be(uploaded.ContentType);
        document.FileSize.Should().Be(uploaded.FileSize);
        // SQLite stores DateTime as text, so compare with a small tolerance instead of exact ticks.
        document.UploadedAt.Should().BeCloseTo(uploaded.UploadedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetDocument_NonexistentId_Returns404()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"{DocumentsUrl}/{missingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(404);
        problem.Detail.Should().Contain(missingId.ToString());
    }
}
