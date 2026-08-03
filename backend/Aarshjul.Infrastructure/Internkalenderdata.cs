using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure;

/// <summary>
/// Start-sett av generelle regler for internkalenderen, utledet fra SBRs tidslinjedokument
/// «Tidslinjer – budsjett». Seedes tomt-vernet (kun når regeltabellen er tom), slik at SBR fritt
/// kan endre/slette/legge til etterpå uten at endringene overskrives ved oppstart.
///
/// Tidfesting er årsuavhengig (måned/dag eller tentativ måned/rundeposisjon). Kalenderåret ved
/// generering = rundeår + rundetypens standardforskyvning (<see cref="Runder.Aarforskyvning"/>) +
/// regelens <c>Justering</c>. For Marsrunden (standardforskyvning −1) faller november/desember-
/// oppgavene ett år tidligere igjen (justering −1); resten samme kalenderår som januar–mars-arbeidet.
/// </summary>
public static class Internkalenderdata
{
    /// <summary>Ett seedet regelsett: hvilke rundetyper det gjelder + tidfesting.</summary>
    public sealed record Regelseed(
        Rundetype[] Rundetyper,
        string Tittel,
        Tidfestingstype Type,
        int? Maaned,
        int? Dag,
        Datokvalifikator? Kvalifikator,
        Rundeposisjon? Rundeposisjon,
        int Justering);

    private static readonly Rundetype[] Hovedrunder =
        [Rundetype.Marsrunden, Rundetype.Augustrunden, Rundetype.Rnb, Rundetype.Nysaldering];

    // --- Konstruktør-hjelpere for lesbarhet ---
    private static Regelseed Dato(Rundetype r, int mnd, int dag, string tittel, int just = 0)
        => new([r], tittel, Tidfestingstype.KonkretDato, mnd, dag, null, null, just);
    private static Regelseed Mnd(Rundetype r, int mnd, Datokvalifikator? k, string tittel, int just = 0)
        => new([r], tittel, Tidfestingstype.TentativMaaned, mnd, null, k, null, just);
    private static Regelseed Pos(Rundetype r, Rundeposisjon p, string tittel, int just = 0)
        => new([r], tittel, Tidfestingstype.Rundeposisjon, null, null, null, p, just);
    private static Regelseed HverRunde(Rundeposisjon p, string tittel)
        => new(Hovedrunder, tittel, Tidfestingstype.Rundeposisjon, null, null, null, p, 0);

    public static readonly Regelseed[] Regler =
    [
        // ---------------------------------------------------------- Før hver runde (alle hovedrunder)
        HverRunde(Domain.Rundeposisjon.Start, "Etablere budsjettrom (Startsider) og lenke til rommet på SBRs startside"),
        HverRunde(Domain.Rundeposisjon.Start, "Etablere budsjett-mappe (Områdeinnhold)"),
        HverRunde(Domain.Rundeposisjon.Start, "Etablere egen websak-sak på runden"),
        HverRunde(Domain.Rundeposisjon.Start, "Booke tidspunkt i statsråden (og andres) kalender for husmøte mv. (og formøter før SMK-møte)"),
        HverRunde(Domain.Rundeposisjon.Start, "Avtale tidspunkt med trykkeri"),
        HverRunde(Domain.Rundeposisjon.Start, "Til konferansen: avtale rigging/levering av pc-utstyr"),

        // ---------------------------------------------------------- Marsrunden
        // November/desember: to kalenderår før budsjettåret (justering −1 på toppen av −1).
        Dato(Rundetype.Marsrunden, 11, 19, "Sende ut beskjed til saksbehandlere om å invitere til møter om tekniske justeringer", -1),
        Dato(Rundetype.Marsrunden, 11, 28, "Etterspørre multiplikatorer fra KDD", -1),
        Dato(Rundetype.Marsrunden, 12, 5, "Få renteforutsetninger fra ØA og send ut til FAG (husk sjekk via seksjonene)", -1),
        Dato(Rundetype.Marsrunden, 12, 17, "Ev. bestilling av tekst fra FAG om resultater (til deplistene)", -1),
        // Januar–mars: samme kalenderår som marskonferansen (budsjettår − 1).
        Dato(Rundetype.Marsrunden, 1, 6, "Spesifikasjon av FAGs driftsposter"),
        Dato(Rundetype.Marsrunden, 1, 8, "Oppstartsmail – tekniske justeringer"),
        Mnd(Rundetype.Marsrunden, 1, Datokvalifikator.Primo, "Starte på hovedbudsjettskrivet (mens det er rolig)"),
        Dato(Rundetype.Marsrunden, 1, 10, "Sende ut mail om maler til deplister"),
        Dato(Rundetype.Marsrunden, 1, 14, "Planlagte proposisjoner og meldinger for sesjonen"),
        Dato(Rundetype.Marsrunden, 1, 16, "Sende driftsresultat i statens forvaltningsbedrifter til saksbehandlere"),
        Dato(Rundetype.Marsrunden, 1, 16, "Krysspeiling udir"),
        Dato(Rundetype.Marsrunden, 1, 17, "Krysspeiling FA-led"),
        Dato(Rundetype.Marsrunden, 1, 21, "Sende mail om bidrag til SSU-notat om tekniske justeringer"),
        Dato(Rundetype.Marsrunden, 1, 21, "Sett opp beregn og SBR-regneark for første gang (beskjed til ØA)"),
        Dato(Rundetype.Marsrunden, 1, 27, "Oppstartsmail – arbeidet med budsjettiltak"),
        Dato(Rundetype.Marsrunden, 1, 28, "Oppstartsmail – arbeidet med satsingsforslag"),
        Dato(Rundetype.Marsrunden, 1, 29, "Sende notat om tekniske justeringer til POL og til SMK via forværelset"),
        Dato(Rundetype.Marsrunden, 1, 31, "SSU om tekniske justeringer"),
        Dato(Rundetype.Marsrunden, 2, 5, "Sette opp dokument for driftskreditter i helseforetak"),
        Dato(Rundetype.Marsrunden, 2, 5, "Krysspeiling underdirektører – om budsjettiltak"),
        Dato(Rundetype.Marsrunden, 2, 7, "Møte med ØA om budsjettall og fremstilling"),
        Dato(Rundetype.Marsrunden, 2, 10, "Avtale kalender med trykkeriet"),
        Dato(Rundetype.Marsrunden, 2, 10, "FAGs frist for reviderte innspill om satsinger og tabeller med nettobudsjetterte virksomheter"),
        Dato(Rundetype.Marsrunden, 2, 11, "Husmøte om makro"),
        Dato(Rundetype.Marsrunden, 2, 12, "Punchefrist 2 (oppdateringer om kutt og satsinger)"),
        Dato(Rundetype.Marsrunden, 2, 13, "Generering av depnotater budsjettiltak (kuttemeny)"),
        Dato(Rundetype.Marsrunden, 2, 14, "Møte om kutt og satsinger med FA-led/Udir"),
        Dato(Rundetype.Marsrunden, 2, 14, "Bestilling til seksjonene, covernotat – kuttemeny"),
        Dato(Rundetype.Marsrunden, 2, 17, "Ferdigstillelse førsteutkast deplister og trykking"),
        Dato(Rundetype.Marsrunden, 2, 17, "Møte med POL (statssekretærer) og FA-led – om kutt og satsinger"),
        Dato(Rundetype.Marsrunden, 2, 18, "Sende ut mail til budsjettledernettverket om å holde av tidspunkt"),
        Dato(Rundetype.Marsrunden, 2, 18, "Prat med ØA/SØ/FA om tidsplan for marsmaterialet"),
        Dato(Rundetype.Marsrunden, 2, 19, "Sende mail om generering satsingsforslag-del av departementlister"),
        Dato(Rundetype.Marsrunden, 2, 20, "Sende utkast til husmøte-notat til FA"),
        Dato(Rundetype.Marsrunden, 2, 21, "Generering av satsingsforslag-del av departementlister"),
        Dato(Rundetype.Marsrunden, 2, 21, "Beskjed om å punche sterke bindinger"),
        Dato(Rundetype.Marsrunden, 2, 24, "Husmøte 2 – utgift/skatt"),
        Dato(Rundetype.Marsrunden, 2, 24, "Sende utkast til r-notat til FA"),
        Dato(Rundetype.Marsrunden, 2, 25, "Ferdigstillelse – andreutkast deplister"),
        Dato(Rundetype.Marsrunden, 2, 26, "Møte SSU – budsjett"),
        Dato(Rundetype.Marsrunden, 2, 27, "Oppstartsmøte om FAs arbeid med plansjer"),
        Dato(Rundetype.Marsrunden, 2, 28, "Møte med POL/FA om tilbakemeldinger til deplister"),
        Dato(Rundetype.Marsrunden, 3, 3, "Mail om ferdigstillelse av materialet (dagen for endelig generering)"),
        Dato(Rundetype.Marsrunden, 3, 3, "Sende materialet til forværelset for utsending og til trykkeri for fysisk trykk"),
        Dato(Rundetype.Marsrunden, 3, 4, "Trykking, pakking og utlevering av materialet"),
        Dato(Rundetype.Marsrunden, 3, 4, "Sende ut agenda til budsjettledernettverket"),
        Dato(Rundetype.Marsrunden, 3, 5, "Sende ut mail til FA om evaluering av budsjettrunden"),
        Dato(Rundetype.Marsrunden, 3, 5, "SSU om hva som kan bli endelig utfall (særlig profilpott)"),
        Dato(Rundetype.Marsrunden, 3, 5, "Budsjettledernettverk"),
        Dato(Rundetype.Marsrunden, 3, 7, "Sette opp tidspunkt for evalueringsmøte for mars-materialet"),
        Dato(Rundetype.Marsrunden, 3, 10, "Marskonferansen (10.–12. mars)"),
        Dato(Rundetype.Marsrunden, 3, 13, "Punchefrist for endringer på konferansen (kl. 13:00)"),
        Dato(Rundetype.Marsrunden, 3, 13, "Sende ut mail om prisomregning av marsrammene"),
        Dato(Rundetype.Marsrunden, 3, 13, "Evalueringsmøte med ØA/SØ om marsmaterialet"),
        Dato(Rundetype.Marsrunden, 3, 14, "Beskjed til seksjonene om rammebrev og hovedbudsjettskriv"),
        Dato(Rundetype.Marsrunden, 3, 18, "Endre prisomregningsfaktorer/deflatorer og kjøre automatisk prisomregning"),
        Dato(Rundetype.Marsrunden, 3, 18, "Beskjed til FA om at automatisk prisomregning er kjørt"),
        Dato(Rundetype.Marsrunden, 3, 18, "Endre budsjettår i FIA fra t+1 (MARS) til t (RNB)"),
        Dato(Rundetype.Marsrunden, 3, 21, "Rammebrevene sendes ut fra seksjonene til FAG"),
        Dato(Rundetype.Marsrunden, 3, 24, "Sende ut hovedbudsjettskriv (til departementene og last opp på regjeringen.no)"),

        // ---------------------------------------------------------- RNB (samme kalenderår som budsjettåret)
        Mnd(Rundetype.Rnb, 1, Datokvalifikator.Primo, "Starte på Rundskriv R-3 (når det er rolig)"),
        Mnd(Rundetype.Rnb, 1, Datokvalifikator.Primo, "Mal for r-notater og deplister (når det er rolig)"),
        Dato(Rundetype.Rnb, 2, 20, "Avtale med ØA når man setter renteforutsetninger"),
        Dato(Rundetype.Rnb, 3, 12, "Få renteforutsetninger fra ØA og send ut til FAG (husk sjekk via seksjonene)"),
        Dato(Rundetype.Rnb, 3, 17, "FAGs frist til RNB-innspill"),
        Dato(Rundetype.Rnb, 3, 17, "Sende ut oppstartsbrev for RNB-arbeidet til FA"),
        Dato(Rundetype.Rnb, 3, 18, "Avtale mal for RNB-materialet med andre avdelinger"),
        Dato(Rundetype.Rnb, 3, 20, "Avtale genereringer med trykkeriet"),
        Dato(Rundetype.Rnb, 3, 21, "Sende ut oppgave om driftsresultat i statens forvaltningsbedrifter til saksbehandlere"),
        Dato(Rundetype.Rnb, 3, 24, "Lage første beregn og SBR-regneark"),
        Dato(Rundetype.Rnb, 3, 24, "Gjøre første anslag for poster der SBR har ansvar"),
        Dato(Rundetype.Rnb, 3, 25, "Sette opp dokument for driftskreditter i helseforetak"),
        Dato(Rundetype.Rnb, 3, 25, "Foreløpige anslag på KPI, lønn, sysselsetting og ledighet"),
        Dato(Rundetype.Rnb, 3, 26, "Første punchefrist"),
        Dato(Rundetype.Rnb, 3, 26, "Krysspeiling udir"),
        Dato(Rundetype.Rnb, 3, 26, "Starte arbeidet med covernotat til departementslistene"),
        Dato(Rundetype.Rnb, 3, 27, "Ev. endringer for sysselsettingsanslag fra ØA"),
        Dato(Rundetype.Rnb, 3, 28, "Ev. endringer for ledighetsanslaget"),
        Dato(Rundetype.Rnb, 3, 28, "Utsendelse av covernotat til førsteutkast til departementslister"),
        Dato(Rundetype.Rnb, 3, 31, "Møte med SØ/ØA/SL/Lars-Henrik om RNB (forberede husmøte)"),
        Dato(Rundetype.Rnb, 3, 31, "Generering første deplisteutkast"),
        Dato(Rundetype.Rnb, 4, 2, "Møte med POL om FAs RNB-saker"),
        Dato(Rundetype.Rnb, 4, 2, "(Foreløpige) tall fra ØA til husmøtet"),
        Dato(Rundetype.Rnb, 4, 3, "Husmøte (eneste husmøtet i RNB)"),
        Dato(Rundetype.Rnb, 4, 3, "Sende utkast til seksjonene om SMK-notat"),
        Dato(Rundetype.Rnb, 4, 4, "Avtale med trykkeri (endelig trykking og andreutkast)"),
        Dato(Rundetype.Rnb, 4, 7, "Generering av oppdaterte deplister"),
        Dato(Rundetype.Rnb, 4, 9, "SSU-møte om RNB"),
        Dato(Rundetype.Rnb, 4, 9, "R-notat om utgift til POL"),
        Dato(Rundetype.Rnb, 4, 10, "Møte mellom finansministeren og statsministeren"),
        Dato(Rundetype.Rnb, 4, 22, "Endelig generering: ferdigstillelse og elektronisk utsendelse av materialet"),
        Dato(Rundetype.Rnb, 4, 23, "Trykking, pakking og utlevering av materialet"),
        Dato(Rundetype.Rnb, 4, 23, "Avtale rigging/levering av pc-utstyr til konferansen"),
        Dato(Rundetype.Rnb, 4, 24, "Budsjettledernettverk"),
        Dato(Rundetype.Rnb, 4, 25, "SSU-møte om RNB"),
        Dato(Rundetype.Rnb, 4, 28, "Forberedelsesdag statsråden/POL til RNB"),
        Dato(Rundetype.Rnb, 4, 29, "RNB-konferansen"),
        Dato(Rundetype.Rnb, 5, 5, "FAGs frist for tekstbidrag til RNB-prop"),
        Dato(Rundetype.Rnb, 5, 8, "Seksjonenes frist for bidrag til RNB-prop"),
        Dato(Rundetype.Rnb, 5, 8, "Skrive inn tall i kapittel 1.3 (og resten av kapittel 1)"),
        Dato(Rundetype.Rnb, 5, 9, "Melde opp proposisjonen til statsråd"),
        Dato(Rundetype.Rnb, 5, 9, "RNB-prop til POL"),
        Dato(Rundetype.Rnb, 5, 12, "Lage tabell 2.2 (fra gul bok) klar til NRK"),
        Dato(Rundetype.Rnb, 5, 12, "Sende kontoplan til Stortinget"),
        Dato(Rundetype.Rnb, 5, 12, "Lage vedlegg til RNB-prop"),
        Dato(Rundetype.Rnb, 5, 13, "Kontroll av vedlegg (inkl. vedtaksliste)"),
        Dato(Rundetype.Rnb, 5, 14, "RNB-prop trykkes"),
        Dato(Rundetype.Rnb, 5, 15, "RNB-prop legges frem"),
        Mnd(Rundetype.Rnb, 5, Datokvalifikator.Ultimo, "Korreksjoner av proposisjonen og sende RNB-tall etter forlik"),

        // ---------------------------------------------------------- Augustrunden (budsjettår − 1)
        Dato(Rundetype.Augustrunden, 6, 1, "Ferdigstille deplistemal, disposisjon budsjettmateriale og maler (r-notater, tekniske justeringer)"),
        Dato(Rundetype.Augustrunden, 6, 11, "Sende ut prosessbeskjed for arbeidet med tekniske justeringer"),
        Dato(Rundetype.Augustrunden, 6, 12, "Få sendt ut renteforutsetninger fra ØA"),
        Mnd(Rundetype.Augustrunden, 6, Datokvalifikator.Medio, "Avtale genereringer med trykkeriet"),
        Mnd(Rundetype.Augustrunden, 8, null, "Sende mail om forvaltningsbedriftene"),
        Dato(Rundetype.Augustrunden, 7, 10, "Minne OS om takstoppgjøret ifm. augustkonferansen"),
        Dato(Rundetype.Augustrunden, 7, 21, "Frist for FAGs rammefordelingsforslag"),
        Dato(Rundetype.Augustrunden, 7, 21, "Sende oppstartsmail til FA"),
        Dato(Rundetype.Augustrunden, 7, 22, "Avtale tidspunkt for trykking med trykkeri"),
        Dato(Rundetype.Augustrunden, 7, 25, "Sende ut dokument om statens forvaltningsbedrifter (med frist på punchefristen)"),
        Dato(Rundetype.Augustrunden, 7, 28, "Sende påminnelse om bevilgningsendringer andre halvår"),
        Dato(Rundetype.Augustrunden, 7, 28, "Starte å legge inn i dokumentet «Skissemessig oppsummering»"),
        Mnd(Rundetype.Augustrunden, 7, Datokvalifikator.Ultimo, "Starte på budsjettskrivet (R-6) og R-3 (mens det er rolig)"),
        Dato(Rundetype.Augustrunden, 7, 30, "Sendt ut notat for tekniske justeringer til seksjonene til sjekk"),
        Mnd(Rundetype.Augustrunden, 8, null, "Utsending av r-notat til FA"),
        Mnd(Rundetype.Augustrunden, 8, null, "Lage rundskriv R-6 (gul bok)"),
        Mnd(Rundetype.Augustrunden, 9, Datokvalifikator.Primo, "Klargjøre tallgrunnlag til publisering av gul bok"),
        Dato(Rundetype.Augustrunden, 9, 10, "Lage tabeller til kap. 6.2"),
        Dato(Rundetype.Augustrunden, 9, 15, "Lage tabeller til kap. 3 og 6.2, tall til kap. 2 og vedlegg 2 (tabell 2.1 og 2.8)"),
        Mnd(Rundetype.Augustrunden, 9, Datokvalifikator.Medio, "Avtale distribusjon, sperrefrist mv."),
        Mnd(Rundetype.Augustrunden, 9, null, "Melde opp proposisjon til statsråd"),
        Dato(Rundetype.Augustrunden, 9, 26, "Lage faktaark og datafil"),
        Pos(Rundetype.Augustrunden, Domain.Rundeposisjon.Sent, "Lage utsendelsesbrev og brev til Slottet"),
        Pos(Rundetype.Augustrunden, Domain.Rundeposisjon.Sent, "Bestille vann/kaffe til BLN og sende inn navn til vakta"),
        Pos(Rundetype.Augustrunden, Domain.Rundeposisjon.Sent, "Endelig budsjettmateriale ferdigstilt og sendt ut"),
        Mnd(Rundetype.Augustrunden, 10, Datokvalifikator.Primo, "Fremleggelse av gul bok (statsbudsjettet)"),

        // ---------------------------------------------------------- Nysalderingen (samme kalenderår som budsjettåret)
        Dato(Rundetype.Nysaldering, 9, 1, "Sende ut rundskriv R-7 (nysaldering). Merk: renteforutsetninger sendes IKKE ut fra ØA"),
        Mnd(Rundetype.Nysaldering, 9, null, "FAGs frist for innspill til nysalderingen"),
        Mnd(Rundetype.Nysaldering, 11, Datokvalifikator.Primo, "Avtale genereringer med trykkeriet"),
        Mnd(Rundetype.Nysaldering, 11, null, "Sende mail om forvaltningsbedriftene (driftsresultat i statens forvaltningsbedrifter)"),
        Mnd(Rundetype.Nysaldering, 11, Datokvalifikator.Medio, "Melde opp sak/proposisjon til statsråd"),
        Pos(Rundetype.Nysaldering, Domain.Rundeposisjon.Sent, "Lage utsendelsesbrev og brev til Slottet"),
    ];

    /// <summary>Seeder start-settet av generelle regler kun når regeltabellen er tom.</summary>
    public static async Task SeedReglerAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.GjoeremaalRegler.AnyAsync(ct))
        {
            return;
        }

        foreach (var s in Regler)
        {
            var regel = new GjoeremaalRegel
            {
                Id = Guid.NewGuid(),
                Tittel = s.Tittel,
                Tidfestingstype = s.Type,
                Maaned = s.Maaned,
                Dag = s.Dag,
                Datokvalifikator = s.Kvalifikator,
                Rundeposisjon = s.Rundeposisjon,
                AarforskyvningJustering = s.Justering
            };
            foreach (var t in s.Rundetyper)
            {
                regel.Rundetyper.Add(new RegelRundetype { RegelId = regel.Id, Rundetype = t });
            }
            db.GjoeremaalRegler.Add(regel);
        }
    }
}
