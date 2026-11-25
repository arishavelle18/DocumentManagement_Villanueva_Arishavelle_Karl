using Carter;
using MediatR;

namespace DocumentManagement.Api.Features.Documents.DeleteDocuments;

public class Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/documents/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
           await sender.Send(new DeleteDocumentsCommand(id), ct))
       .Produces(StatusCodes.Status204NoContent)
       .ProducesProblem(StatusCodes.Status404NotFound)
       .WithName("DeleteDocument")
       .WithTags("Documents");
    }
}