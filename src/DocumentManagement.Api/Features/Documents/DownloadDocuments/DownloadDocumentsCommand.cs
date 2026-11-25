using MediatR;

namespace DocumentManagement.Api.Features.Documents.DownloadDocuments;

public record DownloadDocumentsCommand(Guid Id) : IRequest<IResult>;
