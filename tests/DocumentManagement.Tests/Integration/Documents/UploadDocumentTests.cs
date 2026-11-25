using System.Net;
using System.Net.Http.Json;
using DocumentManagement.Api.Features.Documents.UploadDocument;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Tests.Integration.Documents;

public sealed class UploadDocumentTests : IntegrationTestBase
{
    [Fact]
    public async Task UploadDocument_ValidFile_Returns201WithMetadataAndStoresFile()
    {
        // Arrange
        using var content = TestFileHelper.ValidTextFile("sample.txt");

        // Act
        var response = await Client.PostAsync(DocumentsUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<UploadDocumentResponse>();
        body.Should().NotBeNull();
        var document = body!.Data;

        document.Id.Should().NotBeEmpty();
        document.OriginalFileName.Should().Be("sample.txt");
        document.ContentType.Should().Be("text/plain");
        document.FileSize.Should().Be(TestFileHelper.SmallTextBytes.Length);
        document.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        document.StoredFileName.Should().NotBe("sample.txt").And.EndWith(".txt");

        response.Headers.Location!.ToString().Should().Be($"{DocumentsUrl}/{document.Id}");

        var storedPath = StoredFilePath(document.StoredFileName);
        File.Exists(storedPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(storedPath)).Should().Equal(TestFileHelper.SmallTextBytes);
    }

    [Fact]
    public async Task UploadDocument_FileOver10MB_Returns413()
    {
        // Arrange
        using var content = TestFileHelper.OversizedFile();

        // Act
        var response = await Client.PostAsync(DocumentsUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await AssertProblemDetailsAsync(response, StatusCodes.Status413PayloadTooLarge);
        StorageIsEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task UploadDocument_NoFile_Returns400()
    {
        // Arrange
        using var content = TestFileHelper.NoFile();

        // Act
        var response = await Client.PostAsync(DocumentsUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
        StorageIsEmpty().Should().BeTrue();
    }

    // Regression test: the framework throws BadHttpRequestException here, which used to
    // fall through GlobalExceptionHandler and come back as a 500.
    [Fact]
    public async Task UploadDocument_MalformedMultipartBody_Returns400()
    {
        // Arrange
        using var content = TestFileHelper.MalformedEmptyMultipart();

        // Act
        var response = await Client.PostAsync(DocumentsUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
        StorageIsEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task UploadDocument_EmptyFile_Returns400()
    {
        // Arrange
        using var content = TestFileHelper.EmptyFile();

        // Act
        var response = await Client.PostAsync(DocumentsUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UploadDocument_UnsupportedExtension_Returns415()
    {
        // Arrange
        using var content = TestFileHelper.ExecutableFile("malware.exe");

        // Act
        var response = await Client.PostAsync(DocumentsUrl, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        await AssertProblemDetailsAsync(response, StatusCodes.Status415UnsupportedMediaType);
        StorageIsEmpty().Should().BeTrue();
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response, int expectedStatus)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(expectedStatus);
        problem.Title.Should().NotBeNullOrWhiteSpace();
    }

    private bool StorageIsEmpty() =>
        !Directory.Exists(Factory.StorageRootPath) || !Directory.EnumerateFiles(Factory.StorageRootPath).Any();
}
