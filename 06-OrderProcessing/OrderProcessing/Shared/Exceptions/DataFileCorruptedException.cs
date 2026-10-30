namespace OrderProcessingSystem.Shared.Exceptions;

public sealed class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(string fileName, Exception innerException)
        : base($"The JSON data file '{fileName}' is corrupted or contains invalid JSON.", innerException)
    {
        FileName = fileName;
    }

    public string FileName { get; }
}
