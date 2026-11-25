using System.Text;
using DocumentManagement.Common.FileStorage;
using DocumentManagement.Infrastructure.FileStorage;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentManagement.Tests.Unit.FileStorage;

/// <summary>
/// Exercises the real service against a real temp folder. Layout per test:
///   {base}/                  ← stands in for the app's ContentRootPath
///   {base}/secret.txt        ← a file OUTSIDE the storage root (traversal target)
///   {base}/Storage/Documents ← storage root
/// Only IWebHostEnvironment is stubbed, to supply ContentRootPath.
/// </summary>
public sealed class LocalFileStorageServiceTests : IDisposable
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("storage test content");

    private readonly string _basePath =
        Path.Combine(Path.GetTempPath(), "DocumentManagement.Tests", Guid.NewGuid().ToString("N"));

    private readonly string _rootPath;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_basePath);

        var options = Options.Create(new FileStorageOptions { RootPath = Path.Combine("Storage", "Documents") });

        _sut = new LocalFileStorageService(options, env.Object);
        _rootPath = Path.Combine(_basePath, "Storage", "Documents");
    }

    [Fact]
    public async Task SaveAsync_ValidStream_StoresFileUnderGeneratedGuidName()
    {
        // Arrange
        using var stream = new MemoryStream(Content);

        // Act
        var storedFileName = await _sut.SaveAsync(stream, ".pdf");

        // Assert
        Path.GetExtension(storedFileName).Should().Be(".pdf");
        Guid.TryParseExact(Path.GetFileNameWithoutExtension(storedFileName), "N", out _)
            .Should().BeTrue("the stored name must be a generated Guid, never the user's filename");

        Directory.GetFiles(_rootPath).Should().ContainSingle()
            .Which.Should().Be(Path.Combine(_rootPath, storedFileName));
        (await File.ReadAllBytesAsync(Path.Combine(_rootPath, storedFileName))).Should().Equal(Content);
    }

    [Fact]
    public async Task SaveAsync_CalledTwice_GeneratesDistinctNames()
    {
        // Act
        var first = await _sut.SaveAsync(new MemoryStream(Content), ".txt");
        var second = await _sut.SaveAsync(new MemoryStream(Content), ".txt");

        // Assert
        first.Should().NotBe(second);
        Directory.GetFiles(_rootPath).Should().HaveCount(2);
    }

    [Fact]
    public void OpenRead_MissingFile_ReturnsNull()
    {
        // Act
        using var stream = _sut.OpenRead($"{Guid.NewGuid():N}.txt");

        // Assert
        stream.Should().BeNull();
    }

    [Fact]
    public async Task OpenRead_ExistingFile_ReturnsStreamWithContent()
    {
        // Arrange
        var storedFileName = await _sut.SaveAsync(new MemoryStream(Content), ".txt");

        // Act
        await using var stream = _sut.OpenRead(storedFileName);

        // Assert
        stream.Should().NotBeNull();
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        copy.ToArray().Should().Equal(Content);
    }

    [Theory]
    [InlineData("../../secret.txt")]
    [InlineData("..\\..\\secret.txt")]
    public void OpenRead_PathTraversalName_NeverReadsOutsideStorageRoot(string maliciousName)
    {
        // Arrange: a real file sits exactly where the traversal path points.
        var outsideFile = CreateOutsideFile();

        // Act
        using var stream = _sut.OpenRead(maliciousName);

        // Assert: directory segments are stripped, so it looks for {root}/secret.txt, which doesn't exist.
        stream.Should().BeNull();
        File.Exists(outsideFile).Should().BeTrue();
    }

    [Theory]
    [InlineData("../../secret.txt")]
    [InlineData("..\\..\\secret.txt")]
    public void Delete_PathTraversalName_NeverDeletesOutsideStorageRoot(string maliciousName)
    {
        // Arrange
        var outsideFile = CreateOutsideFile();

        // Act
        var deleted = _sut.Delete(maliciousName);

        // Assert
        deleted.Should().BeFalse();
        File.Exists(outsideFile).Should().BeTrue();
    }

    [Fact]
    public void OpenRead_PathTraversalToAppSettings_NeverResolvesOutsideStorageRoot()
    {
        // Arrange: mirrors the real layout, where appsettings.json lives two levels above Storage/Documents.
        File.WriteAllText(Path.Combine(_basePath, "appsettings.json"), "{ \"secret\": true }");

        // Act
        using var stream = _sut.OpenRead("../../appsettings.json");

        // Assert
        stream.Should().BeNull();
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesItAndReturnsTrue()
    {
        // Arrange
        var storedFileName = await _sut.SaveAsync(new MemoryStream(Content), ".txt");

        // Act
        var deleted = _sut.Delete(storedFileName);

        // Assert
        deleted.Should().BeTrue();
        File.Exists(Path.Combine(_rootPath, storedFileName)).Should().BeFalse();
    }

    private string CreateOutsideFile()
    {
        var path = Path.Combine(_basePath, "secret.txt");
        File.WriteAllText(path, "outside the storage root");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }
}
