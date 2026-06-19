namespace Aarshjul.Kilder;

/// <summary>Utfall av en <see cref="IKilde.HentAsync"/>-kjøring.</summary>
public enum Hentutfall
{
    /// <summary>Dokumentet ble lastet ned.</summary>
    Lyktes,

    /// <summary>Nedlastingen feilet. Prøves et fast antall ganger før det flagges til administrator (kravdok. 3.4).</summary>
    Feilet
}

/// <summary>
/// Resultatet av <see cref="IKilde.HentAsync"/>: det nedlastede råinnholdet, eller en feil.
/// Selve tekst-/datouttrekket skjer i et senere ledd (Steg E), ikke i kilden.
/// </summary>
public sealed record HentResultat
{
    public required Hentutfall Utfall { get; init; }

    /// <summary>Råinnholdet (typisk PDF-bytes) ved <see cref="Hentutfall.Lyktes"/>.</summary>
    public byte[]? Innhold { get; init; }

    /// <summary>MIME-type for innholdet der den er kjent, f.eks. "application/pdf".</summary>
    public string? MediaType { get; init; }

    /// <summary>Beskrivelse når <see cref="Utfall"/> er <see cref="Hentutfall.Feilet"/>.</summary>
    public string? Feilmelding { get; init; }

    public static HentResultat Ok(byte[] innhold, string? mediaType = null) =>
        new() { Utfall = Hentutfall.Lyktes, Innhold = innhold, MediaType = mediaType };

    public static HentResultat Feil(string feilmelding) =>
        new() { Utfall = Hentutfall.Feilet, Feilmelding = feilmelding };
}
