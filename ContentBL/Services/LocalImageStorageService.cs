using ContentBL.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Shared.Common.Exceptions;

namespace ContentBL.Services;

public class LocalImageStorageService : IImageStorageService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    // [Assessment-AI] Visibility widened from private to internal ONLY so
    // LocalMediaContentReader reads from the same folder instead of duplicating
    // the path. Value and behaviour unchanged.
    internal const string UploadsFolder = "uploads/lessons";

    private readonly IWebHostEnvironment _environment;

    public LocalImageStorageService(IWebHostEnvironment environment) => _environment = environment;

    public async Task<string> SaveImageAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new BusinessRuleException("الملف فاضي");

        if (file.Length > MaxFileSizeBytes)
            throw new BusinessRuleException("حجم الصورة أكبر من 5 ميجا");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new BusinessRuleException("امتداد الصورة غير مسموح - المسموح بس jpg, jpeg, png, webp");

        // Deliberately NOT a BusinessRuleException: a missing wwwroot is a server
        // misconfiguration the uploader cannot fix, so it must surface as a 500
        // and its setup hint must never reach the client.
        var webRootPath = _environment.WebRootPath
            ?? throw new InvalidOperationException("wwwroot مش متظبطة، شوفي app.UseStaticFiles() في Program.cs");

        var targetDirectory = Path.Combine(webRootPath, UploadsFolder);
        Directory.CreateDirectory(targetDirectory);

        // اسم عشوائي تمامًا - محدش يقدر يخمّن أسماء الصور أو يستبدل صورة حد تاني
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(targetDirectory, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        // الـ URL النسبي اللي هيتخزن في الداتابيز جوه MediaUrl
        return $"/{UploadsFolder}/{fileName}";
    }

    public void DeleteImage(string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            return;

        // بنتعامل بس مع الصور اللي إحنا رفعناها (تحت /uploads/lessons)، مش أي رابط خارجي
        if (!mediaUrl.StartsWith($"/{UploadsFolder}/", StringComparison.OrdinalIgnoreCase))
            return;

        var webRootPath = _environment.WebRootPath;
        if (webRootPath is null)
            return;

        var relativePath = mediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(webRootPath, relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
