# Active Venue Finder

Active Venue Finder is a Dalamud plugin for browsing Final Fantasy XIV venues using public venue data from ffxivvenues.com.

The plugin is primarily an information and overview tool. It loads venue data, displays it in a searchable table, and helps you inspect venue details such as location, opening times, tags, website links, Discord links, and related venue information.

## Features

- Browse venues from ffxivvenues.com
- Search and filter the venue list
- View venue details in a dedicated info window
- Display venue opening information in a timeline-oriented view
- Use venue tags provided by ffxivvenues.com
- Add local custom tags for personal organization
- Add and manage local custom venues
- Mark venues as favorites or hide unwanted entries
- Optionally travel to a venue plot through Lifestream, if Lifestream is installed and enabled

## Usage

Open the plugin with `/avf`.

This opens the main venue browser. From there, you can search for venues, filter the list, open detailed venue information, and manage local entries or tags depending on the available options in the UI.

Double-clicking a venue opens its information view by default.

## Venue Information

The venue information view shows the selected venue in more detail.

Depending on the available data, this may include:

- Venue name
- World and data center
- Housing district, ward, plot, apartment, or subdivision information
- Opening times
- Tags
- Description
- Website links
- Discord links
- Banner or venue image

External links are only opened when you explicitly click them.

## Tags

Active Venue Finder uses tags provided by ffxivvenues.com where available.

You can also add local tags to venues. Local tags are stored in your plugin configuration and are only used by your local installation. They do not modify data on ffxivvenues.com.

API tags and local tags are kept separate internally, but both can be used for searching and organizing venues.

## Timeline View

The timeline view helps you see venue openings in a time-based layout.

By default, the timeline uses American Eastern time for display. This only affects the timeline view and does not change the underlying venue data.

## Optional Lifestream Integration

Active Venue Finder can optionally integrate with Lifestream to make traveling to venue plots more convenient.

This integration is optional. If Lifestream is not installed, loaded, and enabled, travel controls are hidden and the plugin continues to work as a venue browser.

When Lifestream is available, travel actions are only triggered by explicit user interaction, such as pressing a travel button or selecting a travel action. The plugin does not automatically teleport or travel in the background.

## Data Sources and Network Usage

Active Venue Finder loads public venue data from ffxivvenues.com.

The plugin may also load venue images or banners when venue details are opened. No travel action, external link, or website is opened without user interaction.

The plugin is designed as a browsing and organization tool and does not require Lifestream to be useful.

## AI Assistance Disclosure

Claude Code was used as an assistance tool during development.

It was used to help implement selected features, understand and apply documentation, identify potential errors, and suggest possible solution approaches. The project was reviewed and adjusted manually, and the final responsibility for the code, behavior, testing, and maintenance remains with the plugin author.

## Notes

This plugin depends on publicly available venue data. If ffxivvenues.com is unavailable or returns incomplete data, some venue entries may be missing or outdated until the data source is reachable again.

Local custom venues, favorites, hidden entries, and local tags are stored in the plugin configuration.
