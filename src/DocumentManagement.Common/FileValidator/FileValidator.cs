using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocumentManagement.Common.FileValidator;

public static class FileValidator
{
    public const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    // Allowlist: extension → the content type WE trust
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".txt"] = "text/plain",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
        };

    // Returns null if valid, or a ProblemDetails result if invalid
    public static IResult? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "No file uploaded",
                detail: "A non-empty file must be provided in the 'file' form field.");

        if (file.Length > MaxFileSize)
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "File too large",
                detail: $"Maximum allowed size is {MaxFileSize / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedTypes.ContainsKey(extension))
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported file type",
                detail: $"Allowed types: {string.Join(", ", AllowedTypes.Keys)}");

        return null;
    }
}
