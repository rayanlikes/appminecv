using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CVGenerator.Services;

namespace CVGenerator.ViewModels;

/// <summary>
/// ViewModel de l'écran principal.
/// Hérite de ObservableObject (CommunityToolkit.Mvvm) pour les notifications de propriétés.
/// Toute la logique métier est ici, la vue ne fait que se lier (binding).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IOnnxCvParser       _parser;
    private readonly IPdfGeneratorService _pdfGenerator;

    // ── Propriétés liées à l'UI ───────────────────────────────────

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatus = false;

    [ObservableProperty]
    private bool _isSuccess = false;     // true=vert, false=rouge

    [ObservableProperty]
    private string _lastPdfPath = string.Empty;

    [ObservableProperty]
    private bool _canShare = false;

    // ── Constructeur (injection de dépendances) ───────────────────
    public MainViewModel(IOnnxCvParser parser, IPdfGeneratorService pdfGenerator)
    {
        _parser       = parser;
        _pdfGenerator = pdfGenerator;
    }

    // ── Commande principale ───────────────────────────────────────

    /// <summary>
    /// Déclenché par le bouton "Créer mon CV".
    /// Pipeline : validation → parsing IA → génération PDF → partage.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateCv))]
    private async Task CreateCvAsync()
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(RawText))
        {
            SetStatus("⚠️ Veuillez coller vos informations dans la zone de texte.", success: false);
            return;
        }

        IsLoading = true;
        HasStatus = false;
        CanShare  = false;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        try
        {
            // 2. Parsing IA (ONNX ou fallback règles)
            SetStatusLoading("🤖 Analyse IA en cours…");
            var cvData = await _parser.ParseAsync(RawText, cts.Token);

            if (!cvData.HasData)
            {
                SetStatus("❌ Aucune donnée extraite. Vérifiez le format du texte.", success: false);
                return;
            }

            // 3. Génération PDF
            SetStatusLoading("📄 Génération du PDF…");
            LastPdfPath = await _pdfGenerator.GenerateAsync(cvData, cts.Token);

            // 4. Succès
            CanShare = true;
            SetStatus($"✅ CV créé avec succès !\n📁 {Path.GetFileName(LastPdfPath)}", success: true);
        }
        catch (OperationCanceledException)
        {
            SetStatus("⏱️ Délai dépassé. Réessayez avec un texte plus court.", success: false);
        }
        catch (Exception ex)
        {
            SetStatus($"❌ Erreur : {ex.Message}", success: false);
            System.Diagnostics.Debug.WriteLine($"[CreateCv] {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanCreateCv() => !IsLoading;

    // ── Commande de partage du PDF ────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanShare))]
    private async Task SharePdfAsync()
    {
        if (string.IsNullOrEmpty(LastPdfPath) || !File.Exists(LastPdfPath))
            return;

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Partager mon CV",
            File  = new ShareFile(LastPdfPath)
        });
    }

    // ── Helpers UI ────────────────────────────────────────────────

    private void SetStatus(string message, bool success)
    {
        StatusMessage  = message;
        IsSuccess      = success;
        HasStatus      = true;
    }

    private void SetStatusLoading(string message)
    {
        StatusMessage = message;
        HasStatus     = true;
    }

    partial void OnIsLoadingChanged(bool value)
    {
        CreateCvCommand.NotifyCanExecuteChanged();
    }
}
