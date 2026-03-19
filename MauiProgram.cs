using CVGenerator.Services;
using CVGenerator.ViewModels;
using CVGenerator.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using QuestPDF.Infrastructure;

namespace CVGenerator;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // QuestPDF licence libre (Community)
        QuestPDF.Settings.License = LicenseType.Community;

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        // ── Injection de dépendances ──────────────────────────────────
        // Services IA
        builder.Services.AddSingleton<IOnnxCvParser, OnnxCvParserService>();

        // Services PDF
        builder.Services.AddSingleton<IPdfGeneratorService, QuestPdfGeneratorService>();

        // ViewModel
        builder.Services.AddTransient<MainViewModel>();

        // Page principale
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
