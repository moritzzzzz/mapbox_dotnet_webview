using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace WpfMapboxApp
{
    public partial class MainWindow : Window
    {
        // Driver animation properties
        private bool _isRouteDisplayed = false;
        private bool _isAnimationRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.CoreWebView2.DocumentTitleChanged += (s, e) => Title = MapWebView.CoreWebView2.DocumentTitle;
            
            // Setup JavaScript message handling
            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            
            // Inject console forwarding after page loads
            MapWebView.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                if (e.IsSuccess)
                {
                    await MapWebView.CoreWebView2.ExecuteScriptAsync(@"
                        // Override console methods to forward to C#
                        (function() {
                            const originalLog = console.log;
                            const originalError = console.error;
                            const originalWarn = console.warn;
                            const originalInfo = console.info;
                            
                            console.log = function(...args) {
                                originalLog.apply(console, args);
                                window.chrome.webview.postMessage({type: 'console', level: 'log', message: args.join(' ')});
                            };
                            
                            console.error = function(...args) {
                                originalError.apply(console, args);
                                window.chrome.webview.postMessage({type: 'console', level: 'error', message: args.join(' ')});
                            };
                            
                            console.warn = function(...args) {
                                originalWarn.apply(console, args);
                                window.chrome.webview.postMessage({type: 'console', level: 'warn', message: args.join(' ')});
                            };
                            
                            console.info = function(...args) {
                                originalInfo.apply(console, args);
                                window.chrome.webview.postMessage({type: 'console', level: 'info', message: args.join(' ')});
                            };
                        })();
                    ");
                }
            };
        }




        private async void LoadMapButton_Click(object sender, RoutedEventArgs e)
        {
            var accessToken = AccessTokenTextBox.Text.Trim();
            var styleUrl = StyleUrlTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(accessToken))
            {
                MessageBox.Show("Please enter your Mapbox access token!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            if (string.IsNullOrEmpty(styleUrl))
            {
                MessageBox.Show("Please enter a Mapbox style URL!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await LoadMapInWebView(accessToken, styleUrl);
                StatusTextBlock.Text = "Status: Map loaded successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading map: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private async void DisplayRouteButton_Click(object sender, RoutedEventArgs e)
        {
            var routeText = RouteTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(routeText))
            {
                MessageBox.Show("Please enter a GeoJSON route!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await DisplayRouteOnMap(routeText);
                StatusTextBlock.Text = "Status: Route displayed on map";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SampleRouteButton_Click(object sender, RoutedEventArgs e)
        {
            // Sample GeoJSON LineString route (from New York to Los Angeles via some waypoints)
            var sampleRoute = @"{
  ""type"": ""Feature"",
  ""properties"": {
    ""name"": ""Sample Route""
  },
  ""geometry"": {
    ""type"": ""LineString"",
    ""coordinates"": [
      [-74.0059, 40.7128],
      [-75.1652, 39.9526],
      [-77.0369, 38.9072],
      [-84.3880, 33.7490],
      [-90.0715, 29.9511],
      [-97.7431, 32.7767],
      [-106.4424, 31.7619],
      [-111.8910, 40.7608],
      [-118.2437, 34.0522]
    ]
  }
}";
            RouteTextBox.Text = sampleRoute;
        }

        private async void ClearRouteButton_Click(object sender, RoutedEventArgs e)
        {
            RouteTextBox.Text = "";
            try
            {
                await ClearRouteFromMap();
                StatusTextBlock.Text = "Status: Route cleared from map";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DisplayRouteOnMap(string routeText)
        {
            // Parse and validate the route
            var processedGeoJson = ProcessRouteInput(routeText);
            var escapedGeoJson = processedGeoJson.Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
            
            await MapWebView.CoreWebView2.ExecuteScriptAsync($@"
                try {{
                    // Remove existing route if any
                    if (map.getLayer('route-layer')) {{
                        map.removeLayer('route-layer');
                    }}
                    if (map.getSource('route-source')) {{
                        map.removeSource('route-source');
                    }}
                    
                    // Parse the GeoJSON
                    const routeData = JSON.parse(""{escapedGeoJson}"");
                    console.log('Adding route to map:', routeData);
                    
                    // Store route data globally for driver animation
                    window.currentRoute = routeData;
                    
                    // Add the route source
                    map.addSource('route-source', {{
                        type: 'geojson',
                        data: routeData
                    }});
                    
                    // Add the route layer with blue styling
                    map.addLayer({{
                        id: 'route-layer',
                        type: 'line',
                        source: 'route-source',
                        layout: {{
                            'line-join': 'round',
                            'line-cap': 'round'
                        }},
                        paint: {{
                            'line-color': '#0066cc',
                            'line-width': 4,
                            'line-opacity': 0.8
                        }}
                    }});
                    
                    // Initialize driver animation system
                    window.initializeDriverAnimation();
                    
                    // Fit the map to the route bounds
                    const bounds = new mapboxgl.LngLatBounds();
                    
                    function addCoordinatesToBounds(coords) {{
                        if (coords && coords.length > 0) {{
                            if (typeof coords[0] === 'number') {{
                                // Single coordinate pair
                                bounds.extend(coords);
                            }} else {{
                                // Array of coordinates or nested arrays
                                coords.forEach(coord => {{
                                    if (typeof coord[0] === 'number') {{
                                        bounds.extend(coord);
                                    }} else {{
                                        addCoordinatesToBounds(coord);
                                    }}
                                }});
                            }}
                        }}
                    }}
                    
                    if (routeData.type === 'Feature') {{
                        addCoordinatesToBounds(routeData.geometry.coordinates);
                    }} else if (routeData.type === 'FeatureCollection') {{
                        routeData.features.forEach(feature => {{
                            addCoordinatesToBounds(feature.geometry.coordinates);
                        }});
                    }} else if (routeData.coordinates) {{
                        addCoordinatesToBounds(routeData.coordinates);
                    }}
                    
                    if (!bounds.isEmpty()) {{
                        map.fitBounds(bounds, {{
                            padding: 50,
                            maxZoom: 14
                        }});
                    }}
                    
                    console.log('Route displayed successfully');
                    
                }} catch (error) {{
                    console.error('Error displaying route:', error);
                    throw error;
                }}
            ");
            
            _isRouteDisplayed = true;
            UpdateAnimationButtonStates();
        }

        private async Task ClearRouteFromMap()
        {
            await MapWebView.CoreWebView2.ExecuteScriptAsync(@"
                try {
                    // Clear driver animation first
                    if (window.clearDriverAnimation) {
                        window.clearDriverAnimation();
                    }
                    
                    if (map.getLayer('route-layer')) {
                        map.removeLayer('route-layer');
                    }
                    if (map.getSource('route-source')) {
                        map.removeSource('route-source');
                    }
                    
                    // Clear route data
                    window.currentRoute = null;
                    
                    console.log('Route cleared successfully');
                } catch (error) {
                    console.error('Error clearing route:', error);
                }
            ");
            
            _isRouteDisplayed = false;
            _isAnimationRunning = false;
            UpdateAnimationButtonStates();
        }

        private string ProcessRouteInput(string input)
        {
            try
            {
                // First, try to parse as JSON to see if it's already valid GeoJSON
                using var document = JsonDocument.Parse(input);
                var root = document.RootElement;
                
                // Check if it's a valid GeoJSON structure
                if (root.TryGetProperty("type", out var typeElement))
                {
                    var geoJsonType = typeElement.GetString();
                    
                    // Handle different GeoJSON types
                    if (geoJsonType == "Feature" || geoJsonType == "FeatureCollection")
                    {
                        return input; // Already valid GeoJSON
                    }
                    else if (geoJsonType == "LineString" || geoJsonType == "MultiLineString")
                    {
                        // Wrap geometry in a Feature
                        return $@"{{
                            ""type"": ""Feature"",
                            ""properties"": {{}},
                            ""geometry"": {input}
                        }}";
                    }
                }
                
                // If we get here, it might be polyline6 or other format
                throw new JsonException("Not a recognized GeoJSON format");
            }
            catch (JsonException)
            {
                // Try to decode as polyline6
                try
                {
                    var coordinates = DecodePolyline6(input.Trim());
                    return $@"{{
                        ""type"": ""Feature"",
                        ""properties"": {{}},
                        ""geometry"": {{
                            ""type"": ""LineString"",
                            ""coordinates"": {coordinates}
                        }}
                    }}";
                }
                catch
                {
                    throw new ArgumentException("Input is not valid GeoJSON LineString, MultiLineString, or polyline6 format");
                }
            }
        }

        private string DecodePolyline6(string encoded)
        {
            var coordinates = new List<double[]>();
            int index = 0;
            int lat = 0, lng = 0;

            while (index < encoded.Length)
            {
                int shift = 0, result = 0;
                int b;
                do
                {
                    b = encoded[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                
                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                shift = 0;
                result = 0;
                do
                {
                    b = encoded[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                
                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lng += dlng;

                coordinates.Add(new double[] { lng / 1000000.0, lat / 1000000.0 });
            }

            var coordsJson = "[" + string.Join(",", coordinates.Select(c => $"[{c[0]},{c[1]}]")) + "]";
            return coordsJson;
        }




        private Task LoadMapInWebView(string accessToken, string styleUrl)
        {
            var html = GenerateMapHtml(accessToken, styleUrl);
            MapWebView.NavigateToString(html);
            
            return Task.CompletedTask;
        }


        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {            
            try
            {
                string messageText = "";
                
                // Try to get the message safely
                try
                {
                    messageText = e.TryGetWebMessageAsString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting message as string: {ex.Message}");
                    
                    // Try to get it as JSON instead
                    try
                    {
                        messageText = e.WebMessageAsJson;
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"Error getting message as JSON: {ex2.Message}");
                        // If both methods fail, just return without processing
                        return;
                    }
                }
                
                // If we still don't have a message, return
                if (string.IsNullOrEmpty(messageText))
                {
                    Console.WriteLine("Received empty message from WebView");
                    return;
                }
                
                // Skip processing if it's not a valid JSON message
                if (!messageText.StartsWith("{") || !messageText.EndsWith("}"))
                {
                    Console.WriteLine($"Ignoring non-JSON message: {messageText}");
                    return;
                }
                
                using var document = JsonDocument.Parse(messageText);
                var root = document.RootElement;
                
                if (root.TryGetProperty("type", out var typeElement))
                {
                    var messageType = typeElement.GetString();
                    
                    // Handle console messages
                    if (messageType == "console" &&
                        root.TryGetProperty("level", out var levelElement) &&
                        root.TryGetProperty("message", out var msgElement))
                    {
                        var level = levelElement.GetString()?.ToUpper() ?? "LOG";
                        var message = msgElement.GetString() ?? "";
                        Console.WriteLine($"[WebView {level}] {message}");
                        return;
                    }
                }
                
                Console.WriteLine($"Unhandled message: {messageText}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in OnWebMessageReceived: {ex.Message}");
                
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error processing message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }


        private string GenerateMapHtml(string accessToken, string styleUrl)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Mapbox GL JS Map</title>
    <meta name='viewport' content='initial-scale=1,maximum-scale=1,user-scalable=no'>
    <script src='https://api.mapbox.com/mapbox-gl-js/v3.0.1/mapbox-gl.js'></script>
    <link href='https://api.mapbox.com/mapbox-gl-js/v3.0.1/mapbox-gl.css' rel='stylesheet' />
    <style>
        body {{ margin: 0; padding: 0; }}
        #map {{ position: absolute; top: 0; bottom: 0; width: 100%; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        // Set Mapbox access token
        mapboxgl.accessToken = '{accessToken}';
        
        // Initialize Mapbox GL JS map
        const map = new mapboxgl.Map({{
            container: 'map',
            style: '{styleUrl}',
            center: [0, 0],
            zoom: 2
        }});
        
        map.on('load', function() {{
            console.log('Mapbox GL JS map loaded successfully');
        }});
        
        map.on('error', function(e) {{
            console.error('Map error:', e);
        }});
        
        // Driver Animation System
        {GetDriverAnimationScript()}
    </script>
</body>
</html>";
        }








        // Driver Animation Event Handlers
        private async void StartAnimationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRouteDisplayed)
            {
                try
                {
                    await StartDriverAnimation();
                    _isAnimationRunning = true;
                    UpdateAnimationButtonStates();
                    StatusTextBlock.Text = "Status: Driver animation running";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void PauseAnimationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await PauseDriverAnimation();
                _isAnimationRunning = false;
                UpdateAnimationButtonStates();
                StatusTextBlock.Text = "Status: Driver animation paused";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pausing animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ResetAnimationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ResetDriverAnimation();
                _isAnimationRunning = false;
                UpdateAnimationButtonStates();
                StatusTextBlock.Text = "Status: Driver animation reset";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedLabel != null)
            {
                SpeedLabel.Text = $"{e.NewValue:F1}x";
                
                // Only set speed if we have a route displayed and the map is loaded
                if (_isRouteDisplayed && MapWebView?.CoreWebView2 != null)
                {
                    try
                    {
                        await SetAnimationSpeed(e.NewValue);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error setting animation speed: {ex.Message}");
                    }
                }
            }
        }

        // Driver Animation Control Methods
        private async Task StartDriverAnimation()
        {
            try
            {
                var result = await MapWebView.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        if (window.startDriverAnimation) {
                            return window.startDriverAnimation();
                        }
                        return false;
                    })();
                ");
                Console.WriteLine($"Start animation result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting animation: {ex.Message}");
                throw;
            }
        }

        private async Task PauseDriverAnimation()
        {
            try
            {
                var result = await MapWebView.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        if (window.pauseDriverAnimation) {
                            return window.pauseDriverAnimation();
                        }
                        return false;
                    })();
                ");
                Console.WriteLine($"Pause animation result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pausing animation: {ex.Message}");
                throw;
            }
        }

        private async Task ResetDriverAnimation()
        {
            try
            {
                var result = await MapWebView.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        if (window.resetDriverAnimation) {
                            return window.resetDriverAnimation();
                        }
                        return false;
                    })();
                ");
                Console.WriteLine($"Reset animation result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting animation: {ex.Message}");
                throw;
            }
        }

        private async Task SetAnimationSpeed(double speed)
        {
            try
            {
                var result = await MapWebView.CoreWebView2.ExecuteScriptAsync($@"
                    (function() {{
                        if (window.setAnimationSpeed) {{
                            return window.setAnimationSpeed({speed:F1});
                        }}
                        return false;
                    }})();
                ");
                Console.WriteLine($"Set animation speed result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting animation speed: {ex.Message}");
                // Don't throw for speed changes - it's not critical
            }
        }

        private void UpdateAnimationButtonStates()
        {
            if (StartAnimationButton == null || PauseAnimationButton == null || ResetAnimationButton == null)
                return;

            StartAnimationButton.IsEnabled = _isRouteDisplayed && !_isAnimationRunning;
            PauseAnimationButton.IsEnabled = _isRouteDisplayed && _isAnimationRunning;
            ResetAnimationButton.IsEnabled = _isRouteDisplayed;
        }

        private string GetDriverAnimationScript()
        {
            return @"
        // Driver Animation Variables
        let driverAnimation = {
            marker: null,
            routeCoordinates: [],
            currentPosition: 0,
            animationId: null,
            isRunning: false,
            speed: 1.0,
            lastTime: 0,
            totalDistance: 0,
            distances: []
        };

        // Calculate distance between two points (Haversine formula)
        function calculateDistance(coord1, coord2) {
            const R = 6371000; // Earth radius in meters
            const lat1 = coord1[1] * Math.PI / 180;
            const lat2 = coord2[1] * Math.PI / 180;
            const deltaLat = (coord2[1] - coord1[1]) * Math.PI / 180;
            const deltaLng = (coord2[0] - coord1[0]) * Math.PI / 180;

            const a = Math.sin(deltaLat/2) * Math.sin(deltaLat/2) +
                    Math.cos(lat1) * Math.cos(lat2) *
                    Math.sin(deltaLng/2) * Math.sin(deltaLng/2);
            const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));

            return R * c;
        }

        // Extract coordinates from route data
        function extractRouteCoordinates(routeData) {
            if (!routeData) return [];
            
            let coords = [];
            if (routeData.type === 'Feature') {
                if (routeData.geometry.type === 'LineString') {
                    coords = routeData.geometry.coordinates;
                } else if (routeData.geometry.type === 'MultiLineString') {
                    coords = routeData.geometry.coordinates.flat();
                }
            } else if (routeData.type === 'FeatureCollection') {
                routeData.features.forEach(feature => {
                    if (feature.geometry.type === 'LineString') {
                        coords = coords.concat(feature.geometry.coordinates);
                    } else if (feature.geometry.type === 'MultiLineString') {
                        coords = coords.concat(feature.geometry.coordinates.flat());
                    }
                });
            } else if (routeData.coordinates) {
                coords = routeData.coordinates;
            }
            
            return coords;
        }

        // Calculate cumulative distances along the route
        function calculateRouteDistances(coordinates) {
            const distances = [0];
            let totalDistance = 0;
            
            for (let i = 1; i < coordinates.length; i++) {
                const distance = calculateDistance(coordinates[i-1], coordinates[i]);
                totalDistance += distance;
                distances.push(totalDistance);
            }
            
            return { distances, totalDistance };
        }

        // Interpolate position along route based on distance
        function interpolatePosition(targetDistance, coordinates, distances) {
            if (targetDistance <= 0) return coordinates[0];
            if (targetDistance >= distances[distances.length - 1]) return coordinates[coordinates.length - 1];
            
            // Find the segment containing the target distance
            let segmentIndex = 0;
            for (let i = 1; i < distances.length; i++) {
                if (distances[i] >= targetDistance) {
                    segmentIndex = i - 1;
                    break;
                }
            }
            
            // Interpolate within the segment
            const segmentStart = distances[segmentIndex];
            const segmentEnd = distances[segmentIndex + 1];
            const segmentProgress = (targetDistance - segmentStart) / (segmentEnd - segmentStart);
            
            const startCoord = coordinates[segmentIndex];
            const endCoord = coordinates[segmentIndex + 1];
            
            return [
                startCoord[0] + (endCoord[0] - startCoord[0]) * segmentProgress,
                startCoord[1] + (endCoord[1] - startCoord[1]) * segmentProgress
            ];
        }

        // Create driver marker
        function createDriverMarker() {
            console.log('Creating driver marker...');
            
            // Create a custom div element for the marker
            const markerElement = document.createElement('div');
            markerElement.style.width = '24px';
            markerElement.style.height = '24px';
            markerElement.style.backgroundColor = '#ff0000';
            markerElement.style.borderRadius = '50%';
            markerElement.style.border = '4px solid #ffffff';
            markerElement.style.boxShadow = '0 3px 6px rgba(0,0,0,0.5)';
            markerElement.style.cursor = 'pointer';
            markerElement.style.zIndex = '1000';
            markerElement.style.position = 'relative';
            
            // Add a small arrow/direction indicator
            const arrow = document.createElement('div');
            arrow.style.width = '0';
            arrow.style.height = '0';
            arrow.style.borderLeft = '4px solid transparent';
            arrow.style.borderRight = '4px solid transparent';
            arrow.style.borderBottom = '8px solid #ffffff';
            arrow.style.position = 'absolute';
            arrow.style.top = '50%';
            arrow.style.left = '50%';
            arrow.style.transform = 'translate(-50%, -50%)';
            markerElement.appendChild(arrow);
            
            const marker = new mapboxgl.Marker(markerElement);
            console.log('Driver marker created successfully:', marker);
            
            return marker;
        }

        // Initialize driver animation system
        window.initializeDriverAnimation = function() {
            console.log('=== Initializing driver animation system ===');
            
            if (!window.currentRoute) {
                console.error('No route data available for animation');
                return false;
            }
            
            console.log('Current route data:', window.currentRoute);
            
            // Extract route coordinates
            driverAnimation.routeCoordinates = extractRouteCoordinates(window.currentRoute);
            console.log('Route coordinates extracted:', driverAnimation.routeCoordinates.length, 'points');
            console.log('First coordinate:', driverAnimation.routeCoordinates[0]);
            console.log('Last coordinate:', driverAnimation.routeCoordinates[driverAnimation.routeCoordinates.length - 1]);
            
            if (driverAnimation.routeCoordinates.length < 2) {
                console.error('Route must have at least 2 points for animation');
                return false;
            }
            
            // Calculate distances
            const result = calculateRouteDistances(driverAnimation.routeCoordinates);
            driverAnimation.distances = result.distances;
            driverAnimation.totalDistance = result.totalDistance;
            console.log('Route total distance:', driverAnimation.totalDistance.toFixed(0), 'meters');
            console.log('Distance array length:', driverAnimation.distances.length);
            
            // Create driver marker
            if (driverAnimation.marker) {
                console.log('Removing existing marker');
                driverAnimation.marker.remove();
            }
            
            driverAnimation.marker = createDriverMarker();
            const startCoord = driverAnimation.routeCoordinates[0];
            console.log('Setting marker at start position:', startCoord);
            
            try {
                driverAnimation.marker.setLngLat(startCoord).addTo(map);
                console.log('Marker added to map successfully');
            } catch (error) {
                console.error('Error adding marker to map:', error);
                return false;
            }
            
            // Reset animation state
            driverAnimation.currentPosition = 0;
            driverAnimation.isRunning = false;
            driverAnimation.lastTime = 0;
            driverAnimation.speed = 1.0;
            
            console.log('Driver animation system initialized successfully');
            console.log('=== Initialization complete ===');
            return true;
        };

        // Animation loop
        function animateDriver(timestamp) {
            if (!driverAnimation.isRunning || !driverAnimation.marker) {
                console.log('Animation stopped: running =', driverAnimation.isRunning, 'marker exists =', !!driverAnimation.marker);
                return;
            }
            
            const deltaTime = driverAnimation.lastTime ? timestamp - driverAnimation.lastTime : 16; // Default to 16ms if no previous time
            driverAnimation.lastTime = timestamp;
            
            // Move based on speed (meters per second, adjusted by speed multiplier)
            const baseSpeed = 50; // Increased base speed: 50 meters per second (~180 km/h for more visible movement)
            const moveDistance = (baseSpeed * driverAnimation.speed * deltaTime) / 1000;
            driverAnimation.currentPosition += moveDistance;
            
            console.log(`Animation frame: position=${driverAnimation.currentPosition.toFixed(1)}m / ${driverAnimation.totalDistance.toFixed(1)}m, speed=${driverAnimation.speed}x`);
            
            // Check if we've reached the end
            if (driverAnimation.currentPosition >= driverAnimation.totalDistance) {
                driverAnimation.currentPosition = driverAnimation.totalDistance;
                driverAnimation.isRunning = false;
                console.log('Animation completed - reached end of route');
                
                // Position at the end of the route
                const endCoord = driverAnimation.routeCoordinates[driverAnimation.routeCoordinates.length - 1];
                driverAnimation.marker.setLngLat(endCoord);
                return;
            }
            
            // Calculate current position
            const currentCoord = interpolatePosition(
                driverAnimation.currentPosition,
                driverAnimation.routeCoordinates,
                driverAnimation.distances
            );
            
            console.log(`Moving marker to: [${currentCoord[0].toFixed(6)}, ${currentCoord[1].toFixed(6)}]`);
            
            // Update marker position
            driverAnimation.marker.setLngLat(currentCoord);
            
            // Continue animation
            if (driverAnimation.isRunning) {
                driverAnimation.animationId = requestAnimationFrame(animateDriver);
            } else {
                console.log('Animation stopped in main loop');
            }
        }

        // Start animation
        window.startDriverAnimation = function() {
            console.log('=== Starting driver animation ===');
            console.log('Marker exists:', !!driverAnimation.marker);
            console.log('Route coordinates count:', driverAnimation.routeCoordinates.length);
            console.log('Total distance:', driverAnimation.totalDistance);
            console.log('Current position:', driverAnimation.currentPosition);
            console.log('Animation speed:', driverAnimation.speed);
            
            if (!driverAnimation.marker) {
                console.error('Cannot start animation: no marker');
                return false;
            }
            
            if (driverAnimation.routeCoordinates.length < 2) {
                console.error('Cannot start animation: not enough route points');
                return false;
            }
            
            if (driverAnimation.totalDistance <= 0) {
                console.error('Cannot start animation: invalid route distance');
                return false;
            }
            
            // Stop any existing animation
            if (driverAnimation.animationId) {
                cancelAnimationFrame(driverAnimation.animationId);
                driverAnimation.animationId = null;
            }
            
            // Start animation
            driverAnimation.isRunning = true;
            driverAnimation.lastTime = 0;
            driverAnimation.animationId = requestAnimationFrame(animateDriver);
            
            console.log('Animation started with ID:', driverAnimation.animationId);
            console.log('=== Animation start complete ===');
            return true;
        };

        // Pause animation
        window.pauseDriverAnimation = function() {
            console.log('Pausing driver animation');
            driverAnimation.isRunning = false;
            if (driverAnimation.animationId) {
                cancelAnimationFrame(driverAnimation.animationId);
                driverAnimation.animationId = null;
            }
            return true;
        };

        // Reset animation
        window.resetDriverAnimation = function() {
            console.log('Resetting driver animation');
            window.pauseDriverAnimation();
            driverAnimation.currentPosition = 0;
            driverAnimation.lastTime = 0;
            if (driverAnimation.marker && driverAnimation.routeCoordinates.length > 0) {
                driverAnimation.marker.setLngLat(driverAnimation.routeCoordinates[0]);
                return true;
            }
            return false;
        };

        // Set animation speed
        window.setAnimationSpeed = function(speed) {
            console.log('Setting animation speed to:', speed);
            driverAnimation.speed = Math.max(0.1, Math.min(5.0, speed)); // Clamp between 0.1 and 5.0
            return true;
        };

        // Clear animation
        window.clearDriverAnimation = function() {
            console.log('Clearing driver animation');
            window.pauseDriverAnimation();
            if (driverAnimation.marker) {
                driverAnimation.marker.remove();
                driverAnimation.marker = null;
            }
            driverAnimation.routeCoordinates = [];
            driverAnimation.currentPosition = 0;
            driverAnimation.totalDistance = 0;
            driverAnimation.distances = [];
        };";
        }

    }
}