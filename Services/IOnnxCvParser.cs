using CVGenerator.Models;

namespace CVGenerator.Services;

/// <summary>
/// Contrat du service de parsing IA.
/// Reçoit le texte brut et retourne un objet CvData structuré.
/// </summary>
public interface IOnnxCvParser
{
    /// <summary>
    /// Analyse le texte brut et extrait les sections du CV.
    /// Opération asynchrone pour ne pas bloquer l'UI.
    /// </summary>
    Task<CvData> ParseAsync(string rawText, CancellationToken cancellationToken = default);
}
