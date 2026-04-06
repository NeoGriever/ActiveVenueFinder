using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ActiveVenueFinder.Windows;

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

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();

        addEditWindow = new AddEditVenueWindow(Configuration);
        mainWindow = new VenueFinderWindow(Configuration, PlayerState, addEditWindow);
        popoutWindow = new PopoutWindow(Configuration, mainWindow, addEditWindow);
        mainWindow.PopoutWindow = popoutWindow;
        windowSystem.AddWindow(addEditWindow);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(popoutWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = "Open Active Venue Finder." });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        popoutWindow.Dispose();
        mainWindow.Dispose();
    }

    private void OnCommand(string command, string args) => mainWindow.IsOpen = true;
    private void OnOpenMainUi() => mainWindow.IsOpen = true;

    internal static void SendChatCommand(string cmd)
    {
        CommandManager.ProcessCommand(cmd);
    }
}
