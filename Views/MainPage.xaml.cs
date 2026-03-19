using CVGenerator.ViewModels;

namespace CVGenerator.Views;

/// <summary>
/// Code-behind de la page principale.
/// Réduit au strict minimum grâce au pattern MVVM.
/// </summary>
public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>
    /// Ferme le clavier virtuel quand l'utilisateur appuie en dehors de l'Editor.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetNavBarIsVisible(this, false); // Plein écran, pas de barre nav
    }
}
