using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessingSystem.Shared.Exceptions;

namespace OrderProcessingSystem.Shared.Storage;

public sealed class JsonFileStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    // The same per-file semaphore protects both sync and async operations.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dataDirectory;

    public JsonFileStore()
        : this(Path.Combine(AppContext.BaseDirectory, "Data"))
    {
    }

    public JsonFileStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _dataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(_dataDirectory);
    }

    public string DataDirectory => _dataDirectory;

    public async Task<List<T>> ReadAllAsync<T>(string fileName, CancellationToken ct)
    {
        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetLock(path);
        await fileLock.WaitAsync(ct);

        try
        {
            return await ReadCoreAsync<T>(path, fileName, ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task WriteAllAsync<T>(string fileName, List<T> records, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetLock(path);
        await fileLock.WaitAsync(ct);

        try
        {
            await WriteAtomicCoreAsync(path, records, ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    // The read-modify-write sequence runs under one lock, so updates are not lost.
    public async Task<List<T>> UpdateAsync<T>(
        string fileName,
        Func<List<T>, List<T>> update,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetLock(path);
        await fileLock.WaitAsync(ct);

        try
        {
            List<T> records = await ReadCoreAsync<T>(path, fileName, ct);
            List<T> updated = update(records)
                ?? throw new InvalidOperationException("The update function returned null.");

            await WriteAtomicCoreAsync(path, updated, ct);
            return updated;
        }
        finally
        {
            fileLock.Release();
        }
    }

    // Sync methods are intended only for the synchronous comparison exercises.
    public List<T> ReadAll<T>(string fileName)
    {
        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetLock(path);
        fileLock.Wait();

        try
        {
            return ReadCore<T>(path, fileName);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void WriteAll<T>(string fileName, List<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetLock(path);
        fileLock.Wait();

        try
        {
            WriteAtomicCore(path, records);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static async Task<List<T>> ReadCoreAsync<T>(
        string path,
        string fileName,
        CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return new List<T>();
        }

        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
        return Deserialize<T>(json, fileName);
    }

    private static List<T> ReadCore<T>(string path, string fileName)
    {
        if (!File.Exists(path))
        {
            return new List<T>();
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        return Deserialize<T>(json, fileName);
    }

    private static List<T> Deserialize<T>(string json, string fileName)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(fileName, ex);
        }
    }

    private string GetPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // The store accepts collection file names, not arbitrary paths.
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only a file name is allowed.", nameof(fileName));
        }

        return Path.Combine(_dataDirectory, fileName);
    }

    private static SemaphoreSlim GetLock(string path) =>
        FileLocks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));

    private static async Task WriteAtomicCoreAsync<T>(
        string path,
        List<T> records,
        CancellationToken ct)
    {
        string tempPath = GetTempPath(path);
        string json = JsonSerializer.Serialize(records, JsonOptions);

        try
        {
            await File.WriteAllTextAsync(tempPath, json, new UTF8Encoding(false), ct);
            ct.ThrowIfCancellationRequested();

            // The temporary file is in the same directory so replacement stays on the same volume.
            File.Move(tempPath, path, true);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static void WriteAtomicCore<T>(string path, List<T> records)
    {
        string tempPath = GetTempPath(path);
        string json = JsonSerializer.Serialize(records, JsonOptions);

        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            File.Move(tempPath, path, true);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static string GetTempPath(string path) =>
        $"{path}.{Guid.NewGuid():N}.tmp";

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // The final JSON file remains untouched if cleanup of a temporary file fails.
        }
        catch (UnauthorizedAccessException)
        {
            // The final JSON file remains untouched if cleanup of a temporary file fails.
        }
    }
}
