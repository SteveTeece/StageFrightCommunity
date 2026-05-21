using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using StageFright.UI;

namespace StageFright.Maui;

public partial class BlazorWebViewHost : ContentView
{
    public BlazorWebViewHost()
    {
        InitializeComponent();
        
        // Blazor components are configured in MauiProgram.cs
        // This host provides the container for the BlazorWebView
    }
}
