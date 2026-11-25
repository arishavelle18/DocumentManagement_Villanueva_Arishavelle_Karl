using Carter;
using MediatR;

namespace DocumentManagement.Api.Features.Documents.ListDocuments;

public class Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/documents", async (IMediator mediator) =>
        {
            var response = await mediator.Send(new ListDocumentsCommand());
            return Results.Ok(response);
        })
        .Produces<ListDocumentsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithName("ListDocuments")
        .WithTags("Documents");
    }
}