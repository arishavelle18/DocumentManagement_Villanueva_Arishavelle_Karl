using DocumentManagement.Common.FileStorage;
using DocumentManagement.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Api.Features.Documents.DownloadDocuments;

public sealed class DownloadDocumentsHandler(AppDbContext appDbContext, IFileStorageService fileStorageService, ILogger<DownloadDocumentsHandler> logger) : IRequestHandler<DownloadDocumentsCommand, IResult>
{
    public async Task<IResult> Handle(DownloadDocumentsCommand request, CancellationToken cancellationToken)
    {
        var document = await appDbContext.DocumentsView.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (document is null)
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Document not found",
                detail: $"No document exists with id '{request.Id}'.");

        // open the file stream from storage
        var fileStream = fileStorageService.OpenRead(document.StoredFileName);

        if (fileStream is null)
        {
            // DB says it exists but the disk doesn't: an inconsistency worth logging
            logger.LogWarning(
                "File {StoredFileName} for document {DocumentId} is missing from storage.",
                document.StoredFileName, document.Id);

            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "File not found",
                detail: "The document record exists, but its file could not be found in storage.");
        }
        return Results.File(fileStream, document.ContentType, document.OriginalFileName);
    }
}
