using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using StageFright.UI;

namespace StageFright.Maui;

/// <summary>
/// BlazorWebViewHost is the primary UI container.
/// All navigation, routing, and UI elements are controlled by Blazor.
/// MAUI is responsible only for platform-level concerns (window management, lifecycle).
/// </summary>
public partial class BlazorWebViewHost : ContentView
{
    public BlazorWebViewHost()
    {
        InitializeComponent();
        
        // Blazor components are configured in MauiProgram.cs
        // This host provides the container for the BlazorWebView
        // which renders the full application UI controlled by Blazor
    }
}
