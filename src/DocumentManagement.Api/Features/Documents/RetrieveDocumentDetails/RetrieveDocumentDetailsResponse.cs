namespace DocumentManagement.Api.Features.Documents.RetrieveDocumentDetails;

public record RetrieveDocumentDetailsResponse(
    RetrieveDocumentDetailsModel Data);

public record RetrieveDocumentDetailsModel(Guid Id,
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt);

