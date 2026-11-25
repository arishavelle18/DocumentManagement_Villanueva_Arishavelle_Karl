using MediatR;

namespace DocumentManagement.Api.Features.Documents.DeleteDocuments;

public record DeleteDocumentsCommand(Guid Id) : IRequest<IResult>;
