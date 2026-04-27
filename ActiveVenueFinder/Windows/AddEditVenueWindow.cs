using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ActiveVenueFinder.Windows;

public sealed class AddEditVenueWindow : Window
{
    private readonly Config config;
    private readonly TimezonePickerWindow timezonePicker;

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
    private string timezoneId = "America/New_York";
    private HashSet<string> selectedTags = new();
    private string newTagInput = "";

    public Action? OnSaved { get; set; }

    private static readonly string[] DayNames =
        { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    // Multi-slot per day: List of slots per day-of-week
    private readonly List<List<EditableSlot>> daySlots = new();

    private sealed class EditableSlot
    {
        public int StartHour;
        public int StartMinute;
        public int EndHour;
        public int EndMinute;
        public bool NextDay;
    }

    public AddEditVenueWindow(Config config, TimezonePickerWindow timezonePicker)
        : base("Add Venue###AddEditVenue")
    {
        this.config = config;
        this.timezonePicker = timezonePicker;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 350),
            MaximumSize = new Vector2(700, 1000),
        };
        Size = new Vector2(560, 700);
        SizeCondition = ImGuiCond.FirstUseEver;

        for (var i = 0; i < 7; i++)
            daySlots.Add(new List<EditableSlot>());
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
        timezoneId = cv.TimezoneId;
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
        timezoneId = ov.TimezoneId;
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
        timezoneId = "America/New_York";
        selectedTags = new HashSet<string>();
        ResetSchedules();
    }

    private void LoadSchedules(List<CustomVenueSchedule> schedules)
    {
        ResetSchedules();
        foreach (var s in schedules)
        {
            var idx = (int)s.Day;
            daySlots[idx].Add(new EditableSlot
            {
                StartHour = s.StartHour,
                StartMinute = s.StartMinute,
                EndHour = s.EndHour,
                EndMinute = s.EndMinute,
                NextDay = s.NextDay,
            });
        }
    }

    private void ResetSchedules()
    {
        for (var i = 0; i < 7; i++)
            daySlots[i].Clear();
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
        timezoneId = "America/New_York";
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

        // Timezone via picker
        var entry = TimezoneRegistry.FindById(timezoneId);
        var label = entry?.DisplayLabel ?? timezoneId;
        ImGui.SetNextItemWidth(280);
        if (ImGui.Button($"TZ: {label}##editTzBtn"))
        {
            timezonePicker.OpenPicker(e => timezoneId = e.Id);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Select schedule timezone");

        ImGui.Separator();
        ImGui.TextUnformatted("Tags");

        foreach (var tag in VenueResolver.PredefinedTags)
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
            if (!VenueResolver.PredefinedTags.Contains(tagName) && !config.CustomTags.Contains(tagName))
            {
                config.CustomTags.Add(tagName);
                config.Save();
            }
            selectedTags.Add(tagName);
            newTagInput = "";
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Schedule (multiple slots per day allowed)");

        for (var i = 0; i < 7; i++)
        {
            ImGui.PushID(i);
            ImGui.TextColored(new Vector4(0.7f, 0.85f, 1f, 1f), DayNames[i]);
            ImGui.SameLine();
            if (ImGui.SmallButton("+ slot"))
            {
                daySlots[i].Add(new EditableSlot
                {
                    StartHour = 20, StartMinute = 0,
                    EndHour = 23, EndMinute = 0,
                    NextDay = false,
                });
            }

            for (var j = 0; j < daySlots[i].Count; j++)
            {
                ImGui.PushID(j);
                var slot = daySlots[i][j];
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##sh", ref slot.StartHour);
                slot.StartHour = Math.Clamp(slot.StartHour, 0, 23);
                ImGui.SameLine(); ImGui.TextUnformatted(":"); ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##sm", ref slot.StartMinute);
                slot.StartMinute = Math.Clamp(slot.StartMinute, 0, 59);
                ImGui.SameLine(); ImGui.TextUnformatted("-"); ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##eh", ref slot.EndHour);
                slot.EndHour = Math.Clamp(slot.EndHour, 0, 23);
                ImGui.SameLine(); ImGui.TextUnformatted(":"); ImGui.SameLine();
                ImGui.SetNextItemWidth(40);
                ImGui.InputInt("##em", ref slot.EndMinute);
                slot.EndMinute = Math.Clamp(slot.EndMinute, 0, 59);
                ImGui.SameLine();
                ImGui.Checkbox("Next Day", ref slot.NextDay);
                ImGui.SameLine();
                if (ImGui.SmallButton("X"))
                {
                    daySlots[i].RemoveAt(j);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopID();
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
            foreach (var slot in daySlots[i])
            {
                schedules.Add(new CustomVenueSchedule
                {
                    Day = (DayOfWeek)i,
                    StartHour = slot.StartHour,
                    StartMinute = slot.StartMinute,
                    EndHour = slot.EndHour,
                    EndMinute = slot.EndMinute,
                    NextDay = slot.NextDay,
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
                    TimezoneId = timezoneId,
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
                    existing.TimezoneId = timezoneId;
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
                    TimezoneId = timezoneId,
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
