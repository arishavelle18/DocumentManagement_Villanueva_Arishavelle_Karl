namespace DocumentManagement.Common.FileStorage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string RootPath { get; set; } = "Storage/Documents";
}