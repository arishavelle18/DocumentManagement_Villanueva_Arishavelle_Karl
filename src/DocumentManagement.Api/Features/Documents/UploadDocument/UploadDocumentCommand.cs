using MediatR;

namespace DocumentManagement.Api.Features.Documents.UploadDocument;

// pass iformfile to command handler, which will validate and save the file to blob storage
public record UploadDocumentCommand(
    IFormFile? File) : IRequest<UploadDocumentResponse>;
