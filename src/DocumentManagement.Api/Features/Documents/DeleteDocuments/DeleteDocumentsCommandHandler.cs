using DocumentManagement.Common.Exceptions;
using DocumentManagement.Common.FileStorage;
using DocumentManagement.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Api.Features.Documents.DeleteDocuments;

public class DeleteDocumentsCommandHandler(AppDbContext appDbContext,IFileStorageService fileStorageService,ILogger<DeleteDocumentsCommandHandler> logger) : IRequestHandler<DeleteDocumentsCommand, IResult>
{
    public async Task<IResult> Handle(DeleteDocumentsCommand request, CancellationToken cancellationToken)
    {
        var document = await appDbContext.Documents.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (document == null)
        {
            throw new NotFoundException($"Document with ID {request.Id} not found.");
        }

        appDbContext.Documents.Remove(document);
        await appDbContext.SaveChangesAsync(cancellationToken);

        try
        {
            if (!fileStorageService.Delete(document.StoredFileName))
            {
                logger.LogWarning(
                    "File {StoredFileName} was already missing when deleting document {DocumentId}.",
                    document.StoredFileName, document.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Document {DocumentId} was removed from the DB, but file {StoredFileName} could not be deleted.",
                document.Id, document.StoredFileName);
        }


        return Results.NoContent();
    }
}
