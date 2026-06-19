using Aarshjul.Application.Brukere;
using Aarshjul.Domain;

namespace Aarshjul.Application.Brukerforslag;

/// <summary>
/// Inndata fra bidragsyterens forslagsskjema (kravdok. 5.3) — enklere enn administratorens:
/// ingen løp, og synlighet er kun et <b>forslag</b> administrator kan overstyre. Navn settes
/// aldri her; det utledes fra innlogget identitet.
/// </summary>
public class BrukerforslagInndata
{
    public string Tittel { get; set; } = "";
    public DateOnly Dato { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public Datopresisjon Datopresisjon { get; set; } = Datopresisjon.Dag;
    public Datokvalifikator? Datokvalifikator { get; set; }
    public int Budsjettaar { get; set; } = DateTime.Today.Year + 1;
    public Kategori Kategori { get; set; } = Kategori.Budsjett;
    public string? Notat { get; set; }

    /// <summary>Foreslåtte synlighetsgrupper. Kan inkludere POL som forslag — admin må uansett bekrefte POL aktivt.</summary>
    public List<string> ForeslaattSynlighet { get; set; } = [];

    /// <summary>Satt for endringsforslag mot en publisert frist (kravdok. 3.5/4.4 — Steg I).</summary>
    public Guid? EndrerFristId { get; set; }
}

/// <summary>Et element i bidragsyterens «mine forslag»-oversikt (kravdok. 5.3 / BRUKERHISTORIER 3.2).</summary>
public sealed record MineForslagElement(
    Guid Id,
    ForslagType ForslagType,
    string Tittel,
    DateOnly? Dato,
    int Budsjettaar,
    Kategori Kategori,
    FristStatus Status,
    IReadOnlyList<string> ForeslaattSynlighet,
    Guid? EndrerFristId);

/// <summary>
/// Bidragsyterens innsending av forslag (kravdok. 5.3, BRUKERHISTORIER 3.1–3.5). Forslaget går
/// i samme godkjenningskø som robotforslag, usynlig for andre til administrator godkjenner.
/// Et avvist forslag bevares og kan redigeres og sendes inn på nytt.
/// </summary>
public interface IForslagsinnsending
{
    Task<Guid> SendInnAsync(BrukerforslagInndata inndata, GjeldendeBruker innsender, CancellationToken ct = default);

    Task<IReadOnlyList<MineForslagElement>> HentMineAsync(string brukerId, CancellationToken ct = default);

    /// <summary>Henter et avvist forslag eieren kan redigere, eller null om det ikke finnes/ikke eies.</summary>
    Task<BrukerforslagInndata?> HentForRedigeringAsync(Guid forslagId, string brukerId, CancellationToken ct = default);

    /// <summary>Sender et avvist forslag inn på nytt med justerte verdier (samme rad, status tilbake til forslag).</summary>
    Task SendInnPaaNyttAsync(Guid forslagId, BrukerforslagInndata inndata, GjeldendeBruker innsender, CancellationToken ct = default);
}
