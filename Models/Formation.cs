namespace CVGenerator.Models;

/// <summary>
/// Représente un diplôme ou une formation.
/// </summary>
public class Formation
{
    public string Degree      { get; set; } = string.Empty;   // ex: Licence Informatique
    public string Institution { get; set; } = string.Empty;   // ex: Université Paris-Saclay
    public string Year        { get; set; } = string.Empty;   // ex: 2021
    public string Description { get; set; } = string.Empty;
}
