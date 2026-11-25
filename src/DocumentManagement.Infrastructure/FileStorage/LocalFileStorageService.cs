using DocumentManagement.Common.FileStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace DocumentManagement.Infrastructure.FileStorage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    public LocalFileStorageService(IOptions<FileStorageOptions> options, IWebHostEnvironment env)
    {
        // Resolve to an absolute path so relative paths behave the same everywhere
        _rootPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.RootPath));
        Directory.CreateDirectory(_rootPath);
    }
    public bool Delete(string storedFileName)
    {
        var fullPath = GetSafePath(storedFileName);
        if (!File.Exists(fullPath)) return false;

        File.Delete(fullPath);
        return true;
    }

    public Stream? OpenRead(string storedFileName)
    {
        var fullPath = GetSafePath(storedFileName);
        return File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        // filename never touches the disk path
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = GetSafePath(storedFileName);

        // FileMode.CreateNew throws if the file somehow exists, so nothing gets overwritten
        await using var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream, ct);

        return storedFileName;
    }

    // Path traversal guard: the final path must stay inside _rootPath
    private string GetSafePath(string storedFileName)
    {
        var fileName = Path.GetFileName(storedFileName); // strips any "../" or folders
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, fileName));

        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path.");

        return fullPath;
    }
}
