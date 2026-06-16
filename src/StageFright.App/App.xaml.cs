namespace StageFright.App;

public partial class App : Application
{
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

        Window window = new Window(new MainPage())
        {
            Title = "StageFright Community",
            Width = screenWidth,
            Height = screenHeight,
            X = 0,
            Y = 0
        };

        return window;
    }
}
