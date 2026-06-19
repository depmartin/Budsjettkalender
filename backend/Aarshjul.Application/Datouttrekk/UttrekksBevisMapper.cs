using Aarshjul.Domain;

namespace Aarshjul.Application.Datouttrekk;

/// <summary>
/// Mapper et per-felt-uttrekksresultat til <see cref="UttrekksBevis"/>-rader som senere henges på
/// et <c>Forslag</c> (Steg E). Holder uttrekksabstraksjonen (Application) adskilt fra entiteten.
/// </summary>
public static class UttrekksBevisMapper
{
    public static UttrekksBevis TilBevis(this Uttrekksfelt felt) => new()
    {
        Id = Guid.NewGuid(),
        Felt = felt.Felt,
        TolketVerdi = felt.TolketVerdi,
        Kildeutdrag = felt.Kildeutdrag,
        Konfidens = felt.Konfidens
    };

    public static IReadOnlyList<UttrekksBevis> TilBevis(this Uttrekksresultat resultat) =>
        resultat.Felter.Select(f => f.TilBevis()).ToList();
}
