using CVGenerator.Models;

namespace CVGenerator.Services;

/// <summary>
/// Contrat du service de génération PDF.
/// </summary>
public interface IPdfGeneratorService
{
    /// <summary>
    /// Génère un fichier PDF à partir des données structurées du CV.
    /// Retourne le chemin absolu du fichier créé.
    /// </summary>
    Task<string> GenerateAsync(CvData cvData, CancellationToken cancellationToken = default);
}
