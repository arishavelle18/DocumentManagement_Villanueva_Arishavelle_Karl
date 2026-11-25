using Carter;
using DocumentManagement.Common.FileValidator;
using MediatR;

namespace DocumentManagement.Api.Features.Documents.UploadDocument;

public class Endpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents", async (IFormFile? file, ISender sender, CancellationToken ct) =>
        {
            if (FileValidator.Validate(file) is { } problem) return problem;   // 400/413/415
            var result = await sender.Send(new UploadDocumentCommand(file), ct);
            return Results.Created($"/api/documents/{result.Data.Id}", result);
        }).DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<UploadDocumentResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
        .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
        .WithName("UploadDocument")
        .WithTags("Documents");
    }
}