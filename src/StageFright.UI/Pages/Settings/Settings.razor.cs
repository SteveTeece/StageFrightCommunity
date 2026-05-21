using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Settings;

public partial class Settings
{
    [Inject]
    public ISettingsRepository SettingsRepository { get; set; } = default!;

    private string ActiveTab = "general";
}
