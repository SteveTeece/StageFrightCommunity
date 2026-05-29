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
#if DEBUG
        System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.ctor() called");
#endif
        try
        {
            InitializeComponent();
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.InitializeComponent() completed");
#endif
            
            // Blazor components are configured in MauiProgram.cs
            // This host provides the container for the BlazorWebView
            // which renders the full application UI controlled by Blazor
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] BlazorWebViewHost.ctor() EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Stack Trace: {ex.StackTrace}");
#endif
            throw;
        }
    }
}
