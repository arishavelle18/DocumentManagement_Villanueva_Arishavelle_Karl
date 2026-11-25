using DocumentManagement.Common.FileStorage;
using DocumentManagement.Common.FileValidator;
using DocumentManagement.Infrastructure.Persistence;
using DocumentManagement.Infrastructure.Persistence.Entities;
using MediatR;

namespace DocumentManagement.Api.Features.Documents.UploadDocument;

public sealed class UploadDocumentHandler(
    AppDbContext db,
    IFileStorageService storage,
    ILogger<UploadDocumentHandler> logger) : IRequestHandler<UploadDocumentCommand, UploadDocumentResponse>
{
    public async Task<UploadDocumentResponse> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var file = request.File!;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Save the file to disk
        string storedFileName;

        await using (var stream = file.OpenReadStream())
        {
            storedFileName = await storage.SaveAsync(stream, extension, cancellationToken);
        }
        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            ContentType = FileValidator.AllowedTypes[extension], // trusted, not client-supplied
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };


        //save the document metadata to the database

        try
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DB save failed; deleted orphan file {StoredFileName}", storedFileName);
            storage.Delete(storedFileName); // delete the   orphan file if the database save fails
            throw;
        }

        return new UploadDocumentResponse( new UploadDocumentModel(
            document.Id, document.OriginalFileName, document.StoredFileName, document.ContentType,
            document.FileSize, document.UploadedAt));
    }
}
