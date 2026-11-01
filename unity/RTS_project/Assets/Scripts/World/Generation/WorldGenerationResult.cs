public enum WorldGenerationError
{
    None,
    InvalidRequest,
    PlacementFailed
}

/// <summary>
/// A generálás eredménye. Sikertelen generálás nem dob kivételt: az Error és
/// az ErrorMessage írja le az okot, hogy a host (lobby) érthető üzenetet
/// mutathasson és más beállítással vagy seeddel újrapróbálhassa.
/// </summary>
public sealed class WorldGenerationResult
{
    public bool Success => Error == WorldGenerationError.None;
    public WorldGenerationError Error { get; }
    public string ErrorMessage { get; }
    public WorldLayout Layout { get; }

    /// <summary>
    /// A felhasznált teljes generálási kísérletek száma.
    /// </summary>
    public int Attempts { get; }

    private WorldGenerationResult(
        WorldGenerationError error,
        string errorMessage,
        WorldLayout layout,
        int attempts)
    {
        Error = error;
        ErrorMessage = errorMessage;
        Layout = layout;
        Attempts = attempts;
    }

    public static WorldGenerationResult Succeeded(WorldLayout layout, int attempts)
    {
        return new WorldGenerationResult(WorldGenerationError.None, null, layout, attempts);
    }

    public static WorldGenerationResult Failed(
        WorldGenerationError error,
        string errorMessage,
        int attempts)
    {
        return new WorldGenerationResult(error, errorMessage, null, attempts);
    }
}
