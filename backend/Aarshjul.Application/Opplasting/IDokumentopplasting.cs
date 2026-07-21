namespace Aarshjul.Application.Opplasting;

/// <summary>Ett dokument administrator laster opp for uttrekk.</summary>
public sealed record OpplastetDokument(string Filnavn, byte[] Innhold);

/// <summary>Resultatet av å lese gjennom et opplastet dokument.</summary>
public sealed record OpplastingsResultat
{
    /// <summary>Antall forslag lagt i godkjenningskøen fra dokumentet.</summary>
    public int AntallForslag { get; init; }

    /// <summary>Sant hvis dokumentet (samme kilde+nøkkel+innhold) allerede er behandlet — da lages ingen nye forslag.</summary>
    public bool AlleredeBehandlet { get; init; }

    /// <summary>Kort melding til administrator (f.eks. «ingen datoer funnet»).</summary>
    public string? Melding { get; init; }
}

/// <summary>
/// Manuell dokumentopplasting (administratorfunksjon). Leser teksten ut av et opplastet dokument
/// og kjører <b>samme uttrekk og klassifisering</b> som den automatiske innhentingen fra
/// regjeringen.no: resultatet blir <c>Forslag</c> med per-felt <c>UttrekksBevis</c> i den vanlige
/// godkjenningskøen. Opplasting er bare en alternativ inntaksmåte foran samme uttrekks- og
/// køledd — den hopper over <c>oppdag()</c>/<c>hent()</c> (kildeleddet). Ingenting publiseres uten
/// godkjenning; det bærende prinsippet gjelder uendret for både automatisk og opplastet uttrekk.
/// </summary>
public interface IDokumentopplasting
{
    Task<OpplastingsResultat> LesOgLagForslagAsync(OpplastetDokument dokument, int budsjettaar, CancellationToken ct = default);
}
