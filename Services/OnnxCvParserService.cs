using CVGenerator.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.RegularExpressions;

namespace CVGenerator.Services;

/// <summary>
/// Service de parsing IA.
/// Stratégie hybride optimale (Option 1) recommandée pour mobile :
/// 1. 80% Regex : Extraction déterministe (Emails, Téléphone, Dates, URLs)
/// 2. 20% ONNX : Un mini-modèle BERT Quantized (15-30MB) pour la CLASSIFICATION DE PHRASES.
///    Au lieu de faire du NER complexe, le modèle prédit simplement la catégorie d'une ligne
///    (Ex: "Développeur chez Google" -> Catégorie: EXPÉRIENCE).
/// </summary>
public sealed class OnnxCvParserService : IOnnxCvParser, IDisposable
{
    private InferenceSession? _session;
    private bool _onnxAvailable;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // ──────────────────────────────────────────────────────────────
    // Initialisation paresseuse du modèle ONNX (optimisation mémoire)
    // ──────────────────────────────────────────────────────────────
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            try
            {
                // Cherche le modèle dans les assets embarqués
                using var stream = await FileSystem.OpenAppPackageFileAsync("cv_model.onnx");
                var modelBytes = new byte[stream.Length];
                await stream.ReadAsync(modelBytes);

                // SessionOptions optimisées pour mobile
                var options = new SessionOptions
                {
                    EnableMemoryPattern  = false,   // réduit la fragmentation mémoire
                    ExecutionMode        = ExecutionMode.ORT_SEQUENTIAL,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC
                };
                options.AddSessionConfigEntry("session.use_env_allocators", "1");

                _session = new InferenceSession(modelBytes, options);
                _onnxAvailable = true;
            }
            catch (Exception ex)
            {
                // Modèle absent → fallback silencieux
                System.Diagnostics.Debug.WriteLine($"[ONNX] Modèle non chargé, fallback règles: {ex.Message}");
                _onnxAvailable = false;
            }
            finally
            {
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Point d'entrée public
    // ──────────────────────────────────────────────────────────────
    public async Task<CvData> ParseAsync(string rawText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new CvData();

        await EnsureInitializedAsync();

        return _onnxAvailable
            ? await RunOnnxInferenceAsync(rawText, cancellationToken)
            : await Task.Run(() => RunRuleBasedParser(rawText), cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────
    // Inférence ONNX (Classification de phrases - Option optimale)
    // ──────────────────────────────────────────────────────────────
    private async Task<CvData> RunOnnxInferenceAsync(string text, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            // HYBRIDE : On commence par la base 80% Regex (Option 2 intégrée)
            var cv = RunRuleBasedParser(text);
            
            // Ensuite, on utilise le mini-modèle (20%) pour affiner les lignes non classées
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (cleanLine.Length < 10) continue;

                // Tokenisation simplifiée de la phrase entière
                var tokens = SimpleTokenize(cleanLine);
                var inputIds = new DenseTensor<long>(new[] { 1, tokens.Length });
                for (int i = 0; i < tokens.Length; i++)
                    inputIds[0, i] = tokens[i];

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIds)
                };

                using var results = _session!.Run(inputs);
                var logits = results.First().AsTensor<float>();

                // Logique de classification : Le modèle prédit 0=Autre, 1=Expérience, 2=Formation, 3=Compétence
                int predictedCategory = GetArgMax(logits);

                // Enrichir l'objet CV si la Regex l'avait manqué
                ApplyClassification(cv, cleanLine, predictedCategory);
            }

            return cv;
        }, ct);
    }
    
    private static int GetArgMax(Tensor<float> logits)
    {
        // Simulation pour l'exemple (en réalité, on cherche l'index de la valeur max)
        return 0; 
    }
    
    private static void ApplyClassification(CvData cv, string line, int categoryId)
    {
        // 1 = Expérience, 2 = Formation, 3 = Compétence
        if (categoryId == 1 && !cv.Experiences.Any(e => e.JobTitle.Contains(line)))
        {
            cv.Experiences.Add(new Experience { JobTitle = line });
        }
        else if (categoryId == 3 && !cv.Competences.Any(c => c.Name.Equals(line)))
        {
            cv.Competences.Add(new Competence { Name = line, Category = "Technique", Level = 3 });
        }
    }

    /// <summary>
    /// Tokenisation naïve : transforme chaque mot en un hash stable modulo vocabulaire.
    /// Dans une vraie impl, on utilise un tokenizer SentencePiece/BPE chargé depuis un fichier vocab.
    /// </summary>
    private static long[] SimpleTokenize(string text)
    {
        const int VOCAB_SIZE = 32000;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ids = new long[Math.Min(words.Length, 512)]; // max 512 tokens
        for (int i = 0; i < ids.Length; i++)
            ids[i] = Math.Abs(words[i].GetHashCode()) % VOCAB_SIZE;
        return ids;
    }

    // ──────────────────────────────────────────────────────────────
    // Parseur heuristique (Fallback complet — fonctionne sans modèle)
    // ──────────────────────────────────────────────────────────────
    private static CvData RunRuleBasedParser(string text)
    {
        var cv = new CvData
        {
            Contact     = ExtractContact(text),
            Experiences = ExtractExperiences(text),
            Formations  = ExtractFormations(text),
            Competences = ExtractCompetences(text)
        };
        return cv;
    }

    // ── Contact ───────────────────────────────────────────────────
    private static ContactInfo ExtractContact(string text)
    {
        var contact = new ContactInfo();

        // Email
        var emailMatch = Regex.Match(text, @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}");
        if (emailMatch.Success) contact.Email = emailMatch.Value;

        // Téléphone (formats FR/international)
        var phoneMatch = Regex.Match(text,
            @"(\+33|0033|0)[\s.\-]?[1-9]([\s.\-]?[0-9]{2}){4}|(\+[1-9][0-9]{1,2}[\s.\-]?[0-9\s.\-]{6,})");
        if (phoneMatch.Success) contact.Phone = phoneMatch.Value.Trim();

        // LinkedIn
        var linkedInMatch = Regex.Match(text, @"linkedin\.com/in/[\w\-]+", RegexOptions.IgnoreCase);
        if (linkedInMatch.Success) contact.LinkedIn = linkedInMatch.Value;

        // Nom complet : 1ère ligne non vide, ou ligne avant email
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var clean = line.Trim();
            if (clean.Length is > 3 and < 60 &&
                !clean.Contains('@') &&
                !Regex.IsMatch(clean, @"\d{2}[\s/\-]\d{2}") &&
                Regex.IsMatch(clean, @"^[A-ZÀÂÉÈÊËÎÏÔÙÛÜ][a-zA-ZÀ-ÿ\s\-']+$"))
            {
                contact.FullName = clean;
                break;
            }
        }

        // Titre / poste : ligne contenant des mots-clés de métier
        var jobKeywords = new[] { "développeur", "ingénieur", "chef", "manager", "consultant",
                                   "analyste", "designer", "architecte", "technicien", "responsable" };
        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (jobKeywords.Any(k => lower.Contains(k)) && line.Length < 80)
            {
                contact.JobTitle = line.Trim();
                break;
            }
        }

        return contact;
    }

    // ── Expériences ───────────────────────────────────────────────
    private static List<Experience> ExtractExperiences(string text)
    {
        var result = new List<Experience>();

        // Détecter la section "expériences"
        var sectionMatch = Regex.Match(text,
            @"(exp[ée]riences?\s*(professionnelles?)?|parcours professionnel)(.*?)(formations?|comp[ée]tences?|langues?|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var sectionText = sectionMatch.Success ? sectionMatch.Groups[3].Value : text;

        // Extraire les blocs : ligne avec date + titre poste
        var blocks = Regex.Matches(sectionText,
            @"(?<date>(jan|fév|mar|avr|mai|juin|juil|aoû|sep|oct|nov|déc|[0-9]{4})[\w\s/\-–]{0,20}[0-9]{4})[^\n]*\n(?<title>[^\n]{5,80})\n(?<company>[^\n]{3,60})?",
            RegexOptions.IgnoreCase);

        foreach (Match m in blocks)
        {
            var dateStr = m.Groups["date"].Value.Trim();
            var parts   = Regex.Split(dateStr, @"[–\-—/]");

            result.Add(new Experience
            {
                StartDate = parts.Length > 0 ? parts[0].Trim() : string.Empty,
                EndDate   = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                JobTitle  = m.Groups["title"].Value.Trim(),
                Company   = m.Groups["company"].Value.Trim()
            });
        }

        // Si aucun résultat structuré, chercher des lignes avec des années
        if (result.Count == 0)
        {
            var yearLines = Regex.Matches(sectionText, @"^.{3,60}\s+(20\d{2}|19\d{2}).*$",
                RegexOptions.Multiline);
            foreach (Match m in yearLines)
            {
                result.Add(new Experience
                {
                    JobTitle = m.Value.Trim(),
                    Tasks    = new List<string>()
                });
            }
        }

        return result;
    }

    // ── Formations ────────────────────────────────────────────────
    private static List<Formation> ExtractFormations(string text)
    {
        var result = new List<Formation>();

        var sectionMatch = Regex.Match(text,
            @"(formations?|[ée]tudes?|dipl[ôo]mes?|parcours acad[ée]mique)(.*?)(exp[ée]riences?|comp[ée]tences?|langues?|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var sectionText = sectionMatch.Success ? sectionMatch.Groups[2].Value : text;

        // Chercher "Degré – Institution – Année"
        var lines = sectionText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length < 5) continue;

            var yearMatch = Regex.Match(line, @"(20\d{2}|19\d{2})");
            var hasDegree = Regex.IsMatch(line,
                @"(master|licence|bac|bts|dut|doctorat|ingénieur|mba|bachelor|cap\b)",
                RegexOptions.IgnoreCase);

            if (hasDegree || yearMatch.Success)
            {
                result.Add(new Formation
                {
                    Degree      = line,
                    Institution = (i + 1 < lines.Length) ? lines[i + 1].Trim() : string.Empty,
                    Year        = yearMatch.Success ? yearMatch.Value : string.Empty
                });
            }
        }

        return result;
    }

    // ── Compétences ───────────────────────────────────────────────
    private static List<Competence> ExtractCompetences(string text)
    {
        var result = new List<Competence>();

        var sectionMatch = Regex.Match(text,
            @"(comp[ée]tences?|skills?|technologies?|outils?)(.*?)(formations?|exp[ée]riences?|langues?|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var sectionText = sectionMatch.Success ? sectionMatch.Groups[2].Value : text;

        // Technologies connues à détecter par scan global
        var techKeywords = new Dictionary<string, string>
        {
            { "C#", "Technique" }, { ".NET", "Technique" }, { "MAUI", "Technique" },
            { "Python", "Technique" }, { "Java", "Technique" }, { "JavaScript", "Technique" },
            { "TypeScript", "Technique" }, { "React", "Technique" }, { "Angular", "Technique" },
            { "Vue", "Technique" }, { "SQL", "Technique" }, { "MySQL", "Technique" },
            { "PostgreSQL", "Technique" }, { "MongoDB", "Technique" }, { "Docker", "Technique" },
            { "Kubernetes", "Technique" }, { "Azure", "Technique" }, { "AWS", "Technique" },
            { "Git", "Technique" }, { "Linux", "Technique" },
            { "Français", "Langue" }, { "Anglais", "Langue" }, { "Espagnol", "Langue" },
            { "Arabe", "Langue" }, { "Allemand", "Langue" },
            { "Gestion de projet", "Soft skill" }, { "Travail en équipe", "Soft skill" },
            { "Communication", "Soft skill" }, { "Leadership", "Soft skill" }
        };

        foreach (var kv in techKeywords)
        {
            if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new Competence
                {
                    Name     = kv.Key,
                    Category = kv.Value,
                    Level    = EstimateLevel(text, kv.Key)
                });
            }
        }

        // Compléter avec les mots en liste à puces de la section
        var bulletItems = Regex.Matches(sectionText, @"^[\s•\-\*]+(.{3,40})$", RegexOptions.Multiline);
        foreach (Match m in bulletItems)
        {
            var name = m.Groups[1].Value.Trim();
            if (!result.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new Competence { Name = name, Category = "Technique", Level = 3 });
            }
        }

        return result;
    }

    /// <summary>Estime le niveau d'une compétence selon la fréquence et les mots associés.</summary>
    private static int EstimateLevel(string text, string skill)
    {
        var lowerText  = text.ToLowerInvariant();
        var lowerSkill = skill.ToLowerInvariant();
        int count      = Regex.Matches(lowerText, Regex.Escape(lowerSkill)).Count;

        bool hasExpert = Regex.IsMatch(lowerText,
            $@"(expert|avancé|senior|maîtrise).{{0,20}}{Regex.Escape(lowerSkill)}", RegexOptions.IgnoreCase);
        bool hasBasic = Regex.IsMatch(lowerText,
            $@"(notions?|débutant|junior).{{0,20}}{Regex.Escape(lowerSkill)}", RegexOptions.IgnoreCase);

        if (hasExpert) return 5;
        if (hasBasic)  return 2;
        return count > 2 ? 4 : 3;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }
}
