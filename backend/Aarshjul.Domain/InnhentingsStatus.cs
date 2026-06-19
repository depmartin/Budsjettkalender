namespace Aarshjul.Domain;

/// <summary>
/// Liveness-spor for den automatiske innhentingen (SYSTEMARKITEKTUR 5, BRUKERHISTORIER 4.12).
/// Én rad per kilde. Oppdagelse (<c>oppdag()</c>) og henting/uttrekk (<c>hent()</c>) spores hver
/// for seg, slik at administrator ser hvilket ledd som eventuelt står stille — en automatikk som
/// svikter stille er verre enn ingen automatikk.
/// </summary>
public class InnhentingsStatus
{
    public Guid Id { get; set; }

    /// <summary>Hvilken kilde statusen gjelder, f.eks. "regjeringen".</summary>
    public required string Kilde { get; set; }

    /// <summary>Tidspunkt for siste vellykkede oppdagelse (lesing av oversikten).</summary>
    public DateTimeOffset? SisteVellykkedeOppdagelse { get; set; }

    /// <summary>Tidspunkt for siste vellykkede henting/uttrekk.</summary>
    public DateTimeOffset? SisteVellykkedeHenting { get; set; }

    /// <summary>Tidspunkt for siste kjøring (uansett utfall).</summary>
    public DateTimeOffset? SisteForsoek { get; set; }

    /// <summary>Siste oppdagelsesutfall som tekst (FantDokumenter/IngenDokumenter/KlarteIkkeParse).</summary>
    public string? SisteUtfall { get; set; }

    /// <summary>Feilmelding når siste kjøring ikke lyktes; varsles til administrator.</summary>
    public string? SisteFeilmelding { get; set; }
}
