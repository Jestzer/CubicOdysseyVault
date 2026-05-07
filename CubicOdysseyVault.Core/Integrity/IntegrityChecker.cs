using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CubicOdysseyVault.Core.Saves;

namespace CubicOdysseyVault.Core.Integrity;

public static class IntegrityChecker
{
    public static IntegrityReport InspectSlot(SaveSlot slot)
    {
        var files = slot.Files.Select(f => (f.FileName, f.FullPath, f.SizeBytes)).ToList();
        return InspectFiles(files, screenshotExpected: true);
    }

    public static IntegrityReport InspectAccount(SaveAccount account)
    {
        var files = account.AccountFiles.Select(f => (f.FileName, f.FullPath, f.SizeBytes)).ToList();
        return InspectFiles(files, screenshotExpected: false);
    }

    private static IntegrityReport InspectFiles(
        List<(string Name, string Path, long Size)> files,
        bool screenshotExpected)
    {
        var fileResults = new List<IntegrityFileResult>();
        var issues = new List<string>();
        long total = 0;
        bool hasScreenshot = false;
        bool screenshotHeaderValid = false;
        bool anyCorrupt = false;
        bool anySuspicious = false;

        var sorted = files.OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

        foreach (var (name, path, size) in sorted)
        {
            try
            {
                var (sha256, allNull) = HashAndCheckNull(path);
                fileResults.Add(new IntegrityFileResult(name, sha256, size, allNull));
                total += size;

                if (allNull && size > 0)
                {
                    anyCorrupt = true;
                    issues.Add($"{name} is entirely null bytes (interrupted in-place write?).");
                }
                if (size == 0)
                {
                    anySuspicious = true;
                    issues.Add($"{name} is zero bytes.");
                }

                if (string.Equals(name, "screenshot.tga", StringComparison.OrdinalIgnoreCase))
                {
                    hasScreenshot = true;
                    screenshotHeaderValid = TgaHeaderValid(path);
                    if (!screenshotHeaderValid)
                    {
                        anySuspicious = true;
                        issues.Add("screenshot.tga has an unexpected header.");
                    }
                }
            }
            catch (Exception ex)
            {
                anySuspicious = true;
                issues.Add($"{name}: {ex.Message}");
                fileResults.Add(new IntegrityFileResult(name, "", size, false));
            }
        }

        if (screenshotExpected && !hasScreenshot)
        {
            anySuspicious = true;
            issues.Add("screenshot.tga is missing.");
        }

        var combinedHash = ComputeCombinedHash(fileResults);

        var health = anyCorrupt ? SlotHealth.Corrupted
                   : anySuspicious ? SlotHealth.Suspicious
                   : SlotHealth.Healthy;

        return new IntegrityReport(
            health,
            combinedHash,
            fileResults,
            total,
            hasScreenshot,
            screenshotHeaderValid,
            issues);
    }

    private static (string Sha256, bool AllNull) HashAndCheckNull(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var buffer = new byte[81920];
        bool sawAnyByte = false;
        bool allNullSoFar = true;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            sawAnyByte = true;
            sha.TransformBlock(buffer, 0, read, null, 0);
            if (allNullSoFar)
            {
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] != 0) { allNullSoFar = false; break; }
                }
            }
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hashBytes = sha.Hash!;
        return (Convert.ToHexString(hashBytes).ToLowerInvariant(), sawAnyByte && allNullSoFar);
    }

    private static bool TgaHeaderValid(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[18];
            int read = 0;
            while (read < header.Length)
            {
                int n = stream.Read(header[read..]);
                if (n <= 0) break;
                read += n;
            }
            if (read < 18) return false;

            // Image type at offset 2: 2 (uncompressed RGB), 10 (RLE RGB),
            // 3 (uncompressed grayscale), 11 (RLE grayscale).
            byte imageType = header[2];
            return imageType == 2 || imageType == 10 || imageType == 3 || imageType == 11;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeCombinedHash(IReadOnlyList<IntegrityFileResult> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(f => f.FileName, StringComparer.Ordinal))
        {
            sb.Append(f.FileName);
            sb.Append(':');
            sb.Append(f.Sha256);
            sb.Append('\n');
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
