namespace Aarshjul.Kilder;

/// <summary>
/// Utbyttbart kildeledd (kravdok. 4.1, SYSTEMARKITEKTUR 5). Resten av systemet er
/// kildeagnostisk: alle kilder leverer inn i samme klassifisering og godkjenningskø.
/// Første implementasjon er <c>RegjeringenKilde</c>; <c>DFOeKilde</c> kan kobles på senere
/// bak samme grensesnitt uten ombygging.
/// </summary>
public interface IKilde
{
    /// <summary>
    /// Stabil kode for kilden, f.eks. "regjeringen". Brukes som <c>BehandletDokument.Kilde</c>
    /// og i den unike dedup-nøkkelen (kilde + dokumentnøkkel).
    /// </summary>
    string Kode { get; }

    /// <summary>
    /// Les kildens oversikt og returner referanser til dokumentene den tilbyr, med et
    /// eksplisitt utfall (<see cref="OppdagResultat"/>) som skiller en vellykket, tom kjøring
    /// fra en parse-feil. Gjør ingen dedup — det skjer nedstrøms (Steg C).
    /// </summary>
    Task<OppdagResultat> OppdagAsync(CancellationToken ct = default);

    /// <summary>
    /// Last ned råinnholdet for ett dokument. Tekst-/datouttrekk skjer i et senere ledd (Steg E).
    /// </summary>
    Task<HentResultat> HentAsync(Dokumentreferanse referanse, CancellationToken ct = default);
}
