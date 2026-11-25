using System.Net.Http.Headers;
using System.Text;
using DocumentManagement.Common.FileValidator;

namespace DocumentManagement.Tests.Infrastructure;

/// <summary>
/// Builds multipart/form-data bodies for the upload endpoint.
/// The form field name must be "file" to bind to the endpoint's IFormFile parameter.
/// </summary>
public static class TestFileHelper
{
    public const string FormFieldName = "file";

    public static readonly byte[] SmallTextBytes = Encoding.UTF8.GetBytes("Hello from the DocumentManagement tests.");

    /// <summary>A small, allowed .txt file.</summary>
    public static MultipartFormDataContent ValidTextFile(string fileName = "sample.txt") =>
        Create(SmallTextBytes, fileName, "text/plain");

    /// <summary>A .pdf one byte over the 10 MB business limit (still under the 11 MB server limit).</summary>
    public static MultipartFormDataContent OversizedFile(string fileName = "large.pdf") =>
        Create(new byte[FileValidator.MaxFileSize + 1], fileName, "application/pdf");

    /// <summary>A small file with an extension that is not on the allowlist.</summary>
    public static MultipartFormDataContent ExecutableFile(string fileName = "malware.exe") =>
        Create(Encoding.UTF8.GetBytes("MZ fake executable"), fileName, "application/octet-stream");

    /// <summary>A zero-byte file with an allowed extension.</summary>
    public static MultipartFormDataContent EmptyFile(string fileName = "empty.txt") =>
        Create([], fileName, "text/plain");

    /// <summary>A valid multipart form that has a text field but no file part.</summary>
    public static MultipartFormDataContent NoFile() =>
        new() { { new StringContent("no file attached"), "description" } };

    /// <summary>
    /// A multipart body with zero parts. .NET serializes this as an empty section with no
    /// Content-Disposition header, which the server must reject as malformed (400).
    /// </summary>
    public static MultipartFormDataContent MalformedEmptyMultipart() => new();

    public static MultipartFormDataContent Create(byte[] bytes, string fileName, string contentType)
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent();
        form.Add(fileContent, FormFieldName, fileName);
        return form;
    }
}
