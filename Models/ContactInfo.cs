namespace CVGenerator.Models;

/// <summary>
/// Informations de contact extraites du texte brut.
/// </summary>
public class ContactInfo
{
    public string FullName   { get; set; } = string.Empty;
    public string Email      { get; set; } = string.Empty;
    public string Phone      { get; set; } = string.Empty;
    public string Address    { get; set; } = string.Empty;
    public string LinkedIn   { get; set; } = string.Empty;
    public string JobTitle   { get; set; } = string.Empty;

    /// <summary>Vrai si au moins un champ est renseigné.</summary>
    public bool HasData =>
        !string.IsNullOrWhiteSpace(FullName)  ||
        !string.IsNullOrWhiteSpace(Email)     ||
        !string.IsNullOrWhiteSpace(Phone);
}
