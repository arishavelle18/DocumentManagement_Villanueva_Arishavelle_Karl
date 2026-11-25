namespace DocumentManagement.Common.FileStorage;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);
    Stream? OpenRead(string storedFileName);
    bool Delete(string storedFileName);

}
