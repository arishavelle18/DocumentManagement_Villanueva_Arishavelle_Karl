using DocumentManagement.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Api.Features.Documents.ListDocuments;

public sealed class ListDocumentsHandler(
    AppDbContext db) : IRequestHandler<ListDocumentsCommand, ListDocumentsResponse>
{
    public async Task<ListDocumentsResponse> Handle(ListDocumentsCommand request, CancellationToken cancellationToken)
    {
        var documents = await db.DocumentsView
            .Select(d => new DocumentDto
            (
                d.Id,
                d.OriginalFileName,
                d.StoredFileName,
                d.ContentType,
                d.FileSize,
                d.UploadedAt
            ))
            .ToListAsync(cancellationToken);
        return new ListDocumentsResponse(documents);
    }
}
