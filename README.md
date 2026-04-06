# Active Venue Finder

A Dalamud plugin for Final Fantasy XIV that lets you browse active venues from [FFXIV Venues](https://ffxivvenues.com) and teleport to them with one click using [Lifestream](https://github.com/NightmareXIV/Lifestream).

![icon](ActiveVenueFinder/images/icon.png)

## Features

- **Live Venue List** -- Fetches venues from the FFXIV Venues API and displays them in a sortable table with datacenter-colored entries
- **One-Click Teleport** -- Double-click any venue to travel there via Lifestream (`/li` command)
- **Popout Window** -- A compact widget showing your favorites and the 3 most recently opened venues, always accessible
- **Favorites & Blacklist** -- Star venues you visit often and hide ones you don't want to see
- **Custom Venues** -- Add your own venues with name, location, schedule, and tags -- they appear alongside API venues
- **Venue Overrides** -- Edit API venue details locally (name, location, schedule, tags) without affecting the source
- **Tag System** -- Filter venues by predefined tags (Gamba, Giveaway, Court) or create custom tags. Search with `T:tagname`
- **Timeline Bar** -- Visual timeline showing when each venue opens and closes across a 48-hour window
- **Timezone Support** -- Switch the displayed times to any system timezone
- **Lookahead** -- Preview which venues will be open in the coming hours
- **Region Awareness** -- Venues on your datacenter region are highlighted, cross-region venues are dimmed
- **SFW Indicator** -- Each venue shows whether it's marked as SFW or not

## Requirements

- [Dalamud](https://github.com/goatcorp/Dalamud) (API Level 14)
- [Lifestream](https://github.com/NightmareXIV/Lifestream) plugin (for teleportation)

## Installation

Add the following custom repository URL in Dalamud's plugin installer settings:

```
https://raw.githubusercontent.com/NeoGriever/ActiveVenueFinder/main/pluginmaster.json
```

Then search for **Active Venue Finder** in the plugin list and install it.

## Usage

Open the main window with the chat command:

```
/avf
```

### Main Window

The main window shows all venues in a table. You can:

- **Sort** by clicking column headers (Name, World, Ward, Time, Remaining, etc.)
- **Search** by typing in the search bar -- use `T:` prefix to search by tag
- **Teleport** by double-clicking a venue row
- **Right-click** a venue for more options:
  - Open venue page on ffxivvenues.com
  - Travel to venue
  - Copy Lifestream command
  - Add/remove from favorites
  - Blacklist/unblacklist
  - Edit venue details (creates a local override)

### Popout Window

Toggle the popout via the main window. It shows a compact list of:
- All your favorited active venues
- The 3 most recently opened non-favorite venues

Double-click to teleport, right-click for the context menu.

### Custom Venues

Add venues that aren't listed on FFXIV Venues:

1. Right-click in the main window and select "Add Venue"
2. Fill in name, world, district, ward, plot (or apartment)
3. Set a weekly schedule with timezone
4. Add tags as needed
5. Save -- the venue appears in your list like any other

## Building from Source

```bash
dotnet build ActiveVenueFinder/ActiveVenueFinder.csproj -c Release
```

The build uses DalamudPackager to produce `latest.zip` in the Release output directory.
