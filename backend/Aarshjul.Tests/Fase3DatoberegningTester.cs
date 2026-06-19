using Aarshjul.Domain;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Datoberegning per regeltype, virkedagjustering og valgårslogikk (Fase 3 Steg A/B).</summary>
public class Fase3DatoberegningTester
{
    [Fact]
    public void Virkedag_uendret_paa_hverdag()
    {
        // Onsdag 13. mai 2026.
        var onsdag = new DateOnly(2026, 5, 13);
        Assert.Equal(onsdag, Datoberegning.Virkedagjuster(onsdag));
    }

    [Fact]
    public void Loerdag_flyttes_bakover_til_fredag()
    {
        // Lørdag 8. jan. 2022 → fredag 7. jan.
        Assert.Equal(new DateOnly(2022, 1, 7), Datoberegning.Virkedagjuster(new DateOnly(2022, 1, 8)));
    }

    [Fact]
    public void Soendag_flyttes_framover_til_mandag()
    {
        // Søndag 9. jan. 2022 → mandag 10. jan.
        Assert.Equal(new DateOnly(2022, 1, 10), Datoberegning.Virkedagjuster(new DateOnly(2022, 1, 9)));
    }

    [Fact]
    public void Virkedag_krysser_aldri_aarsskifte_framover()
    {
        // Lørdag 1. jan. 2022: fredag ville vært 31. des. 2021 (over årsskiftet) → mandag 3. jan. i stedet.
        Assert.Equal(new DateOnly(2022, 1, 3), Datoberegning.Virkedagjuster(new DateOnly(2022, 1, 1)));
    }

    [Fact]
    public void Virkedag_krysser_aldri_aarsskifte_bakover()
    {
        // Søndag 31. des. 2023: mandag ville vært 1. jan. 2024 (over årsskiftet) → fredag 29. des. i stedet.
        Assert.Equal(new DateOnly(2023, 12, 29), Datoberegning.Virkedagjuster(new DateOnly(2023, 12, 31)));
    }

    [Fact]
    public void NteUkedag_gir_andre_mandag_i_mars()
    {
        // 1. mars 2026 er søndag → første mandag 2., andre mandag 9.
        Assert.Equal(new DateOnly(2026, 3, 9), Datoberegning.NteUkedag(2026, 3, 2, DayOfWeek.Monday));
    }

    [Fact]
    public void NteUkedag_klamper_til_siste_forekomst_naar_uken_ikke_finnes()
    {
        // Mars 2026 har ikke fem mandager: 2, 9, 16, 23, 30 — det er faktisk fem. Bruk februar 2026 (4 mandager).
        var resultat = Datoberegning.NteUkedag(2026, 2, 5, DayOfWeek.Monday);
        Assert.Equal(2, resultat.Month);
        Assert.Equal(DayOfWeek.Monday, resultat.DayOfWeek);
    }

    [Theory]
    [InlineData(2025, Valgtype.Stortingsvalg)]
    [InlineData(2029, Valgtype.Stortingsvalg)]
    [InlineData(2033, Valgtype.Stortingsvalg)]
    [InlineData(2027, Valgtype.Kommunevalg)]
    [InlineData(2031, Valgtype.Kommunevalg)]
    [InlineData(2026, Valgtype.Ingen)]
    [InlineData(2028, Valgtype.Ingen)]
    public void Valgtype_foelger_den_faste_syklusen(int aar, Valgtype forventet)
    {
        Assert.Equal(forventet, Valgaar.Type(aar));
    }
}
