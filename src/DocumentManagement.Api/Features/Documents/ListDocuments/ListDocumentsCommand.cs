using MediatR;

namespace DocumentManagement.Api.Features.Documents.ListDocuments;

public record ListDocumentsCommand() : IRequest<ListDocumentsResponse>;
