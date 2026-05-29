using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using StageFright.UI;
using System.Runtime.InteropServices;

namespace StageFright.Maui;

/// <summary>
/// BlazorWebViewHost is the primary UI container.
/// All navigation, routing, and UI elements are controlled by Blazor.
/// MAUI is responsible only for platform-level concerns (window management, lifecycle).
/// </summary>
public partial class BlazorWebViewHost : ContentView
{
    private Grid? _blazorContainer;

    public BlazorWebViewHost()
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.ctor() called");
        CheckWebView2Availability();
#endif
        try
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.ctor() - calling InitializeComponent");
#endif
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
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] BlazorWebViewHost.ctor() EXCEPTION in InitializeComponent: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Stack Trace: {ex.StackTrace}");
#endif
            throw;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

#if DEBUG
        System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.OnApplyTemplate() called - loading BlazorWebView programmatically");
#endif

        try
        {
            _blazorContainer = (Grid)this.FindByName("blazorContainer");
            if (_blazorContainer == null)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] ERROR: blazorContainer Grid not found in XAML");
#endif
                return;
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] Creating BlazorWebView instance programmatically");
#endif

            var blazorWebView = new BlazorWebView
            {
                HostPage = "wwwroot/index.html"
            };

            // Add root component
            var rootComponent = new RootComponent
            {
                Selector = "#app",
                ComponentType = typeof(StageFright.UI.App)
            };
            blazorWebView.RootComponents.Add(rootComponent);

#if DEBUG
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebView instance created, adding to container");
#endif

            _blazorContainer.Children.Add(blazorWebView);

#if DEBUG
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebView added to container successfully");
#endif
        }
        catch (DllNotFoundException dllEx)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] DllNotFoundException when creating BlazorWebView: {dllEx.Message}");
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Missing DLL likely WebView2 runtime. Install from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/");
#endif
            ShowErrorMessage($"WebView2 Runtime Not Found:\n\n{dllEx.Message}\n\nPlease install WebView2 from:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/");
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Exception creating BlazorWebView: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Stack Trace: {ex.StackTrace}");
#endif
            ShowErrorMessage($"Error Loading BlazorWebView:\n\n{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ShowErrorMessage(string message)
    {
        var errorContainer = new VerticalStackLayout
        {
            Padding = new Thickness(20),
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Application Error",
                    FontSize = 24,
                    FontAttributes = Microsoft.Maui.Controls.FontAttributes.Bold
                },
                new Label
                {
                    Text = message,
                    FontSize = 14,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };

        if (_blazorContainer != null)
        {
            _blazorContainer.Children.Add(errorContainer);
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
#if DEBUG
        if (args.NewHandler != null)
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.OnHandlerChanging - about to call base.OnHandlerChanging()");
#endif
        try
        {
            base.OnHandlerChanging(args);
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] BlazorWebViewHost.OnHandlerChanging - base.OnHandlerChanging() completed successfully");
#endif
        }
        catch (DllNotFoundException dllEx)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] DllNotFoundException in OnHandlerChanging: {dllEx.Message}");
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Missing DLL likely WebView2 runtime. Install from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/");
#endif
            throw;
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] BlazorWebViewHost.OnHandlerChanging EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Stack Trace: {ex.StackTrace}");
#endif
            throw;
        }
    }

#if DEBUG
    private void CheckWebView2Availability()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] Checking WebView2 availability...");
            
            // Check if WebView2 runtime is available
            var regPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
            try
            {
                var version = Microsoft.Win32.Registry.GetValue(regPath, "pv", null);
                if (version != null)
                    System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] WebView2 runtime found: {version}");
                else
                    System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] WebView2 registry entry found but no version - may need reinstall");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] WebView2 runtime NOT found in registry - installation required");
            }

            // Try to load WebView2 loader DLL
            try
            {
                var loaderDll = "WebView2Loader.dll";
                var handle = LoadLibrary(loaderDll);
                if (handle != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] WebView2Loader.dll loaded successfully");
                    FreeLibrary(handle);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[STARTUP-XAML] WebView2Loader.dll not found in system PATH");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Error loading WebView2Loader: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STARTUP-XAML] Error checking WebView2 availability: {ex.Message}");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibrary(string dllToLoad);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr handle);
#endif
}
