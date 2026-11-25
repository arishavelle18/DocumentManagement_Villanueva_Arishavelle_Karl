using System.Text;
using DocumentManagement.Api.Features.Documents.UploadDocument;
using DocumentManagement.Common.FileStorage;
using DocumentManagement.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentManagement.Tests.Unit.Handlers;

public sealed class UploadDocumentHandlerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IFileStorageService> _storage = new();

    [Fact]
    public async Task Handle_SaveChangesFails_DeletesStoredFileAndRethrows()
    {
        // Arrange
        const string storedFileName = "0123456789abcdef0123456789abcdef.txt";
        _storage
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), ".txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedFileName);

        await using var db = _database.CreateContext(new FailingSaveChangesInterceptor());
        var handler = new UploadDocumentHandler(db, _storage.Object, NullLogger<UploadDocumentHandler>.Instance);
        var command = new UploadDocumentCommand(CreateFormFile("sample.txt", "hello"));

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _storage.Verify(s => s.Delete(storedFileName), Times.Once);

        await using var verifyDb = _database.CreateContext();
        (await verifyDb.Documents.CountAsync()).Should().Be(0);
    }

    private static FormFile CreateFormFile(string fileName, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    public void Dispose() => _database.Dispose();
}
