using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ActiveVenueFinder.Models;
using ActiveVenueFinder.Services;
using ActiveVenueFinder.Services.Tags;
using ActiveVenueFinder.Services.Validation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ActiveVenueFinder.Windows;

public sealed class AddEditVenueWindow : Window
{
    private readonly Config config;
    private readonly VenueTagService tagService;

    private enum EditMode { AddCustom, EditCustom, EditApi }
    private EditMode editMode;
    private int editingCustomId;
    private string editingApiId = "";
    private List<string> editingApiTags = new();

    private string name = "";
    private string world = "";
    private string district = "";
    private int ward = 1;
    private int plot = 1;
    private int apartment;
    private bool hasApartment;
    private bool subdivision;
    private bool sfw = true;
    private List<string> localTags = new();
    private string newTagInput = "";
    private List<string> validationErrors = new();

    public Action? OnSaved { get; set; }

    private static readonly string[] DayNames =
        { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };

    private readonly List<List<EditableSlot>> daySlots = new();

    private sealed class EditableSlot
    {
        public int StartHour;
        public int StartMinute;
        public int EndHour;
        public int EndMinute;
        public bool NextDay;
    }

    public AddEditVenueWindow(Config config, VenueTagService tagService)
        : base("Add Venue###AddEditVenue")
    {
        this.config = config;
        this.tagService = tagService;
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
        editingApiTags = new List<string>();
        ResetFields();
        IsOpen = true;
    }

    public void OpenEdit(CustomVenue cv)
    {
        editMode = EditMode.EditCustom;
        editingCustomId = cv.Id;
        editingApiId = "";
        editingApiTags = new List<string>();
        PopulateFromCustomVenue(cv);
        IsOpen = true;
    }

    public void OpenEditApi(string apiId, Venue venue)
    {
        editMode = EditMode.EditApi;
        editingApiId = apiId;
        editingCustomId = 0;
        editingApiTags = venue.Tags?.ToList() ?? new List<string>();

        if (config.VenueOverrides.TryGetValue(apiId, out var ov))
            PopulateFromOverride(ov, venue);
        else
            PopulateFromVenue(venue);

        IsOpen = true;
    }

    private VenueKey GetEditingVenueKey() => editMode == EditMode.EditApi
        ? VenueKey.Api(editingApiId)
        : VenueKey.Custom(editingCustomId == 0 ? config.NextCustomVenueId : editingCustomId);

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
        localTags = tagService.GetLocal(VenueKey.Custom(cv.Id)).ToList();
        LoadSchedules(cv.Schedules);
        validationErrors.Clear();
    }

    private void PopulateFromOverride(VenueOverride ov, Venue apiVenue)
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
        localTags = tagService.GetLocal(VenueKey.FromVenue(apiVenue)).ToList();
        LoadSchedules(ov.Schedules);
        validationErrors.Clear();
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
        localTags = tagService.GetLocal(VenueKey.FromVenue(venue)).ToList();
        LoadApiSchedules(venue.Schedule);
        validationErrors.Clear();
    }

    private void LoadApiSchedules(List<VenueSchedule> schedules)
    {
        ResetSchedules();
        foreach (var s in schedules)
        {
            // API: Mon=0..Sun=6 → EditableSlot-Index (.NET DayOfWeek): Sun=0..Sat=6
            var idx = ((s.Day % 7) + 8) % 7;
            daySlots[idx].Add(new EditableSlot
            {
                StartHour = s.Start.Hour,
                StartMinute = s.Start.Minute,
                EndHour = s.End.Hour,
                EndMinute = s.End.Minute,
                NextDay = s.End.NextDay,
            });
        }
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
        localTags = new List<string>();
        newTagInput = "";
        validationErrors.Clear();
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

        ImGui.Separator();
        ImGui.TextUnformatted("Tags");

        if (editMode == EditMode.EditApi && editingApiTags.Count > 0)
        {
            ImGui.TextDisabled("From ffxivvenues.com (read-only):");
            foreach (var apiTag in editingApiTags)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.78f, 1f, 1f));
                ImGui.TextUnformatted("[" + apiTag + "]");
                ImGui.PopStyleColor();
            }
            ImGui.NewLine();
        }

        ImGui.TextDisabled("Local tags (your own):");
        DrawLocalTagChips();

        ImGui.TextDisabled("Quick add:");
        DrawQuickAddTags();

        ImGui.SetNextItemWidth(180);
        ImGui.InputText("##newTag", ref newTagInput, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add Tag") && !string.IsNullOrWhiteSpace(newTagInput))
        {
            AddLocalTag(newTagInput.Trim());
            newTagInput = "";
        }
        ImGui.SameLine();
        if (ImGui.Button("Save as suggestion"))
        {
            var t = newTagInput.Trim();
            if (!string.IsNullOrEmpty(t) && !config.CustomTags.Contains(t, StringComparer.OrdinalIgnoreCase))
            {
                config.CustomTags.Add(t);
                config.Save();
            }
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

        if (validationErrors.Count > 0)
        {
            foreach (var err in validationErrors)
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), err);
            ImGui.Spacing();
        }

        if (ImGui.Button("Save"))
            Save();
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            IsOpen = false;
    }

    private void DrawLocalTagChips()
    {
        if (localTags.Count == 0)
        {
            ImGui.TextDisabled("(none)");
            return;
        }
        for (var i = 0; i < localTags.Count; i++)
        {
            ImGui.SameLine();
            var tag = localTags[i];
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.45f, 0.25f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.55f, 0.35f, 0.9f));
            if (ImGui.SmallButton(tag + " x##rmtag" + i))
            {
                localTags.RemoveAt(i);
                ImGui.PopStyleColor(2);
                return;
            }
            ImGui.PopStyleColor(2);
        }
        ImGui.NewLine();
    }

    private void DrawQuickAddTags()
    {
        var any = false;
        foreach (var tag in VenueTagService.PredefinedTags)
        {
            if (localTags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) continue;
            ImGui.SameLine();
            if (ImGui.SmallButton("+ " + tag + "##pre"))
                AddLocalTag(tag);
            any = true;
        }
        foreach (var tag in config.CustomTags)
        {
            if (string.IsNullOrWhiteSpace(tag)) continue;
            if (localTags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) continue;
            ImGui.SameLine();
            if (ImGui.SmallButton("+ " + tag + "##cus"))
                AddLocalTag(tag);
            any = true;
        }
        if (any) ImGui.NewLine();
        else { ImGui.TextDisabled("(no suggestions)"); }
    }

    private void AddLocalTag(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        if (editingApiTags.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))) return;
        if (localTags.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))) return;
        localTags.Add(trimmed);
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
        var input = new VenueInputValidator.Input(
            Name: name,
            World: world,
            District: district,
            Ward: ward,
            Plot: plot,
            Apartment: hasApartment ? apartment : (int?)null,
            IsCustomMode: editMode != EditMode.EditApi,
            EditingCustomId: editMode == EditMode.EditCustom ? editingCustomId : 0);

        var result = VenueInputValidator.Validate(input, config);
        if (!result.Ok)
        {
            validationErrors = result.Errors.ToList();
            return;
        }
        validationErrors.Clear();

        var schedules = BuildSchedules();

        switch (editMode)
        {
            case EditMode.AddCustom:
            {
                var newId = config.NextCustomVenueId++;
                config.CustomVenues.Add(new CustomVenue
                {
                    Id = newId,
                    Name = name,
                    World = world,
                    District = district,
                    Ward = ward,
                    Plot = plot,
                    Apartment = hasApartment ? apartment : null,
                    Subdivision = subdivision,
                    Sfw = sfw,
                    Schedules = schedules,
                });
                tagService.SetLocal(VenueKey.Custom(newId), localTags);
                break;
            }

            case EditMode.EditCustom:
            {
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
                    existing.Schedules = schedules;
                    tagService.SetLocal(VenueKey.Custom(editingCustomId), localTags);
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
                    Schedules = schedules,
                };
                tagService.SetLocal(VenueKey.Api(editingApiId), localTags, editingApiTags);
                break;
            }
        }

        config.Save();
        OnSaved?.Invoke();
        IsOpen = false;
    }
}
