using Carter;
using MediatR;

namespace DocumentManagement.Api.Features.Documents.RetrieveDocumentDetails;

public class Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/documents/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var response = await mediator.Send(new RetrieveDocumentDetailsCommand(id));
            return Results.Ok(response);
        })
        .Produces<RetrieveDocumentDetailsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("RetrieveDocumentDetails")
        .WithTags("Documents");
    }
}


