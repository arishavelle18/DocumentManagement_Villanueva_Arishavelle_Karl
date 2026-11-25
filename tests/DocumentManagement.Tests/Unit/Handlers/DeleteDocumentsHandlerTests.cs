using DocumentManagement.Api.Features.Documents.DeleteDocuments;
using DocumentManagement.Common.FileStorage;
using DocumentManagement.Infrastructure.Persistence.Entities;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentManagement.Tests.Unit.Handlers;

public sealed class DeleteDocumentsHandlerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IFileStorageService> _storage = new();

    [Fact]
    public async Task Handle_StorageDeleteThrows_RemovesRowAndReturnsNoContent()
    {
        // Arrange
        var document = await SeedDocumentAsync();
        _storage
            .Setup(s => s.Delete(document.StoredFileName))
            .Throws(new IOException("File is locked by another process."));

        // Act
        var result = await CreateHandlerAndHandleAsync(document.Id);

        // Assert
        result.Should().BeOfType<NoContent>();
        (await DocumentExistsAsync(document.Id)).Should().BeFalse();
        _storage.Verify(s => s.Delete(document.StoredFileName), Times.Once);
    }

    [Fact]
    public async Task Handle_FileAlreadyMissing_RemovesRowAndReturnsNoContent()
    {
        // Arrange
        var document = await SeedDocumentAsync();
        _storage.Setup(s => s.Delete(document.StoredFileName)).Returns(false);

        // Act
        var result = await CreateHandlerAndHandleAsync(document.Id);

        // Assert
        result.Should().BeOfType<NoContent>();
        (await DocumentExistsAsync(document.Id)).Should().BeFalse();
        _storage.Verify(s => s.Delete(document.StoredFileName), Times.Once);
    }

    private async Task<Microsoft.AspNetCore.Http.IResult> CreateHandlerAndHandleAsync(Guid id)
    {
        await using var db = _database.CreateContext();
        var handler = new DeleteDocumentsCommandHandler(db, _storage.Object, NullLogger<DeleteDocumentsCommandHandler>.Instance);
        return await handler.Handle(new DeleteDocumentsCommand(id), CancellationToken.None);
    }

    // Uses a fresh context so we see what was actually persisted, not the handler's change tracker.
    private async Task<bool> DocumentExistsAsync(Guid id)
    {
        await using var db = _database.CreateContext();
        return await db.Documents.AnyAsync(d => d.Id == id);
    }

    private async Task<Document> SeedDocumentAsync()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "to-delete.txt",
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
