namespace DocumentManagement.Api.Features.Documents.ListDocuments;

public record DocumentDto(
    Guid Id,
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt);
