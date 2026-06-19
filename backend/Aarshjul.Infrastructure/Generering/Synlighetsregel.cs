using Aarshjul.Application.Generering;
using Microsoft.Extensions.Options;

namespace Aarshjul.Infrastructure.Generering;

/// <summary>Konfigurasjon for synlighetsregelen (kravdok. 2.4). Default FIN-internt: FA + FIN-FAG.</summary>
public class SynlighetsregelOpsjoner
{
    public const string Seksjon = "Synlighetsregel";

    /// <summary>Gruppekoder som forhåndsutfylles på auto-/genererte forslag. POL fjernes alltid (kan ikke settes automatisk).</summary>
    public List<string> StandardKoder { get; set; } = ["FA", "FIN-FAG"];
}

/// <summary>
/// Konfig-drevet synlighetsregel. Returnerer de konfigurerte standardkodene, men fjerner POL
/// ubetinget — POL kan aldri settes av en automatisk regel (SYSTEMARKITEKTUR 4).
/// </summary>
public class Synlighetsregel(IOptions<SynlighetsregelOpsjoner> opsjoner) : ISynlighetsregel
{
    private const string Pol = "POL";

    public IReadOnlyList<string> StandardForslagssynlighet()
        => opsjoner.Value.StandardKoder
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Where(k => !string.Equals(k, Pol, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
