using Microsoft.AspNetCore.Components.WebView.Maui;

namespace StageFright.App;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // Load BlazorWebView programmatically to avoid XAML startup crash (commit acdad7b).
        // ServiceProvider is resolved automatically by the MAUI host in .NET 10.
        var blazorWebView = new BlazorWebView
        {
            HostPage = "wwwroot/index.html",
            StartPath = "/dashboard"
        };

        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(StageFright.UI.App)
        });

        Content = blazorWebView;
    }
}
