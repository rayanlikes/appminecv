namespace CVGenerator.Models;

/// <summary>
/// Objet racine représentant toutes les données du CV.
/// Chaque propriété correspond à une section du document final.
/// </summary>
public class CvData
{
    public ContactInfo Contact { get; set; } = new();
    public List<Experience> Experiences { get; set; } = new();
    public List<Formation> Formations { get; set; } = new();
    public List<Competence> Competences { get; set; } = new();

    /// <summary>Retourne true si au moins une section contient des données.</summary>
    public bool HasData =>
        Contact.HasData ||
        Experiences.Count > 0 ||
        Formations.Count > 0 ||
        Competences.Count > 0;
}
