using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>
/// Synkronisering av en konkret runde mot regelendringer (trinn 3): forslag om legg til / oppdater
/// / fjern, som administrator godtar enkeltvis — og som aldri rører avhukede eller manuelt endrede.
/// </summary>
public class InternkalenderSynkTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private readonly InternkalenderTjeneste _kal;
    private readonly GjoeremaalRegelTjeneste _regler;

    public InternkalenderSynkTester()
    {
        _kal = new InternkalenderTjeneste(_t.Db, new FastKlokke(new DateOnly(2026, 8, 3)));
        _regler = new GjoeremaalRegelTjeneste(_t.Db);
    }

    private async Task<Guid> LeggRegel(string tittel, Rundeposisjon pos)
        => await _regler.OpprettAsync(new RegelInndata
        {
            Tittel = tittel,
            Rundetyper = [Rundetype.Augustrunden],
            Tidfestingstype = Tidfestingstype.Rundeposisjon,
            Rundeposisjon = pos
        });

    [Fact]
    public async Task Ny_regel_gir_forslag_om_a_legge_til()
    {
        await LeggRegel("Første", Rundeposisjon.Start);
        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");

        await LeggRegel("Ny regel etterpå", Rundeposisjon.Sent);
        var forslag = await _kal.ForberedSynkAsync(runde);
        var leggTil = Assert.Single(forslag);
        Assert.Equal(SynkHandling.LeggTil, leggTil.Handling);

        await _kal.SynkroniserAsync(runde, forslag);
        Assert.Equal(2, (await _kal.HentGjoeremaalAsync(runde, null)).Count);
    }

    [Fact]
    public async Task Endret_regel_gir_oppdateringsforslag_som_endrer_gjoeremaalet()
    {
        var regelId = await LeggRegel("Oppgave", Rundeposisjon.Start);
        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        Assert.Equal(new DateOnly(2026, 7, 1), (await _kal.HentGjoeremaalAsync(runde, null)).Single().Sorteringsdag);

        // Endre regelens posisjon til slutten av runden.
        await _regler.OppdaterAsync(regelId, new RegelInndata
        {
            Tittel = "Oppgave", Rundetyper = [Rundetype.Augustrunden],
            Tidfestingstype = Tidfestingstype.Rundeposisjon, Rundeposisjon = Rundeposisjon.Slutt
        });

        var forslag = await _kal.ForberedSynkAsync(runde);
        Assert.Equal(SynkHandling.Oppdater, Assert.Single(forslag).Handling);

        await _kal.SynkroniserAsync(runde, forslag);
        Assert.Equal(new DateOnly(2026, 10, 31), (await _kal.HentGjoeremaalAsync(runde, null)).Single().Sorteringsdag);
    }

    [Fact]
    public async Task Slettet_regel_gir_fjern_forslag()
    {
        var regelId = await LeggRegel("Skal fjernes", Rundeposisjon.Start);
        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        await _regler.SlettAsync(regelId);

        var forslag = await _kal.ForberedSynkAsync(runde);
        Assert.Equal(SynkHandling.Fjern, Assert.Single(forslag).Handling);

        await _kal.SynkroniserAsync(runde, forslag);
        Assert.Empty(await _kal.HentGjoeremaalAsync(runde, null));
    }

    [Fact]
    public async Task Sync_rorer_ikke_manuelt_endret_gjoeremaal()
    {
        var regelId = await LeggRegel("Oppgave", Rundeposisjon.Start);
        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        var g = (await _kal.HentGjoeremaalAsync(runde, null)).Single();

        // Manuell justering av det genererte gjøremålet setter ManueltEndret.
        await _kal.OppdaterGjoeremaalAsync(g.Id, new GjoeremaalInndata { Tittel = "Håndredigert" });

        // Endre regelen etterpå.
        await _regler.OppdaterAsync(regelId, new RegelInndata
        {
            Tittel = "Endret regel", Rundetyper = [Rundetype.Augustrunden],
            Tidfestingstype = Tidfestingstype.Rundeposisjon, Rundeposisjon = Rundeposisjon.Slutt
        });

        var forslag = await _kal.ForberedSynkAsync(runde);
        Assert.Empty(forslag);   // manuelt endret → ikke rørt

        // Selv om regelen slettes, foreslås ikke fjerning av det manuelt adopterte gjøremålet.
        await _regler.SlettAsync(regelId);
        Assert.Empty(await _kal.ForberedSynkAsync(runde));
        Assert.Equal("Håndredigert", (await _kal.HentGjoeremaalAsync(runde, null)).Single().Tittel);
    }

    [Fact]
    public async Task Sync_rorer_ikke_avhuket_gjoeremaal()
    {
        var regelId = await LeggRegel("Oppgave", Rundeposisjon.Start);
        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        var g = (await _kal.HentGjoeremaalAsync(runde, null)).Single();
        await _kal.FullfoerAsync(g.Id, "demo-sbr", "Sivert");

        await _regler.SlettAsync(regelId);
        Assert.Empty(await _kal.ForberedSynkAsync(runde));
    }

    [Fact]
    public async Task Ingen_endringer_gir_tomt_forslag()
    {
        await LeggRegel("Oppgave", Rundeposisjon.Start);
        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        Assert.Empty(await _kal.ForberedSynkAsync(runde));
    }

    public void Dispose() => _t.Dispose();
}
