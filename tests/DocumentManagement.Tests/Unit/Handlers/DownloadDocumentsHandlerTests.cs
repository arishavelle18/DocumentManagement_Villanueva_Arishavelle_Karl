using DocumentManagement.Api.Features.Documents.DownloadDocuments;
using DocumentManagement.Common.FileStorage;
using DocumentManagement.Infrastructure.Persistence.Entities;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentManagement.Tests.Unit.Handlers;

public sealed class DownloadDocumentsHandlerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IFileStorageService> _storage = new();

    [Fact]
    public async Task Handle_FileMissingOnDisk_Returns404Problem()
    {
        // Arrange: the DB row exists, but storage cannot find the file.
        var document = await SeedDocumentAsync();
        _storage.Setup(s => s.OpenRead(document.StoredFileName)).Returns((Stream?)null);

        await using var db = _database.CreateContext();
        var handler = new DownloadDocumentsHandler(db, _storage.Object, NullLogger<DownloadDocumentsHandler>.Instance);

        // Act
        var result = await handler.Handle(new DownloadDocumentsCommand(document.Id), CancellationToken.None);

        // Assert
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Title.Should().Be("File not found");
        _storage.Verify(s => s.OpenRead(document.StoredFileName), Times.Once);
    }

    private async Task<Document> SeedDocumentAsync()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "missing.txt",
            StoredFileName = $"{Guid.NewGuid():N}.txt",
            ContentType = "text/plain",
            FileSize = 5,
            UploadedAt = DateTime.UtcNow
        };

        await using var db = _database.CreateContext();
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document;
    }

    public void Dispose() => _database.Dispose();
}
