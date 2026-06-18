namespace Aarshjul.Application;

/// <summary>Kastes når inndata bryter en forretningsregel (f.eks. frist uten valgt synlighet).</summary>
public class Valideringsfeil(string melding) : Exception(melding);
