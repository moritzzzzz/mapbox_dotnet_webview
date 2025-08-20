
# WPF (dotnet) Mapbox Online Viewer

A Windows desktop application that displays Mapbox maps using the Mapbox GL JS library for online map rendering.

## Features

- **Online Mapbox Maps** - Displays maps directly from Mapbox services
- WPF application with WebView2 integration
- Mapbox GL JS integration for modern map rendering
- **Route Display** - Display GeoJSON routes on the map
- **Driver Animation** - Animate a driver marker moving along displayed routes with configurable speed
- Support for all Mapbox style URLs and custom styles

<img width="1221" height="858" alt="image" src="https://github.com/user-attachments/assets/ba0321d6-adc7-429b-9ebe-e9af8e96fa55" />


## Requirements

- .NET 8.0 or higher
- Windows 10/11
- WebView2 Runtime (usually pre-installed on Windows 11)
- **Mapbox Access Token** - Required for map display
- Internet connection for map tiles

## Setup

### Getting a Mapbox Access Token

1. Go to [Mapbox](https://www.mapbox.com/) and create a free account
2. Navigate to your [Account page](https://account.mapbox.com/)
3. Create a new access token or use your default public token
4. Copy the token (starts with `pk.`)

## Building and Running

1. **Build the project:**
   ```bash
   dotnet build
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

## Usage

### Basic Map Display
1. Launch the application
2. Enter your Mapbox access token in the "Mapbox Access Token" field
3. Enter a Mapbox style URL (default: `mapbox://styles/mapbox/streets-v11`)
4. Click "Load Map" to display the map
5. The map will load with your specified Mapbox style

### Supported Style URLs
- `mapbox://styles/mapbox/streets-v11` - Street map
- `mapbox://styles/mapbox/satellite-v9` - Satellite imagery
- `mapbox://styles/mapbox/outdoors-v11` - Outdoor/topographic
- `mapbox://styles/mapbox/light-v10` - Light theme
- `mapbox://styles/mapbox/dark-v10` - Dark theme
- Custom style URLs from your Mapbox account

### Route Display and Driver Animation
1. After loading the map, enter a GeoJSON route in the route text box
2. Click "Display Route" to show the route on the map
3. Use the driver animation controls:
   - **Start**: Begin driver animation along the route
   - **Pause**: Pause the animation at current position
   - **Reset**: Return driver to start of route
   - **Speed Slider**: Adjust animation speed (0.1x to 5.0x)

#### Route Formats Supported
- GeoJSON Feature with LineString geometry
- GeoJSON Feature with MultiLineString geometry
- GeoJSON FeatureCollection
- Polyline6 encoded strings

#### Sample Route
Click "Load Sample" to load a sample cross-country route from New York to Los Angeles.

## Customization

### Map Style
You can use any Mapbox style by entering its URL in the Style URL field. This includes:
- Pre-built Mapbox styles
- Custom styles created in Mapbox Studio
- Styles shared by other users (if you have access)

### Access Token Management
For security, consider:
- Using environment variables for access tokens in production
- Implementing token refresh if using temporary tokens
- Restricting token permissions to only what's needed

## Troubleshooting

### Map doesn't load
- Verify your Mapbox access token is valid and not expired
- Check your internet connection
- Ensure the style URL is correctly formatted
- Check the browser console (F12) for detailed error messages

### Authentication errors
- Confirm your access token starts with `pk.` (public token)
- Check that your token has the necessary permissions
- Verify the token hasn't been revoked in your Mapbox account

### Network issues
- Ensure port 443 (HTTPS) is accessible for Mapbox API calls
- Check if your firewall or proxy is blocking requests to `*.mapbox.com`
- Consider using a different network if behind restrictive firewalls

### Performance
For better performance:
- Use appropriate zoom levels for your use case
- Consider Mapbox's usage limits and pricing
- Monitor your token usage in the Mapbox account dashboard

## License and Usage

This application uses:
- **Mapbox GL JS** - Subject to Mapbox Terms of Service
- **Mapbox Maps API** - Usage tracked and billed according to your Mapbox plan

Please review [Mapbox's Terms of Service](https://www.mapbox.com/legal/tos) for details on usage limits and pricing.
