using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using StageFright.UI;

namespace StageFright.Maui;

public partial class BlazorWebViewHost : ContentView
{
    public BlazorWebViewHost()
    {
        InitializeComponent();

        var blazorWebView = new BlazorWebView();
        blazorWebView.HostPage = "wwwroot/index.html";

        // Register RootComponents for Blazor
        var rootComponents = new RootComponentMapping()
        {
            Selector = "#app",
            ComponentType = typeof(App)
        };

        blazorWebView.RootComponents.Add(rootComponents);

        // Setup services
        var services = Application.Current?.Handler?.MauiContext?.Services ??
                      throw new InvalidOperationException("Cannot resolve services");

        blazorWebView.Services = services;

        Content = blazorWebView;
    }
}
