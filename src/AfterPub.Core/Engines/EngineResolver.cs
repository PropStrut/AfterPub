namespace AfterPub.Core.Engines;

/// <summary>
/// Turns the user's <see cref="EngineSelection"/> into an actual engine to use,
/// given the engines' current availability. Kept separate from the engines
/// themselves so this selection logic can be unit-tested with fakes, without
/// needing Publisher, LibreOffice or Scribus installed (CLAUDE.md section 12).
/// </summary>
public sealed class EngineResolver
{
    /// <summary>
    /// Returns the engine to use, or null if the selection cannot be satisfied
    /// (the specifically chosen engine is unavailable, or — for Auto — none is).
    /// Auto tries Publisher, then LibreOffice, then Scribus; that order lives only here.
    /// </summary>
    /// <param name="scribusEngine">Optional so callers written before Scribus existed still compile.</param>
    public IConversionEngine? ResolveEngine(
        EngineSelection selection,
        IConversionEngine publisherEngine,
        IConversionEngine libreOfficeEngine,
        IConversionEngine? scribusEngine = null)
    {
        switch (selection)
        {
            case EngineSelection.Publisher:
                return publisherEngine.IsAvailable() ? publisherEngine : null;

            case EngineSelection.LibreOffice:
                return libreOfficeEngine.IsAvailable() ? libreOfficeEngine : null;

            case EngineSelection.Scribus:
                return scribusEngine is not null && scribusEngine.IsAvailable() ? scribusEngine : null;

            case EngineSelection.Auto:
            default:
                IConversionEngine?[] inPriorityOrder = { publisherEngine, libreOfficeEngine, scribusEngine };

                foreach (IConversionEngine? engine in inPriorityOrder)
                {
                    if (engine is not null && engine.IsAvailable())
                    {
                        return engine;
                    }
                }

                return null;
        }
    }
}
