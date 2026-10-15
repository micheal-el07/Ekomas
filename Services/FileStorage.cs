public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);
    string GetPhysicalPath(string storedName);
    void Delete(string storedName);
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration config, IHostEnvironment env)
    {
        var configured = config["Storage:Path"] ?? "uploads";
        _root = Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_root);

    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        var storedName = $"{Guid.NewGuid():N}{extension}";
        await using var file = File.Create(Path.Combine(_root, storedName));
        await content.CopyToAsync(file, ct);
        return storedName;
    }

    public string GetPhysicalPath(string storedName) => Path.Combine(_root, Path.GetFileName(storedName));

    public void Delete(string storedName)
    {
        var path = GetPhysicalPath(storedName);
        if (File.Exists(path)) File.Delete(path);
    }
}

public static class ImageSniffer
{
    public static async Task<string> DetectAsync(Stream s, CancellationToken ct)
    {
        var h = new byte[12];
        var read = await s.ReadAtLeastAsync(h, 12, throwOnEndOfStream: false, ct);
        if (read < 12) return null;

        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return "image/jpeg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return "image/png";
        if (System.Text.Encoding.ASCII.GetString(h, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(h, 8, 4) == "WEBP") return "image/webp";
        return null;
    }
}