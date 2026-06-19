using Aarshjul.Domain;

namespace Aarshjul.Application.Generering;

/// <summary>
/// Resultatet av å beregne én gjentaksregel for et målbudsjettår. Bærer enten en beregnet dato
/// (med presisjon) eller en <see cref="Feil"/> (uoppløselig/sirkulær anker-kjede), pluss flagg for
/// tentativitet og valgårssensitivitet.
/// </summary>
public sealed record BeregnetRegel
{
    public required Gjentaksregel Regel { get; init; }

    /// <summary>Beregnet dato, eller <c>null</c> når beregningen feilet (se <see cref="Feil"/>).</summary>
    public DateOnly? Dato { get; init; }

    public Datopresisjon Presisjon { get; init; } = Datopresisjon.Dag;

    /// <summary>Sant når fristen mangler en fastsatt dag — enten valgårssensitiv i stortingsvalgår
    /// eller arvet fra et tentativt anker.</summary>
    public bool Tentativ { get; init; }

    /// <summary>Sant når fristen skal markeres for valgår (per frist, ikke banner — kravdok. 6).</summary>
    public bool Valgaarsflagg { get; init; }

    /// <summary>Feilmelding når anker-kjeden er sirkulær eller mangler et anker-løp.</summary>
    public string? Feil { get; init; }

    public bool ErFeil => Feil is not null;
}

/// <summary>
/// Ren beregning av en årsmal for et målbudsjettår (SYSTEMARKITEKTUR 8). Tar gjentaksreglene og
/// eventuelle manuelt satte ankre inn, og gir per regel en beregnet dato eller en tydelig feil.
/// Ingen database, ingen sideeffekter — fullt testbar.
///
/// Bærende regler (designintervju 2026-06-19):
/// - <see cref="Regeltype.RelativTilMilepael"/> løses i avhengighetsrekkefølge; sirkulære eller
///   uoppløselige kjeder gir <see cref="BeregnetRegel.Feil"/> framfor en gjettet dato.
/// - Tentativitet arves nedover kjeden: en frist som henger på et tentativt anker blir selv
///   tentativ og arver ankerets presisjon — også uten selv å være valgårssensitiv.
/// - Valgårssensitiv regel i stortingsvalgår settes tentativ (månedspresisjon), ikke gjettet dag.
///   Kommunevalgår gir en mildere markering (flagg), men beholder konkret dato.
/// </summary>
public sealed class Genereringsberegning
{
    private readonly int _maalbudsjettaar;
    private readonly IReadOnlyDictionary<string, Gjentaksregel> _regelPerLoep;
    private readonly IReadOnlyDictionary<string, DateOnly> _manuelleAnkre;
    private readonly Dictionary<string, BeregnetRegel> _memo = new(StringComparer.OrdinalIgnoreCase);

    private Genereringsberegning(
        int maalbudsjettaar,
        IReadOnlyDictionary<string, Gjentaksregel> regelPerLoep,
        IReadOnlyDictionary<string, DateOnly> manuelleAnkre)
    {
        _maalbudsjettaar = maalbudsjettaar;
        _regelPerLoep = regelPerLoep;
        _manuelleAnkre = manuelleAnkre;
    }

    /// <summary>
    /// Beregner alle reglene for målbudsjettåret. <paramref name="manuelleAnkre"/> er løp → dato
    /// satt av administrator (to-trinns flyt: valgårssensitive ankre settes først, deretter
    /// beregnes resten fra dem).
    /// </summary>
    public static IReadOnlyList<BeregnetRegel> Beregn(
        int maalbudsjettaar,
        IReadOnlyList<Gjentaksregel> regler,
        IReadOnlyDictionary<string, DateOnly>? manuelleAnkre = null)
    {
        var perLoep = new Dictionary<string, Gjentaksregel>(StringComparer.OrdinalIgnoreCase);
        foreach (var regel in regler)
        {
            // Ved duplikate løp vinner den første; malen bør ha ett løp per regel.
            perLoep.TryAdd(regel.Loep, regel);
        }

        var beregning = new Genereringsberegning(
            maalbudsjettaar, perLoep,
            manuelleAnkre ?? new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase));

        return regler.Select(r => beregning.Resolve(r.Loep, new HashSet<string>(StringComparer.OrdinalIgnoreCase))).ToList();
    }

    private BeregnetRegel Resolve(string loep, HashSet<string> besoeker)
    {
        if (_memo.TryGetValue(loep, out var ferdig))
        {
            return ferdig;
        }

        if (!_regelPerLoep.TryGetValue(loep, out var regel))
        {
            // Et anker uten egen regel kan likevel være satt manuelt av administrator.
            if (_manuelleAnkre.TryGetValue(loep, out var manuell))
            {
                return new BeregnetRegel { Regel = SyntetiskAnker(loep), Dato = manuell };
            }

            return new BeregnetRegel { Regel = SyntetiskAnker(loep), Feil = $"Mangler anker-løp «{loep}»." };
        }

        // Manuelt satt anker overstyrer beregning (to-trinns flyt).
        if (_manuelleAnkre.TryGetValue(loep, out var manueltSatt))
        {
            return Lagre(loep, new BeregnetRegel { Regel = regel, Dato = manueltSatt });
        }

        if (!besoeker.Add(loep))
        {
            return new BeregnetRegel { Regel = regel, Feil = $"Sirkulær anker-kjede oppdaget ved «{loep}»." };
        }

        var resultat = regel.Regeltype switch
        {
            Regeltype.FastDato => BeregnFastDato(regel),
            Regeltype.RelativUkedag => BeregnRelativUkedag(regel),
            Regeltype.RelativTilMilepael => BeregnRelativTilMilepael(regel, besoeker),
            _ => new BeregnetRegel { Regel = regel, Feil = $"Ukjent regeltype for «{loep}»." }
        };

        besoeker.Remove(loep);
        return Lagre(loep, resultat);
    }

    private BeregnetRegel BeregnFastDato(Gjentaksregel regel)
    {
        FastDatoParametre p;
        try
        {
            p = Regelparser.FastDato(regel.Regelparametre);
        }
        catch (FormatException e)
        {
            return new BeregnetRegel { Regel = regel, Feil = e.Message };
        }

        var kalenderaar = _maalbudsjettaar + p.AarForskyvning;
        DateOnly dato;
        try
        {
            dato = Datoberegning.FastDato(kalenderaar, p.Maaned, p.Dag);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new BeregnetRegel { Regel = regel, Feil = $"Ugyldig måned/dag ({p.Maaned}/{p.Dag}) for «{regel.Loep}»." };
        }

        return MedValgaar(regel, dato);
    }

    private BeregnetRegel BeregnRelativUkedag(Gjentaksregel regel)
    {
        RelativUkedagParametre p;
        DayOfWeek ukedag;
        try
        {
            p = Regelparser.RelativUkedag(regel.Regelparametre);
            ukedag = Regelparser.TolkUkedag(p.Ukedag);
        }
        catch (FormatException e)
        {
            return new BeregnetRegel { Regel = regel, Feil = e.Message };
        }

        var kalenderaar = _maalbudsjettaar + p.AarForskyvning;
        DateOnly dato;
        try
        {
            dato = Datoberegning.NteUkedag(kalenderaar, p.Maaned, p.Uke, ukedag);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new BeregnetRegel { Regel = regel, Feil = $"Ugyldig måned ({p.Maaned}) for «{regel.Loep}»." };
        }

        return MedValgaar(regel, dato);
    }

    private BeregnetRegel BeregnRelativTilMilepael(Gjentaksregel regel, HashSet<string> besoeker)
    {
        RelativTilMilepaelParametre p;
        try
        {
            p = Regelparser.RelativTilMilepael(regel.Regelparametre);
        }
        catch (FormatException e)
        {
            return new BeregnetRegel { Regel = regel, Feil = e.Message };
        }

        var anker = Resolve(p.AnkerLoep, besoeker);
        if (anker.ErFeil)
        {
            // Forplant feilen nedover kjeden — heller en tydelig feil enn en vilkårlig dato.
            return new BeregnetRegel { Regel = regel, Feil = anker.Feil };
        }

        if (anker.Dato is not { } ankerDato)
        {
            return new BeregnetRegel { Regel = regel, Feil = $"Anker «{p.AnkerLoep}» mangler dato." };
        }

        var dato = ankerDato.AddDays(p.OffsetDager);

        // Tentativitet arves: arver ankerets presisjon, ikke bare dato (designintervju 2026-06-19).
        // En frist kan dermed bli tentativ uten selv å være valgårssensitiv.
        var resultat = new BeregnetRegel
        {
            Regel = regel,
            Dato = dato,
            Presisjon = anker.Presisjon,
            Tentativ = anker.Tentativ
        };

        // Egen valgårssensitivitet kan i tillegg gjøre fristen tentativ.
        return KombinerEgenValgaar(resultat, dato);
    }

    /// <summary>Påfører valgårslogikk på en fastberegnet dato (FastDato/RelativUkedag).</summary>
    private BeregnetRegel MedValgaar(Gjentaksregel regel, DateOnly dato)
        => KombinerEgenValgaar(new BeregnetRegel { Regel = regel, Dato = dato }, dato);

    /// <summary>
    /// Anvender regelens egen valgårssensitivitet på et (eventuelt allerede tentativt) resultat:
    /// stortingsvalgår → tentativ (månedspresisjon) + flagg; kommunevalgår → mildt flagg, konkret
    /// dato beholdes. Valgår avgjøres ut fra kalenderåret fristen faktisk faller i.
    /// </summary>
    private static BeregnetRegel KombinerEgenValgaar(BeregnetRegel resultat, DateOnly dato)
    {
        if (!resultat.Regel.Valgaarssensitiv)
        {
            return resultat;
        }

        return Valgaar.Type(dato.Year) switch
        {
            Valgtype.Stortingsvalg => resultat with
            {
                Tentativ = true,
                Presisjon = Datopresisjon.Maaned,
                Valgaarsflagg = true
            },
            Valgtype.Kommunevalg => resultat with { Valgaarsflagg = true },
            _ => resultat
        };
    }

    private BeregnetRegel Lagre(string loep, BeregnetRegel resultat)
    {
        _memo[loep] = resultat;
        return resultat;
    }

    private static Gjentaksregel SyntetiskAnker(string loep)
        => new() { Loep = loep, Regeltype = Regeltype.FastDato };
}
