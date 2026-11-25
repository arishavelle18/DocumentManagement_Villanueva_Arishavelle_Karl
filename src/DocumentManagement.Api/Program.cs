using Carter;
using DocumentManagement.Api.Extensions;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Server-level guard: reject oversized bodies early (before buffering).
// 11 MB = 10 MB business limit + headroom for multipart boundaries/headers.
// The exact 10 MB rule is enforced by FileValidator (returns 413 ProblemDetails).
const long MaxRequestBodySize = 11 * 1024 * 1024;

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = MaxRequestBodySize);

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = MaxRequestBodySize);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddCarter();

var app = builder.Build();

app.UseApplicationMiddleware();
app.MapCarter();

app.Run();
