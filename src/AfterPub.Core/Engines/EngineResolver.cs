namespace AfterPub.Core.Engines;

/// <summary>
/// Turns the user's <see cref="EngineSelection"/> into an actual engine to use,
/// given both engines' current availability. Kept separate from the engines
/// themselves so this selection logic can be unit-tested with fakes, without
/// needing Publisher or LibreOffice installed (CLAUDE.md section 12).
/// </summary>
public sealed class EngineResolver
{
    /// <summary>
    /// Returns the engine to use, or null if the selection cannot be satisfied
    /// (the specifically chosen engine is unavailable, or — for Auto — neither is).
    /// </summary>
    public IConversionEngine? ResolveEngine(
        EngineSelection selection,
        IConversionEngine publisherEngine,
        IConversionEngine libreOfficeEngine)
    {
        switch (selection)
        {
            case EngineSelection.Publisher:
                return publisherEngine.IsAvailable() ? publisherEngine : null;

            case EngineSelection.LibreOffice:
                return libreOfficeEngine.IsAvailable() ? libreOfficeEngine : null;

            case EngineSelection.Auto:
            default:
                if (publisherEngine.IsAvailable())
                {
                    return publisherEngine;
                }

                if (libreOfficeEngine.IsAvailable())
                {
                    return libreOfficeEngine;
                }

                return null;
        }
    }
}
