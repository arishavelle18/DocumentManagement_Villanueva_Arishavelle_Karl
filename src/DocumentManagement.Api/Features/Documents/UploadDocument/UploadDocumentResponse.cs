namespace DocumentManagement.Api.Features.Documents.UploadDocument;

public sealed record UploadDocumentResponse(
    UploadDocumentModel Data);

public sealed record UploadDocumentModel(
    Guid Id,
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt);
