using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Conversion;

/// <summary>The outcome of converting one source file, broken down by output target.</summary>
public sealed class RowConversionResult
{
    public IReadOnlyDictionary<OutputTarget, ConversionOutcome> OutcomesByTarget { get; }

    public RowConversionResult(IReadOnlyDictionary<OutputTarget, ConversionOutcome> outcomesByTarget)
    {
        this.OutcomesByTarget = outcomesByTarget;
    }

    public ConversionOutcome? ForTarget(OutputTarget target)
    {
        return this.OutcomesByTarget.TryGetValue(target, out ConversionOutcome? outcome) ? outcome : null;
    }
}
