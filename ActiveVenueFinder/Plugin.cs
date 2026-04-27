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
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    private const string CommandName = "/avf";

    public Config Configuration { get; }

    private readonly WindowSystem windowSystem = new("ActiveVenueFinder");
    private readonly VenueFinderWindow mainWindow;
    private readonly AddEditVenueWindow addEditWindow;
    private readonly PopoutWindow popoutWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly TimezonePickerWindow timezonePicker;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();

        timezonePicker = new TimezonePickerWindow(Configuration);
        addEditWindow = new AddEditVenueWindow(Configuration, timezonePicker);
        settingsWindow = new SettingsWindow(Configuration);
        mainWindow = new VenueFinderWindow(Configuration, PlayerState, addEditWindow, settingsWindow, timezonePicker);
        popoutWindow = new PopoutWindow(Configuration, mainWindow, addEditWindow);
        mainWindow.PopoutWindow = popoutWindow;

        windowSystem.AddWindow(addEditWindow);
        windowSystem.AddWindow(settingsWindow);
        windowSystem.AddWindow(timezonePicker);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(popoutWindow);

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
