using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ActiveVenueFinder.Models;

namespace ActiveVenueFinder.Windows;

public sealed class AddEditVenueWindow : Window
{
    private readonly Config config;

    private enum EditMode { AddCustom, EditCustom, EditApi }
    private EditMode editMode;
    private int editingCustomId;
    private string editingApiId = "";

    private string name = "";
    private string world = "";
    private string district = "";
    private int ward = 1;
    private int plot = 1;
    private int apartment;
    private bool hasApartment;
    private bool subdivision;
    private bool sfw = true;
    private int selectedTimezoneIndex;
    private HashSet<string> selectedTags = new();
    private string newTagInput = "";

    public Action? OnSaved { get; set; }

    private static readonly (string Label, string Id)[] TimezoneOptions =
    {
        ("EST", "America/New_York"),
        ("CST", "America/Chicago"),
        ("MST", "America/Denver"),
        ("PST", "America/Los_Angeles"),
    };

    private static readonly string[] DayNames =
        { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    private readonly bool[] scheduleActive = new bool[7];
    private readonly int[] scheduleStartHour = new int[7];
    private readonly int[] scheduleStartMinute = new int[7];
    private readonly int[] scheduleEndHour = new int[7];
    private readonly int[] scheduleEndMinute = new int[7];
    private readonly bool[] scheduleNextDay = new bool[7];

    public AddEditVenueWindow(Config config)
        : base("Add Venue###AddEditVenue")
    {
        this.config = config;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(600, 800),
        };
        Size = new Vector2(500, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void OpenAdd()
    {
        editMode = EditMode.AddCustom;
        editingCustomId = 0;
        editingApiId = "";
        ResetFields();
        IsOpen = true;
    }

    public void OpenEdit(CustomVenue cv)
    {
        editMode = EditMode.EditCustom;
        editingCustomId = cv.Id;
        editingApiId = "";
        PopulateFromCustomVenue(cv);
        IsOpen = true;
    }

    public void OpenEditApi(string apiId, Venue venue)
    {
        editMode = EditMode.EditApi;
        editingApiId = apiId;
        editingCustomId = 0;

        if (config.VenueOverrides.TryGetValue(apiId, out var ov))
            PopulateFromOverride(ov);
        else
            PopulateFromVenue(venue);

        IsOpen = true;
    }

    private void PopulateFromCustomVenue(CustomVenue cv)
    {
        name = cv.Name;
        world = cv.World;
        district = cv.District;
        ward = cv.Ward;
        plot = cv.Plot;
        hasApartment = cv.Apartment.HasValue;
        apartment = cv.Apartment ?? 0;
        subdivision = cv.Subdivision;
        sfw = cv.Sfw;

        selectedTimezoneIndex = FindTimezoneIndex(cv.TimezoneId);
        selectedTags = new HashSet<string>(cv.Tags);
        LoadSchedules(cv.Schedules);
    }

    private void PopulateFromOverride(VenueOverride ov)
    {
        name = ov.Name;
        world = ov.World;
        district = ov.District;
        ward = ov.Ward;
        plot = ov.Plot;
        hasApartment = ov.Apartment.HasValue;
        apartment = ov.Apartment ?? 0;
        subdivision = ov.Subdivision;
        sfw = ov.Sfw;

        selectedTimezoneIndex = FindTimezoneIndex(ov.TimezoneId);
        selectedTags = new HashSet<string>(ov.Tags);
        LoadSchedules(ov.Schedules);
    }

    private void PopulateFromVenue(Venue venue)
    {
        name = venue.Name;
        world = venue.Location.World;
        district = venue.Location.District;
        ward = venue.Location.Ward;
        plot = venue.Location.Plot;
        hasApartment = venue.Location.Apartment.HasValue;
        apartment = venue.Location.Apartment ?? 0;
        subdivision = venue.Location.Subdivision;
        sfw = venue.Sfw;

        selectedTimezoneIndex = 0;
        selectedTags = new HashSet<string>();
        ResetSchedules();
    }

    private static int FindTimezoneIndex(string timezoneId)
    {
        for (var i = 0; i < TimezoneOptions.Length; i++)
        {
            if (TimezoneOptions[i].Id == timezoneId)
                return i;
        }
        return 0;
    }

    private void LoadSchedules(List<CustomVenueSchedule> schedules)
    {
        ResetSchedules();
        foreach (var s in schedules)
        {
            var idx = (int)s.Day;
            scheduleActive[idx] = true;
            scheduleStartHour[idx] = s.StartHour;
            scheduleStartMinute[idx] = s.StartMinute;
            scheduleEndHour[idx] = s.EndHour;
            scheduleEndMinute[idx] = s.EndMinute;
            scheduleNextDay[idx] = s.NextDay;
        }
    }

    private void ResetSchedules()
    {
        for (var i = 0; i < 7; i++)
        {
            scheduleActive[i] = false;
            scheduleStartHour[i] = 20;
            scheduleStartMinute[i] = 0;
            scheduleEndHour[i] = 23;
            scheduleEndMinute[i] = 0;
            scheduleNextDay[i] = false;
        }
    }

    private void ResetFields()
    {
        name = "";
        world = "";
        district = "";
        ward = 1;
        plot = 1;
        apartment = 0;
        hasApartment = false;
        subdivision = false;
        sfw = true;
        selectedTimezoneIndex = 0;
        selectedTags = new HashSet<string>();
        newTagInput = "";
        ResetSchedules();
    }

    public override void Draw()
    {
        WindowName = editMode == EditMode.AddCustom
            ? "Add Venue###AddEditVenue"
            : "Edit Venue###AddEditVenue";

        ImGui.InputText("Name", ref name, 128);
        ImGui.InputText("World", ref world, 64);
        ImGui.InputText("District", ref district, 64);
        ImGui.InputInt("Ward", ref ward);
        ImGui.InputInt("Plot", ref plot);
        ImGui.Checkbox("Has Apartment", ref hasApartment);
        if (hasApartment)
            ImGui.InputInt("Apartment", ref apartment);
        ImGui.Checkbox("Subdivision", ref subdivision);
        ImGui.Checkbox("SFW", ref sfw);

        ImGui.SetNextItemWidth(150);
        if (ImGui.BeginCombo("Timezone", TimezoneOptions[selectedTimezoneIndex].Label))
        {
            for (var i = 0; i < TimezoneOptions.Length; i++)
            {
                if (ImGui.Selectable(TimezoneOptions[i].Label, selectedTimezoneIndex == i))
                    selectedTimezoneIndex = i;
            }
            ImGui.EndCombo();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Tags");

        foreach (var tag in VenueFinderWindow.PredefinedTags)
        {
            var isSet = selectedTags.Contains(tag);
            if (ImGui.Checkbox(tag + "##tag", ref isSet))
            {
                if (isSet) selectedTags.Add(tag);
                else selectedTags.Remove(tag);
            }
            ImGui.SameLine();
        }
        ImGui.NewLine();

        foreach (var tag in config.CustomTags)
        {
            var isSet = selectedTags.Contains(tag);
            if (ImGui.Checkbox(tag + "##ctag", ref isSet))
            {
                if (isSet) selectedTags.Add(tag);
                else selectedTags.Remove(tag);
            }
            ImGui.SameLine();
        }
        if (config.CustomTags.Count > 0)
            ImGui.NewLine();

        ImGui.SetNextItemWidth(120);
        ImGui.InputText("##newTag", ref newTagInput, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add Tag") && !string.IsNullOrWhiteSpace(newTagInput))
        {
            var tagName = newTagInput.Trim();
            if (!VenueFinderWindow.PredefinedTags.Contains(tagName) && !config.CustomTags.Contains(tagName))
            {
                config.CustomTags.Add(tagName);
                config.Save();
            }
            selectedTags.Add(tagName);
            newTagInput = "";
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Schedule");

        for (var i = 0; i < 7; i++)
        {
            ImGui.PushID(i);
            ImGui.Checkbox(DayNames[i], ref scheduleActive[i]);
            if (scheduleActive[i])
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##sh", ref scheduleStartHour[i]);
                scheduleStartHour[i] = Math.Clamp(scheduleStartHour[i], 0, 23);
                ImGui.SameLine();
                ImGui.TextUnformatted(":");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##sm", ref scheduleStartMinute[i]);
                scheduleStartMinute[i] = Math.Clamp(scheduleStartMinute[i], 0, 59);
                ImGui.SameLine();
                ImGui.TextUnformatted("-");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##eh", ref scheduleEndHour[i]);
                scheduleEndHour[i] = Math.Clamp(scheduleEndHour[i], 0, 23);
                ImGui.SameLine();
                ImGui.TextUnformatted(":");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##em", ref scheduleEndMinute[i]);
                scheduleEndMinute[i] = Math.Clamp(scheduleEndMinute[i], 0, 59);
                ImGui.SameLine();
                ImGui.Checkbox("Next Day", ref scheduleNextDay[i]);
            }
            ImGui.PopID();
        }

        ImGui.Separator();

        if (ImGui.Button("Save"))
            Save();
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            IsOpen = false;
    }

    private List<CustomVenueSchedule> BuildSchedules()
    {
        var schedules = new List<CustomVenueSchedule>();
        for (var i = 0; i < 7; i++)
        {
            if (scheduleActive[i])
            {
                schedules.Add(new CustomVenueSchedule
                {
                    Day = (DayOfWeek)i,
                    StartHour = scheduleStartHour[i],
                    StartMinute = scheduleStartMinute[i],
                    EndHour = scheduleEndHour[i],
                    EndMinute = scheduleEndMinute[i],
                    NextDay = scheduleNextDay[i],
                });
            }
        }
        return schedules;
    }

    private void Save()
    {
        var schedules = BuildSchedules();

        switch (editMode)
        {
            case EditMode.AddCustom:
            {
                var isDuplicate = config.CustomVenues.Any(cv =>
                    cv.World == world &&
                    cv.District == district &&
                    cv.Ward == ward &&
                    cv.Plot == plot);
                if (isDuplicate)
                    return;

                config.CustomVenues.Add(new CustomVenue
                {
                    Id = config.NextCustomVenueId++,
                    Name = name,
                    World = world,
                    District = district,
                    Ward = ward,
                    Plot = plot,
                    Apartment = hasApartment ? apartment : null,
                    Subdivision = subdivision,
                    Sfw = sfw,
                    TimezoneId = TimezoneOptions[selectedTimezoneIndex].Id,
                    Schedules = schedules,
                    Tags = new HashSet<string>(selectedTags),
                });
                break;
            }

            case EditMode.EditCustom:
            {
                var isDuplicate = config.CustomVenues.Any(cv =>
                    cv.Id != editingCustomId &&
                    cv.World == world &&
                    cv.District == district &&
                    cv.Ward == ward &&
                    cv.Plot == plot);
                if (isDuplicate)
                    return;

                var existing = config.CustomVenues.FirstOrDefault(cv => cv.Id == editingCustomId);
                if (existing != null)
                {
                    existing.Name = name;
                    existing.World = world;
                    existing.District = district;
                    existing.Ward = ward;
                    existing.Plot = plot;
                    existing.Apartment = hasApartment ? apartment : null;
                    existing.Subdivision = subdivision;
                    existing.Sfw = sfw;
                    existing.TimezoneId = TimezoneOptions[selectedTimezoneIndex].Id;
                    existing.Schedules = schedules;
                    existing.Tags = new HashSet<string>(selectedTags);
                }
                break;
            }

            case EditMode.EditApi:
            {
                config.VenueOverrides[editingApiId] = new VenueOverride
                {
                    Name = name,
                    World = world,
                    District = district,
                    Ward = ward,
                    Plot = plot,
                    Apartment = hasApartment ? apartment : null,
                    Subdivision = subdivision,
                    Sfw = sfw,
                    TimezoneId = TimezoneOptions[selectedTimezoneIndex].Id,
                    Schedules = schedules,
                    Tags = new HashSet<string>(selectedTags),
                };
                break;
            }
        }

        config.Save();
        OnSaved?.Invoke();
        IsOpen = false;
    }
}
