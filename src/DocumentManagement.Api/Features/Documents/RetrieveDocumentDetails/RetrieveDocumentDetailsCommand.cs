using MediatR;

namespace DocumentManagement.Api.Features.Documents.RetrieveDocumentDetails;

public record RetrieveDocumentDetailsCommand(Guid Id) : IRequest<RetrieveDocumentDetailsResponse>;


