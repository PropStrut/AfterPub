namespace AfterPub.Core.Engines;

/// <summary>Result of one conversion attempt.</summary>
public sealed class ConversionOutcome
{
    public bool Success { get; }

    /// <summary>Set when <see cref="Success"/> is false; a short, human-readable reason.</summary>
    public string? ErrorMessage { get; }

    private ConversionOutcome(bool success, string? errorMessage)
    {
        this.Success = success;
        this.ErrorMessage = errorMessage;
    }

    public static ConversionOutcome Ok()
    {
        return new ConversionOutcome(success: true, errorMessage: null);
    }

    public static ConversionOutcome Failed(string errorMessage)
    {
        return new ConversionOutcome(success: false, errorMessage: errorMessage);
    }
}
