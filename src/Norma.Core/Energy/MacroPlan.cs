namespace Norma.Core.Energy;

/// <summary>
/// Целевые БЖУ на сутки, в граммах. Округлены до целых: UI показывает их как есть.
/// </summary>
/// <param name="ProteinG">Белок, г.</param>
/// <param name="FatG">Жиры, г.</param>
/// <param name="CarbsG">Углеводы, г.</param>
public sealed record MacroPlan(int ProteinG, int FatG, int CarbsG);
