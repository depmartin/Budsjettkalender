using Aarshjul.Domain;

namespace Aarshjul.Application.Synlighet;

/// <summary>
/// Den sikkerhetskritiske mekanismen (SYSTEMARKITEKTUR 4). All synlighetsfiltrering går
/// gjennom dette ene punktet, slik at både visningene og API-et filtrerer likt og kan
/// verifiseres på selve spørringsresultatet — ikke bare på det grensesnittet viser.
/// </summary>
public static class Synlighetsfilter
{
    /// <summary>
    /// Begrenser en frist-spørring til det konteksten har rett til å se: administrator ser alt,
    /// ellers returneres kun frister der minst én av brukerens grupper er i fristens synlig_for.
    /// </summary>
    public static IQueryable<Frist> FiltrerSynlige(this IQueryable<Frist> kilde, ISynlighetskontekst ctx)
    {
        if (ctx.SerAlt)
        {
            return kilde;
        }

        // Materialiser til en liste for stabil oversettelse til SQL IN (...).
        var grupper = ctx.Grupper.ToList();
        if (grupper.Count == 0)
        {
            return kilde.Where(_ => false);
        }

        return kilde.Where(f => f.Synlighet.Any(s => grupper.Contains(s.GruppeKode)));
    }
}
