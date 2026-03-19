namespace CVGenerator.Models;

/// <summary>
/// Représente une compétence technique ou linguistique.
/// </summary>
public class Competence
{
    public string Name     { get; set; } = string.Empty;  // ex: Python, Gestion de projet
    public string Category { get; set; } = string.Empty;  // ex: Technique, Soft skill, Langue
    public int    Level    { get; set; } = 3;              // 1–5, utilisé pour la barre de niveau PDF
}
