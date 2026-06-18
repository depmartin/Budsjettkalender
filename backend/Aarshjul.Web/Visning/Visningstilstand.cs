using Aarshjul.Application.Frister;
using Aarshjul.Domain;

namespace Aarshjul.Web.Visning;

/// <summary>
/// Felles filtertilstand delt mellom de tre visningene (kravdok. 7): kategori og budsjettår.
/// Registreres scoped, så valg følger brukeren mellom visningene i samme krets.
/// </summary>
public class Visningstilstand
{
    /// <summary>Standard: budsjett + gulbok på, regnskap av (kravdok. 3.2).</summary>
    public HashSet<Kategori> Kategorier { get; } = [Kategori.Budsjett, Kategori.Gulbok];

    /// <summary>Valgte budsjettår. Tomt = vis alle synlige år.</summary>
    public HashSet<int> Budsjettaar { get; } = [];

    public bool InkluderHistorikk { get; set; }

    public event Action? Endret;

    public void VekslKategori(Kategori kategori, bool på)
    {
        if (på)
        {
            Kategorier.Add(kategori);
        }
        else
        {
            Kategorier.Remove(kategori);
        }
        Endret?.Invoke();
    }

    public void VekslBudsjettaar(int aar, bool på)
    {
        if (på)
        {
            Budsjettaar.Add(aar);
        }
        else
        {
            Budsjettaar.Remove(aar);
        }
        Endret?.Invoke();
    }

    public void SettHistorikk(bool på)
    {
        InkluderHistorikk = på;
        Endret?.Invoke();
    }

    public FristFilter TilFilter() => new()
    {
        Kategorier = Kategorier.Count > 0 ? Kategorier.ToArray() : null,
        Budsjettaar = Budsjettaar.Count > 0 ? Budsjettaar.ToArray() : null,
        InkluderHistorikk = InkluderHistorikk
    };
}
