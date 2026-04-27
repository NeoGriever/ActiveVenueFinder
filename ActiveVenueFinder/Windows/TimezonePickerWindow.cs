using System;
using System.Linq;
using System.Numerics;
using ActiveVenueFinder.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ActiveVenueFinder.Windows;

public sealed class TimezonePickerWindow : Window
{
    private readonly Config config;
    private string searchText = "";
    private Action<TimezoneEntry>? pendingCallback;

    public TimezonePickerWindow(Config config)
        : base("Select Timezone###AvfTimezonePicker")
    {
        this.config = config;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 350),
            MaximumSize = new Vector2(700, 800),
        };
        Size = new Vector2(500, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void OpenPicker(Action<TimezoneEntry> onSelected)
    {
        pendingCallback = onSelected;
        searchText = "";
        IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##tzSearch", "Search...", ref searchText, 64);

        ImGui.Separator();

        var entries = TimezoneRegistry.All;
        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? entries
            : entries.Where(e =>
                e.DisplayLabel.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || e.Region.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || e.Id.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        if (ImGui.BeginChild("##tzList", new Vector2(0, -1)))
        {
            string? lastRegion = null;
            foreach (var e in filtered)
            {
                if (e.Region != lastRegion)
                {
                    if (lastRegion != null) ImGui.Spacing();
                    ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), e.Region);
                    ImGui.Separator();
                    lastRegion = e.Region;
                }

                var isCurrent = config.SelectedTimezoneId == e.Id;
                if (isCurrent) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1f, 0.4f, 1f));
                ImGui.TextUnformatted(e.DisplayLabel);
                if (isCurrent) ImGui.PopStyleColor();

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 80);
                if (ImGui.SmallButton($"Select##{e.Id}"))
                {
                    config.SelectedTimezoneId = e.Id;
                    config.Save();
                    pendingCallback?.Invoke(e);
                    IsOpen = false;
                }
            }
        }
        ImGui.EndChild();
    }
}
