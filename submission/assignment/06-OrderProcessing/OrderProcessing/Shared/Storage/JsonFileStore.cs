using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Shared.Storage;

public sealed class JsonFileStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // One gate (a SemaphoreSlim with 1 slot) per file name
    static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    static SemaphoreSlim GateFor(string fileName) =>
        Gates.GetOrAdd(fileName, _ => new SemaphoreSlim(1, 1));

    // ---------- async API ----------

    public async Task<List<T>> ReadAllAsync<T>(string fileName, CancellationToken ct)
    {
        SemaphoreSlim gate = GateFor(fileName);
        await gate.WaitAsync(ct);
        try
        {
            return await ReadUnlockedAsync<T>(fileName, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task WriteAllAsync<T>(string fileName, List<T> items, CancellationToken ct)
    {
        SemaphoreSlim gate = GateFor(fileName);
        await gate.WaitAsync(ct);
        try
        {
            await WriteUnlockedAsync(fileName, items, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    // Read + change + write as ONE step, so no update is ever lost
    public async Task<TResult> UpdateAsync<T, TResult>(
        string fileName, Func<List<T>, TResult> change, CancellationToken ct)
    {
        SemaphoreSlim gate = GateFor(fileName);
        await gate.WaitAsync(ct);
        try
        {
            List<T> items = await ReadUnlockedAsync<T>(fileName, ct);
            TResult result = change(items);
            await WriteUnlockedAsync(fileName, items, ct);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task UpdateAsync<T>(string fileName, Action<List<T>> change, CancellationToken ct) =>
        UpdateAsync<T, bool>(fileName, items => { change(items); return true; }, ct);

    // ---------- sync API (only for the sync comparison versions) ----------

    public List<T> ReadAll<T>(string fileName)
    {
        SemaphoreSlim gate = GateFor(fileName);
        gate.Wait();
        try
        {
            return ReadUnlocked<T>(fileName);
        }
        finally
        {
            gate.Release();
        }
    }

    public void WriteAll<T>(string fileName, List<T> items)
    {
        SemaphoreSlim gate = GateFor(fileName);
        gate.Wait();
        try
        {
            WriteUnlocked(fileName, items);
        }
        finally
        {
            gate.Release();
        }
    }

    public TResult Update<T, TResult>(string fileName, Func<List<T>, TResult> change)
    {
        SemaphoreSlim gate = GateFor(fileName);
        gate.Wait();
        try
        {
            List<T> items = ReadUnlocked<T>(fileName);
            TResult result = change(items);
            WriteUnlocked(fileName, items);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    // ---------- private helpers (the caller already holds the gate) ----------

    static async Task<List<T>> ReadUnlockedAsync<T>(string fileName, CancellationToken ct)
    {
        string path = DataFiles.FullPath(fileName);
        if (!File.Exists(path))
            return new List<T>();

        string json = await File.ReadAllTextAsync(path, Utf8, ct);
        return Parse<T>(fileName, json);
    }

    static List<T> ReadUnlocked<T>(string fileName)
    {
        string path = DataFiles.FullPath(fileName);
        if (!File.Exists(path))
            return new List<T>();

        return Parse<T>(fileName, File.ReadAllText(path, Utf8));
    }

    static async Task WriteUnlockedAsync<T>(string fileName, List<T> items, CancellationToken ct)
    {
        string path = DataFiles.FullPath(fileName);
        string temp = path + ".tmp";
        string json = JsonSerializer.Serialize(items, Options);

        Directory.CreateDirectory(DataFiles.Folder);
        try
        {
            await File.WriteAllTextAsync(temp, json, Utf8, ct);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    static void WriteUnlocked<T>(string fileName, List<T> items)
    {
        string path = DataFiles.FullPath(fileName);
        string temp = path + ".tmp";
        string json = JsonSerializer.Serialize(items, Options);

        Directory.CreateDirectory(DataFiles.Folder);
        try
        {
            File.WriteAllText(temp, json, Utf8);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    static List<T> Parse<T>(string fileName, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Options)
                   ?? throw new JsonException("The file contains null instead of an array.");
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(fileName, ex);
        }
    }
}