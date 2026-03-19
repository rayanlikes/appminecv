namespace CVGenerator.Models;

/// <summary>
/// Représente une expérience professionnelle.
/// </summary>
public class Experience
{
    public string JobTitle   { get; set; } = string.Empty;
    public string Company    { get; set; } = string.Empty;
    public string StartDate  { get; set; } = string.Empty;
    public string EndDate    { get; set; } = string.Empty;

    /// <summary>Période formatée, ex: "Jan 2020 – Déc 2022"</summary>
    public string Period => string.IsNullOrWhiteSpace(EndDate)
        ? $"{StartDate} – Présent"
        : $"{StartDate} – {EndDate}";

    public List<string> Tasks { get; set; } = new();
}
