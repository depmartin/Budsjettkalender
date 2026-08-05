using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure;

/// <summary>
/// EF Core-konteksten for hele løsningen. Datamodellen settes opp komplett allerede i Fase 1
/// (også tabeller som først tas i bruk i Fase 2/3), slik at senere arbeid ikke krever
/// skjemaendringer.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Frist> Frister => Set<Frist>();
    public DbSet<FristSynlighet> FristSynlighet => Set<FristSynlighet>();
    public DbSet<Synlighetsgruppe> Synlighetsgrupper => Set<Synlighetsgruppe>();
    public DbSet<Bruker> Brukere => Set<Bruker>();
    public DbSet<BrukerGruppe> BrukerGrupper => Set<BrukerGruppe>();
    public DbSet<Forslag> Forslag => Set<Forslag>();
    public DbSet<UttrekksBevis> UttrekksBevis => Set<UttrekksBevis>();
    public DbSet<BehandletDokument> BehandledeDokumenter => Set<BehandletDokument>();
    public DbSet<Gjentaksregel> Gjentaksregler => Set<Gjentaksregel>();
    public DbSet<Varsel> Varsler => Set<Varsel>();
    public DbSet<InnhentingsStatus> InnhentingsStatuser => Set<InnhentingsStatus>();
    public DbSet<Kalenderabonnement> Kalenderabonnementer => Set<Kalenderabonnement>();
    public DbSet<Sidetekst> Sidetekster => Set<Sidetekst>();

    // --- Internkalender for SBR (SBR-intern arbeidsliste) ---
    public DbSet<InternRunde> InternRunder => Set<InternRunde>();
    public DbSet<InterntGjoeremaal> InterneGjoeremaal => Set<InterntGjoeremaal>();
    public DbSet<GjoeremaalAnsvarlig> GjoeremaalAnsvarlige => Set<GjoeremaalAnsvarlig>();
    public DbSet<GjoeremaalRegel> GjoeremaalRegler => Set<GjoeremaalRegel>();
    public DbSet<RegelRundetype> RegelRundetyper => Set<RegelRundetype>();
    public DbSet<RegelAnsvarlig> RegelAnsvarlige => Set<RegelAnsvarlig>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // --- Synlighetsgruppe: Kode er stabil, unik nøkkel det refereres til fra synlig_for ---
        b.Entity<Synlighetsgruppe>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kode).HasMaxLength(64);
            e.Property(x => x.Navn).HasMaxLength(256);
            e.HasIndex(x => x.Kode).IsUnique();
            e.HasAlternateKey(x => x.Kode);
        });

        // --- Frist ---
        b.Entity<Frist>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tittel).HasMaxLength(512);
            e.Property(x => x.Loep).HasMaxLength(128);
            e.Property(x => x.Kilde).HasMaxLength(128);
            e.HasIndex(x => x.Budsjettaar);
            e.HasIndex(x => x.Sorteringsdag);
        });

        // --- Kalenderabonnement: delbar feed-lenke per gruppe (Endring 1) ---
        b.Entity<Kalenderabonnement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).HasMaxLength(64);
            e.Property(x => x.GruppeKode).HasMaxLength(64);
            e.Property(x => x.Etikett).HasMaxLength(256);
            e.Property(x => x.OpprettetAv).HasMaxLength(256);
            e.HasIndex(x => x.Token).IsUnique();
        });

        // --- FristSynlighet: koblingstabell frist ↔ gruppekode (indeksert join) ---
        b.Entity<FristSynlighet>(e =>
        {
            e.HasKey(x => new { x.FristId, x.GruppeKode });
            e.Property(x => x.GruppeKode).HasMaxLength(64);
            e.HasIndex(x => x.GruppeKode);
            e.HasOne(x => x.Frist).WithMany(f => f.Synlighet)
                .HasForeignKey(x => x.FristId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Gruppe).WithMany()
                .HasForeignKey(x => x.GruppeKode)
                .HasPrincipalKey(g => g.Kode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Bruker ---
        b.Entity<Bruker>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(128);
            e.Property(x => x.Navn).HasMaxLength(256);
        });

        // --- BrukerGruppe: koblingstabell med kilde (Entra vs. manuell) ---
        b.Entity<BrukerGruppe>(e =>
        {
            e.HasKey(x => new { x.BrukerId, x.GruppeKode });
            e.Property(x => x.GruppeKode).HasMaxLength(64);
            e.HasOne(x => x.Bruker).WithMany(u => u.Grupper)
                .HasForeignKey(x => x.BrukerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Gruppe).WithMany()
                .HasForeignKey(x => x.GruppeKode)
                .HasPrincipalKey(g => g.Kode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Forslag + uttrekksbevis ---
        b.Entity<Forslag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tittel).HasMaxLength(512);
            e.HasIndex(x => x.EndrerFristId);
            e.HasMany(x => x.UttrekksBevis).WithOne(u => u.Forslag)
                .HasForeignKey(u => u.ForslagId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UttrekksBevis>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Felt).HasMaxLength(64);
        });

        // --- Behandlet dokument: dedup-register ---
        b.Entity<BehandletDokument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kilde).HasMaxLength(64);
            e.Property(x => x.DokumentNokkel).HasMaxLength(512);
            e.HasIndex(x => new { x.Kilde, x.DokumentNokkel }).IsUnique();
        });

        // --- Gjentaksregel ---
        b.Entity<Gjentaksregel>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Loep).HasMaxLength(128);
            e.Property(x => x.Tittel).HasMaxLength(512);
        });

        // --- Varsel ---
        b.Entity<Varsel>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BrukerId).HasMaxLength(128);
            e.HasIndex(x => x.BrukerId);
        });

        // --- InnhentingsStatus: liveness-spor, én rad per kilde ---
        b.Entity<InnhentingsStatus>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kilde).HasMaxLength(64);
            e.HasIndex(x => x.Kilde).IsUnique();
        });

        // --- Redigerbar flate-tekst (nøkkel → tekst) ---
        b.Entity<Sidetekst>(e =>
        {
            e.HasKey(x => x.Nokkel);
            e.Property(x => x.Nokkel).HasMaxLength(128);
        });

        // --- Internkalender: konkret runde ---
        b.Entity<InternRunde>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OpprettetAv).HasMaxLength(256);
            e.HasIndex(x => new { x.Rundetype, x.Aar });
        });

        // --- Internkalender: internt gjøremål ---
        b.Entity<InterntGjoeremaal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tittel).HasMaxLength(512);
            e.Property(x => x.AnkerLoep).HasMaxLength(128);
            e.Property(x => x.FullfoertAvId).HasMaxLength(128);
            e.Property(x => x.FullfoertAvNavn).HasMaxLength(256);
            e.HasIndex(x => x.RundeId);
            e.HasIndex(x => x.Sorteringsdag);
            e.HasOne(x => x.Runde).WithMany(r => r.Gjoeremaal)
                .HasForeignKey(x => x.RundeId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Ansvarlige).WithOne(a => a.Gjoeremaal)
                .HasForeignKey(a => a.GjoeremaalId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GjoeremaalAnsvarlig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BrukerId).HasMaxLength(128);
            e.Property(x => x.Navn).HasMaxLength(256);
            e.HasIndex(x => x.BrukerId);
        });

        // --- Internkalender: generell regel (mal) ---
        b.Entity<GjoeremaalRegel>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tittel).HasMaxLength(512);
            e.Property(x => x.AnkerLoep).HasMaxLength(128);
            e.HasMany(x => x.Rundetyper).WithOne(r => r.Regel)
                .HasForeignKey(r => r.RegelId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Ansvarlige).WithOne(a => a.Regel)
                .HasForeignKey(a => a.RegelId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RegelRundetype>(e =>
        {
            e.HasKey(x => new { x.RegelId, x.Rundetype });
        });

        b.Entity<RegelAnsvarlig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BrukerId).HasMaxLength(128);
            e.Property(x => x.Navn).HasMaxLength(256);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        OppdaterSorteringsdager();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        OppdaterSorteringsdager();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Sørger for at hver frist har et entydig sorteringspunkt før lagring.</summary>
    private void OppdaterSorteringsdager()
    {
        foreach (var entry in ChangeTracker.Entries<Frist>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.OppdaterSorteringsdag();
            }
        }
    }
}
