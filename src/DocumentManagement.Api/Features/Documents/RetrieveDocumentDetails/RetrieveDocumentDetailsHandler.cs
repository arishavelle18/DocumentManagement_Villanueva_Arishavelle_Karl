using DocumentManagement.Common.Exceptions;
using DocumentManagement.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Api.Features.Documents.RetrieveDocumentDetails;

public sealed class RetrieveDocumentDetailsHandler(
    AppDbContext db) : IRequestHandler<RetrieveDocumentDetailsCommand, RetrieveDocumentDetailsResponse>
{
    public async Task<RetrieveDocumentDetailsResponse> Handle(RetrieveDocumentDetailsCommand request, CancellationToken cancellationToken)
    {
        var document = await db.DocumentsView.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (document is null)
        {
            throw new NotFoundException($"Document with ID {request.Id} not found.");
        }
        return new RetrieveDocumentDetailsResponse(new RetrieveDocumentDetailsModel(
            document.Id,
            document.OriginalFileName,
            document.StoredFileName,
            document.ContentType,
            document.FileSize,
            document.UploadedAt));
    }
}


