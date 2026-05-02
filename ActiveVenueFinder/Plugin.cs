using ActiveVenueFinder.Services;
using ActiveVenueFinder.Services.Api;
using ActiveVenueFinder.Services.Lifestream;
using ActiveVenueFinder.Services.Tags;
using ActiveVenueFinder.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ActiveVenueFinder;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    private const string CommandName = "/avf";

    public Config Configuration { get; }

    private readonly WindowSystem windowSystem = new("ActiveVenueFinder");
    private readonly VenueApiClient apiClient;
    private readonly VenueRepository repository;
    private readonly VenueTagService tagService;
    private readonly LifestreamAvailabilityService lifestreamAvailability;
    private readonly VenueTravelService travelService;
    private readonly VenueFinderWindow mainWindow;
    private readonly AddEditVenueWindow addEditWindow;
    private readonly PopoutWindow popoutWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly TimezonePickerWindow timezonePicker;
    private readonly VenueInfoWindow venueInfoWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();
        ConfigMigrator.Run(Configuration);

        apiClient = new VenueApiClient();
        repository = new VenueRepository(apiClient, Configuration, PluginInterface);
        tagService = new VenueTagService(Configuration, () => repository.NotifyConfigChanged());
        lifestreamAvailability = new LifestreamAvailabilityService(PluginInterface);
        travelService = new VenueTravelService(lifestreamAvailability);

        timezonePicker = new TimezonePickerWindow(Configuration);
        addEditWindow = new AddEditVenueWindow(Configuration, tagService);
        settingsWindow = new SettingsWindow(Configuration, lifestreamAvailability);
        venueInfoWindow = new VenueInfoWindow(TextureProvider, Configuration, tagService);
        mainWindow = new VenueFinderWindow(Configuration, PlayerState, repository, tagService, travelService, addEditWindow, settingsWindow, timezonePicker, venueInfoWindow);
        popoutWindow = new PopoutWindow(Configuration, repository, travelService, mainWindow, addEditWindow);
        mainWindow.PopoutWindow = popoutWindow;

        windowSystem.AddWindow(addEditWindow);
        windowSystem.AddWindow(settingsWindow);
        windowSystem.AddWindow(timezonePicker);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(popoutWindow);
        windowSystem.AddWindow(venueInfoWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Active Venue Finder. Use '/avf config' for settings.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        popoutWindow.Dispose();
        mainWindow.Dispose();
        venueInfoWindow.Dispose();
        repository.Dispose();
        apiClient.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = (args ?? "").Trim();
        if (trimmed.Equals("config", System.StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("settings", System.StringComparison.OrdinalIgnoreCase))
        {
            settingsWindow.IsOpen = true;
            return;
        }
        mainWindow.IsOpen = true;
    }

    private void OnOpenMainUi() => mainWindow.IsOpen = true;
    private void OnOpenConfigUi() => settingsWindow.IsOpen = true;

    internal static void SendChatCommand(string cmd) => CommandManager.ProcessCommand(cmd);
}
