namespace Aarshjul.Application.Synlighet;

/// <summary>
/// Den innloggede brukerens synlighetskontekst, slik synlighetsfilteret trenger den.
/// Administrator ser alt (<see cref="SerAlt"/>); øvrige ser frister der minst én av
/// <see cref="Grupper"/> matcher fristens synlig_for. Brukes også av administrators
/// «se som rolle», ved å konstruere en kontekst for den valgte gruppen.
/// </summary>
public interface ISynlighetskontekst
{
    /// <summary>Sant for administrator — unntatt synlighetsfilteret.</summary>
    bool SerAlt { get; }

    /// <summary>Brukerens synlighetsgrupper (gruppekoder).</summary>
    IReadOnlyCollection<string> Grupper { get; }
}

/// <summary>Enkel, uforanderlig synlighetskontekst — nyttig for «se som rolle» og tester.</summary>
public sealed class Synlighetskontekst(bool serAlt, IReadOnlyCollection<string> grupper) : ISynlighetskontekst
{
    public bool SerAlt { get; } = serAlt;
    public IReadOnlyCollection<string> Grupper { get; } = grupper;

    /// <summary>Kontekst for en enkelt gruppe (brukes av administrators «se som rolle»).</summary>
    public static Synlighetskontekst ForGruppe(string gruppeKode) => new(false, [gruppeKode]);
}
