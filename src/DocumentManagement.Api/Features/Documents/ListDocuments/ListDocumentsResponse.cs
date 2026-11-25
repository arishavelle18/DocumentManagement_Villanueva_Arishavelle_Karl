namespace DocumentManagement.Api.Features.Documents.ListDocuments;

public record ListDocumentsResponse(
    IEnumerable<DocumentDto> Data);
