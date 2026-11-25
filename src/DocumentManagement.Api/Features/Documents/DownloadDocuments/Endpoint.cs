using Carter;
using MediatR;

namespace DocumentManagement.Api.Features.Documents.DownloadDocuments;

public class Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/documents/{id:guid}/download",
             async (Guid id, ISender sender, CancellationToken ct) =>
                 await sender.Send(new DownloadDocumentsCommand(id), ct))
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DownloadDocument")
            .WithTags("Documents"); ;
    }
}