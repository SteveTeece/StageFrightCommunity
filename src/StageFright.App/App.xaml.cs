namespace StageFright.App;

public partial class App : Application
{
    private const double StartupWidth = 800;
    private const double StartupHeight = 600;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        DisplayInfo displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        double density = displayInfo.Density;
        double screenWidth = displayInfo.Width / density;
        double screenHeight = displayInfo.Height / density;

        // Open at a fixed 800x600, centred on the display (issue #383). The
        // Math.Max guards a display smaller than the window from pushing it off-screen.
        Window window = new Window(new MainPage())
        {
            Title = "StageFright Community",
            Width = StartupWidth,
            Height = StartupHeight,
            X = Math.Max(0, (screenWidth - StartupWidth) / 2),
            Y = Math.Max(0, (screenHeight - StartupHeight) / 2)
        };

        return window;
    }
}
