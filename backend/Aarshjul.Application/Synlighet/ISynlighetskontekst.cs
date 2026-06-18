using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Domain;

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

    /// <summary>
    /// Bygger konteksten fra den innloggede principalens berikede claims (rolle + grupper).
    /// Brukes av Blazor-komponenter, der HttpContext ikke er tilgjengelig i den interaktive kretsen.
    /// </summary>
    public static Synlighetskontekst FraPrincipal(ClaimsPrincipal principal)
    {
        var serAlt = principal.FindFirst(Brukerclaims.Rolle)?.Value == nameof(Funksjonsrolle.Administrator);
        var grupper = principal.FindAll(Brukerclaims.Gruppe).Select(c => c.Value).ToArray();
        return new Synlighetskontekst(serAlt, grupper);
    }
}
