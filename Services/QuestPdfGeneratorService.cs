using CVGenerator.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CVGenerator.Services;

/// <summary>
/// Génère un CV professionnel au format PDF en utilisant QuestPDF.
/// Design deux colonnes : sidebar violet + contenu blanc.
/// </summary>
public sealed class QuestPdfGeneratorService : IPdfGeneratorService
{
    private static readonly string ColorSidebar  = "#7C3AED";    // Violet accent
    private static readonly string ColorHeader   = "#1A1A2E";    // Fond sombre
    private static readonly string ColorBody     = "#1E293B";
    private static readonly string ColorMuted    = "#64748B";
    private static readonly string ColorAccent   = "#A855F7";
    private static readonly string ColorBar      = "#E2E8F0";    // Barre niveau (fond)

    public async Task<string> GenerateAsync(CvData cv, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            // Chemin de sortie dans AppDataDirectory (pas de permission requise)
            var dir      = FileSystem.AppDataDirectory;
            var fileName = $"CV_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(dir, fileName);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Content().Row(row =>
                    {
                        // ── Colonne gauche (Sidebar) ──────────────
                        row.ConstantItem(180).Background(ColorSidebar).Padding(20).Column(sidebar =>
                        {
                            // Initiales / Avatar
                            BuildAvatar(sidebar, cv.Contact);

                            sidebar.Item().Height(20);

                            // Coordonnées
                            if (cv.Contact.HasData)
                                BuildContactSection(sidebar, cv.Contact);

                            sidebar.Item().Height(16);

                            // Compétences
                            if (cv.Competences.Count > 0)
                                BuildCompetencesSection(sidebar, cv.Competences);
                        });

                        // ── Colonne droite (Contenu principal) ────
                        row.RelativeItem().Background(QuestPDF.Helpers.Colors.White).Column(main =>
                        {
                            // En-tête nom + titre
                            BuildMainHeader(main, cv.Contact);

                            // Expériences
                            if (cv.Experiences.Count > 0)
                                BuildExperiencesSection(main, cv.Experiences);

                            // Formations
                            if (cv.Formations.Count > 0)
                                BuildFormationsSection(main, cv.Formations);
                        });
                    });
                });
            });

            document.GeneratePdf(filePath);
            return filePath;

        }, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers de construction PDF
    // ─────────────────────────────────────────────────────────────

    private static void BuildAvatar(ColumnDescriptor col, ContactInfo contact)
    {
        // Cercle avec initiales
        var initials = GetInitials(contact.FullName);
        col.Item().AlignCenter().Width(80).Height(80)
            .Background("#FFFFFF22")
            .Border(2).BorderColor("#FFFFFF55")
            .AlignMiddle().AlignCenter()
            .Text(initials)
                .FontSize(28).Bold().FontColor(QuestPDF.Helpers.Colors.White);
    }

    private static void BuildContactSection(ColumnDescriptor col, ContactInfo contact)
    {
        col.Item().Text("CONTACT").FontSize(9).Bold().FontColor("#FFFFFFAA").LetterSpacing(2);
        col.Item().Height(8);

        if (!string.IsNullOrEmpty(contact.Email))
            BuildContactRow(col, "✉", contact.Email);

        if (!string.IsNullOrEmpty(contact.Phone))
            BuildContactRow(col, "✆", contact.Phone);

        if (!string.IsNullOrEmpty(contact.Address))
            BuildContactRow(col, "⌂", contact.Address);

        if (!string.IsNullOrEmpty(contact.LinkedIn))
            BuildContactRow(col, "in", contact.LinkedIn);
    }

    private static void BuildContactRow(ColumnDescriptor col, string icon, string value)
    {
        col.Item().PaddingBottom(6).Row(r =>
        {
            r.ConstantItem(18).Text(icon).FontSize(11).FontColor("#FFFFFFCC");
            r.RelativeItem().Text(value).FontSize(9).FontColor(QuestPDF.Helpers.Colors.White).Italic();
        });
    }

    private static void BuildCompetencesSection(ColumnDescriptor col, List<Competence> competences)
    {
        col.Item().Text("COMPÉTENCES").FontSize(9).Bold().FontColor("#FFFFFFAA").LetterSpacing(2);
        col.Item().Height(8);

        var groups = competences
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            // Sous-titre catégorie
            col.Item().PaddingBottom(4).Text(group.Key.ToUpperInvariant())
                .FontSize(8).Bold().FontColor("#FFFFFFAA");

            foreach (var skill in group.Take(8)) // max 8 par catégorie
            {
                col.Item().PaddingBottom(5).Column(sc =>
                {
                    sc.Item().Text(skill.Name).FontSize(9).FontColor(QuestPDF.Helpers.Colors.White);
                    // Barre de niveau (5 segments)
                    sc.Item().Height(4).Row(br =>
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            br.RelativeItem()
                              .Height(4)
                              .Background(i <= skill.Level ? "#FFFFFFEE" : "#FFFFFF33")
                              .Border(0.5f).BorderColor(ColorSidebar);
                        }
                    });
                });
            }
            col.Item().Height(8);
        }
    }

    private static void BuildMainHeader(ColumnDescriptor col, ContactInfo contact)
    {
        col.Item().Background(ColorHeader).Padding(24).Column(h =>
        {
            h.Item().Text(string.IsNullOrEmpty(contact.FullName) ? "Votre Nom" : contact.FullName)
                .FontSize(26).Bold().FontColor(QuestPDF.Helpers.Colors.White);

            if (!string.IsNullOrEmpty(contact.JobTitle))
            {
                h.Item().Height(4);
                h.Item().Text(contact.JobTitle)
                    .FontSize(13).FontColor(ColorAccent);
            }
        });
    }

    private static void BuildExperiencesSection(ColumnDescriptor col, List<Experience> experiences)
    {
        col.Item().Padding(20).Column(s =>
        {
            BuildSectionTitle(s, "EXPÉRIENCES PROFESSIONNELLES");

            foreach (var exp in experiences)
            {
                s.Item().PaddingBottom(14).Column(e =>
                {
                    // Ligne : Titre poste | Période
                    e.Item().Row(r =>
                    {
                        r.RelativeItem().Text(exp.JobTitle).Bold().FontSize(11).FontColor(ColorBody);
                        r.ConstantItem(90).AlignRight()
                          .Text(exp.Period).FontSize(9).FontColor(ColorMuted);
                    });

                    if (!string.IsNullOrEmpty(exp.Company))
                    {
                        e.Item().Text(exp.Company)
                            .FontSize(10).FontColor(ColorAccent).Italic();
                    }

                    // Tâches / bullet points
                    foreach (var task in exp.Tasks)
                    {
                        e.Item().PaddingLeft(10).PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(10).Text("•").FontColor(ColorAccent);
                            r.RelativeItem().Text(task).FontSize(9).FontColor(ColorBody);
                        });
                    }
                });

                s.Item().LineHorizontal(0.5f).LineColor(ColorBar);
                s.Item().Height(8);
            }
        });
    }

    private static void BuildFormationsSection(ColumnDescriptor col, List<Formation> formations)
    {
        col.Item().PaddingHorizontal(20).PaddingBottom(20).Column(s =>
        {
            BuildSectionTitle(s, "FORMATIONS");

            foreach (var f in formations)
            {
                s.Item().PaddingBottom(10).Column(fe =>
                {
                    fe.Item().Row(r =>
                    {
                        r.RelativeItem().Text(f.Degree).Bold().FontSize(10).FontColor(ColorBody);
                        r.ConstantItem(40).AlignRight()
                          .Text(f.Year).FontSize(9).FontColor(ColorMuted);
                    });

                    if (!string.IsNullOrEmpty(f.Institution))
                    {
                        fe.Item().Text(f.Institution)
                            .FontSize(9).FontColor(ColorAccent).Italic();
                    }

                    if (!string.IsNullOrEmpty(f.Description))
                    {
                        fe.Item().Text(f.Description)
                            .FontSize(9).FontColor(ColorMuted);
                    }
                });
            }
        });
    }

    private static void BuildSectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().PaddingBottom(10).Column(t =>
        {
            t.Item().Text(title)
                .FontSize(11).Bold().FontColor(ColorSidebar).LetterSpacing(1);
            t.Item().Height(2).Background(ColorSidebar);
        });
    }

    private static string GetInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "CV";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : fullName[..Math.Min(2, fullName.Length)].ToUpperInvariant();
    }
}
