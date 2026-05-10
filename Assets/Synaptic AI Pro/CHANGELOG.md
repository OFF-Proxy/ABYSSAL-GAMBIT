# Changelog

All notable changes to Synaptic AI Pro for Unity will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.19] - 2026-04-23

### Fixed
- **Windows HTTP WebSocket Stability**: Fixed HTTP server tab becoming unresponsive on Windows (contributed by OverlordMethuselah777)
  - Added reentrancy guard to prevent concurrent Connect() calls
  - Connect timeout (5 seconds) prevents indefinite hang
  - Background tasks properly tracked and awaited on disconnect

- **MCP Reconnect Storm Prevention**: Fixed reconnect phase machine firing unlimited background tasks
  - Added MIN_RECONNECT_INTERVAL (10s) hard gate between reconnect attempts
  - All fire-and-forget tasks now tracked with fault logging
  - Connect timeout (5s) added to MCP WebSocket handshake

- **Setup Window UI Freeze**: Port check moved to background thread (contributed by OverlordMethuselah777)
  - Port check throttled to every 2 seconds with cached result

- **Main Window Repaint Flood**: Added ThrottledRepaint() at 10Hz cap (contributed by OverlordMethuselah777)

### Added
- **MCP Server Start/Stop Menu**: Tools > Synaptic Pro > MCP Server: Start/Stop (contributed by OverlordMethuselah777)
  - enableMCP master switch, persisted via EditorPrefs

### Changed
- **Assembly Definition**: Fixed VFX Graph versionDefines expression

---

## [1.2.11] - 2026-04-08

### Changed
- **HTTP Server Externalized**: HTTP Server now runs as external Node.js process (`http-server.js`)
  - Resolves domain reload and Play Mode stability issues
  - HTTP and WebSocket on same port (default: 8086)
  - Unity connects via WebSocket automatically
  - Project-specific port settings (each Unity project can use different port)

### Added
- **http-server.js**: New standalone HTTP server
  - All endpoints: `/`, `/health`, `/tools`, `/categories`, `/tools/search`, `/execute`, `/batch`, `/resources`
  - Same port for HTTP and WebSocket (no port conflict with MCP)
  - Auto-reconnect on connection loss

### Removed
- **Internal HTTP Server (C#)**: Removed `NexusHTTPServer.cs` - replaced by external Node.js server

---

## [1.2.10] - 2026-04-06

### Fixed
- **MCP Dual-Process Startup Bug**: Fixed critical issue where Claude Code/VSCode starts two MCP processes simultaneously
  - Root cause: When extension starts MCP, PID-A listens successfully, PID-B gets EADDRINUSE
  - Silent `uncaughtException` handler swallowed error, PID-A dies (stdin closed), PID-B survives but never listened
  - Result: MCP stdio works but WebSocket connection fails
  - Solution: Added `startServerWithRetry()` with 5 retry attempts and `requestShutdownFromPriorProcess()` WebSocket handover
  - Added shutdown message handler for graceful process takeover
  - Fixed in both `index.js` and `index-supersave.js`

### Added
- **MCP Resources**: New `resources/list` and `resources/read` protocol support
  - `synaptic://tools/reference` - Compact tools reference (~31KB)
  - `synaptic://tools/reference/full` - Full markdown reference with inputSchema (~103KB)
  - Enables prompt caching for reduced token usage

- **HTTP Resources Endpoints**: New endpoints for tools reference
  - `GET /resources` - List available resources
  - `GET /resources/read?uri=...` - Read resource by URI
  - Root `/` endpoint now includes full tools reference with inputSchema

---

## [1.2.9] - 2026-04-03

### Added
- **Export Package Tool**: New `unity_export_package` tool to create .unitypackage files
  - Export multiple asset paths at once (comma-separated)
  - Optional dependency inclusion
  - Custom output path or auto-generated with timestamp

---

## [1.2.8] - 2026-04-03

### Added
- **Tool Search Endpoint**: New `GET /tools/search` endpoint for keyword-based tool discovery
  - Search by keyword in tool name, title, and description
  - Optional category filter: `?q=material&category=Material`
  - Configurable result limit: `?q=camera&limit=10`
  - Relevance scoring for better results ranking
  - Example: `curl "http://localhost:8086/tools/search?q=material&limit=5"`

- **MCP Search Tool**: New `search_tools` meta-tool for MCP server
  - Search tools without knowing exact category
  - Parameters: `query`, `category` (optional), `limit` (optional)
  - Returns ranked results with score

- **Console Log Filtering**: New filtering options for `unity_analyze_console_logs` to reduce token usage
  - `excludeSynaptic` (default: true): Auto-exclude internal Synaptic logs ([Synaptic], [NexusConsole], etc.)
  - `filter`: Include only logs containing specified text
  - `exclude`: Exclude logs matching patterns (comma-separated)
  - `groupByMessage`: Group duplicate messages with count instead of repeating
  - Reduces token usage significantly (1,500-2,000 → 50-100 tokens for typical queries)

### Improved
- **HTTP Server Port Recovery**: Now forcefully kills any process blocking the port on startup
  - No more "port already in use" errors after domain reload
  - Works on Windows (netstat/taskkill) and macOS/Linux (lsof/kill)
  - Reduced retry count (5→3) and delay (2s→500ms) for faster startup

### Fixed
- **MeshRenderer Material Assignment**: Fixed JSON serialization error when setting material via `unity_update_component`
  - Root cause: `Color.linear` property caused circular reference during serialization
  - Added `ConvertValueForSerialization` handlers for Material, Texture, and UnityEngine.Object types
  - Now returns safe serializable object with `name`, `shader`, `assetPath`, `instanceId`

---

## [1.2.7] - 2026-03-27

### Fixed
- **Windows Domain Reload Recovery**: Fixed critical issue where HTTP server could not restart after script recompilation on Windows
  - Root cause: Port remained occupied during domain reload due to HttpListener not being properly released
  - Added `ForceReleasePort()` method called before assembly reload
  - Direct Abort/Stop/Close on HttpListener object for immediate port release
  - Added `GC.Collect()` and `GC.WaitForPendingFinalizers()` to ensure port is released
  - Replaced `Thread.Abort()` (deprecated in .NET Core) with `Thread.Join(500)` for graceful thread termination
  - 100ms sleep after cleanup to ensure OS releases the port
  - Resolves "Failed to start HTTP server" error that previously required Unity restart

- **HTTP Server Retry Settings**: Increased retry attempts for better Windows compatibility
  - Auto-start retries: 5 → 15 attempts
  - Auto-start retry delay: 0.5s → 1.0s
  - Manual start retries: 3 → 5 attempts
  - Manual start retry delay: 500ms → 1000ms
  - Provides more time for Windows to release ports after domain reload

---

## [1.2.6] - 2026-03-23

### Fixed
- **HTTP Server Port Stability**: Improved port release and binding reliability
  - Added `Abort()` call on Stop for immediate port release
  - Added retry logic on Start to handle TIME_WAIT state (common on Windows)
  - Up to 5 retries with 500ms delay for robust port binding
  - Prevents "port already in use" errors after rapid restart cycles

---

## [1.2.5] - 2026-02-27

### Added
- **HTTP Prompt Endpoint**: New `GET /prompt` endpoint to fetch AI control instructions directly
  - No more manual copy-paste from Setup Window
  - Returns the full AI control prompt with endpoint documentation
  - Useful for automation and custom integrations

- **Test Runner Auto-Execution**: `unity_run_tests` now supports automated test execution
  - `operation="run"` - Start test execution (EditMode or PlayMode)
  - `operation="results"` - Get test progress and results
  - `operation="list"` - List available tests
  - Uses Unity TestRunnerApi with ICallbacks for reliable async execution
  - Returns detailed results: passed/failed/skipped counts, duration, error messages

### Fixed
- **Windows Claude Desktop Auto-Setup**: Fixed one-click setup not detecting Claude Desktop config on Windows
  - Added support for Microsoft Store version (`%LOCALAPPDATA%\Packages\Claude_*`)
  - Added multiple path candidates (`%APPDATA%\Claude`, `%LOCALAPPDATA%\Claude`)
  - Improved detection logic to find existing config files first
  - Added detailed debug logging for troubleshooting
- **VFX Graph Tools**: Fixed critical reflection issues in VFX Graph manipulation tools
  - `unity_vfx_add_context`: Fixed "Index out of range" error when adding contexts
  - `unity_vfx_add_parameter`: Fixed "Index out of range" error when adding parameters
  - `unity_vfx_add_block`: Fixed "AddChild failed" error when adding blocks
  - `unity_vfx_set_attribute`: Fixed "Ambiguous match found" error when setting attributes
  - Improved compatibility with Unity 2022.3 LTS and newer VFX Graph versions
  - Added child count verification to detect successful adds despite internal exceptions

- **HTTP Server Play Mode Stability**: Fixed server becoming unresponsive after Play mode transitions
  - Added `playModeStateChanged` handler to restart server during Play mode changes
  - Server now cleanly stops before entering/exiting Play mode and restarts after
  - Prevents port occupation issues that caused "server not responding" errors

- **Animator Controller Editor Refresh**: Fixed Animator window not updating after script-based changes
  - Added `AssetDatabase.ImportAsset(..., ForceUpdate)` after controller modifications
  - Animator window now immediately reflects changes from AI tools

- **MCP Connection Messages**: Improved connection error messages
  - Reduced console log noise (only logs 1st and every 5th attempt)
  - Clearer message when MCP server is not running
  - Suggests starting Claude Desktop/Cursor and using AI Reconnect

## [1.2.4] - 2026-02-03

### Added
- **Dynamic Meta-Tools**: New universal tools for inspecting and modifying any Unity component
  - `unity_dynamic_inspect`: Inspect GameObjects, components, scene, hierarchy, prefabs, project
    - Discovers all serialized properties with types and current values
    - Supports depth-limited hierarchy traversal
    - Wildcard search for prefabs (e.g., `Assets/**/*.prefab`)
  - `unity_dynamic_modify`: Modify any component property using property paths
    - Supports nested properties (e.g., `m_Lens.FieldOfView`)
    - Auto-creates components with `createIfMissing` option
    - Handles Vector2/3/4, Color, Enum, and all basic types
  - `unity_dynamic_create`: Universal creation tool
    - GameObjects (empty or primitives)
    - Prefab instantiation from any asset path
    - Scene loading (single or additive)
    - Component addition to existing objects
- **SuperSave Mode Meta-Tools**: Same functionality available as `inspect`, `modify`, `create` in token-saving mode

### Fixed
- **HTTP Server Port Cleanup**: Fixed port remaining occupied after script recompilation
  - Added `AssemblyReloadEvents.beforeAssemblyReload` handler to stop server before domain reload
  - Added `EditorApplication.quitting` handler to stop server on editor quit
  - Prevents "port already in use" errors after script changes

- **Script Creation Path**: `unity_create_script` now supports custom path parameter
  - Previously always created scripts in `Assets/Synaptic_Generated/`
  - Now accepts `path` parameter (e.g., `Assets/Scripts/Player/`)
  - AI can organize scripts in proper project folders

- **Node.js Path Detection for Windows**: Fixed MCP server not starting on Windows when Node.js is installed on D: drive or non-standard locations
  - Added `FindNodePath()` function to detect Node.js across multiple installation paths
  - Supports nvm-windows, Volta, fnm, and manual installations
  - Searches D:, E: drives for users who install on secondary drives
  - Falls back to PATH if not found in common locations
  - Config now writes full Node.js path instead of just "node"

- **Prefab/Asset Contamination in Scene Analysis**: Fixed critical bug where prefabs loaded in memory were incorrectly included in scene analysis
  - `unity_analyze_draw_calls` was reporting objects from prefabs as if they existed in the scene
  - `unity_search_objects` and `unity_cleanup_empty_objects` had the same issue
  - Added `EditorUtility.IsPersistent()` check to exclude non-scene objects
  - Now only returns objects that actually exist in loaded scenes

---

## [1.2.3] - 2026-01-24

### Fixed
- **HTTP API Tool Mapping**: Fixed unmapped tools returning "Unknown operation" error
  - Tools without explicit mapping now correctly strip `unity_` prefix
  - `/execute`, `/batch`, `/tool/:name` endpoints all work with any tool
  - Example: `unity_get_scene_summary` → `GET_SCENE_SUMMARY`

---

## [1.2.2] - 2026-01-22

### Fixed
- **Token SuperSave Mode**: Fixed execute tool not sending commands to Unity
  - Command name now uses lowercase to match Unity's `ConvertCommandToOperationType`
  - Fixed PORT environment variable (was MCP_PORT)

- **MCP Config Generation**: All config generators now use selected server mode
  - Fixed `GenerateGeminiConfig()` - was hardcoded to index.js
  - Fixed `GenerateWindsurfConfig()` - was hardcoded to index.js
  - Fixed `GenerateClaudeCodeSpecificConfig()` - was hardcoded to index.js
  - Now correctly uses `GetServerScriptPath()` for all clients

- **GetGameObjectsList**: Added parameter aliases for more intuitive usage
  - `tag` → `tagFilter`
  - `layer` → `layerFilter`
  - `name` → `nameFilter`

- **GetComponentDetails**: Now returns useful info for all component types
  - Previously returned `null` for unhandled component types
  - Now returns basic serializable properties (up to 10)
  - Falls back to `{ type_name: "ComponentName" }` if no properties available

---

## [1.2.1] - 2026-01-20

### Fixed
- **Token SuperSave Mode**: Added missing shutdown handlers to `index-supersave.js`
  - SIGINT/SIGTERM signal handling
  - stdin close detection for proper MCP client cleanup
  - Graceful WebSocket and server shutdown

---

## [1.2.0] - 2026-01-19

### Added - Token SuperSave Mode ★
- **New MCP Server Mode**: 99% context reduction with only 3 meta-tools
  - `list_categories()` - Discover available tool categories
  - `list_tools(category)` - See tools & parameters in a category
  - `execute(tool, params)` - Run any of 350+ tools by name
  - Works with ALL MCP clients (no dynamic tool loading required)
  - Best for long AI sessions - more context for conversation
  - Now set as **Recommended** and default in Setup Window

- **Changelog Dialog**: Shows what's new on startup/import
  - "Don't show on startup" toggle
  - Manual access: Tools → Synaptic Pro → What's New
  - Version-aware: Only shows once per version

### Changed
- **Setup Window Redesigned**:
  - Token SuperSave Mode at top with ★ Recommended label
  - Green highlight for recommended option
  - Updated tool counts (350+ tools)
  - Clearer mode descriptions

### Improved
- **Error Messages**: Better debugging experience
  - "Did you mean...?" suggestions for unknown tools
  - Detailed troubleshooting for connection errors
  - Timeout error explanations
  - Tool info included in error context

- **Tool Registry**: Dynamic loading from tool-registry.json
  - No more hardcoded tool definitions in supersave mode
  - Easier maintenance and updates

---

## [1.1.9] - 2026-01-15

### Fixed
- **Batch Tool (`unity_execute_batch`)**: Fixed batch execution not working
  - Added missing `EXECUTE_BATCH` operation mapping in MCP service
  - HTTP Server now auto-converts `operations` format to Unity's `tasks` format
  - Both formats now work: `operations` (user-friendly) and `tasks` (native)

- **MCP stdio Protocol**: Changed `console.log` to `console.error` in index.js
  - Prevents log messages from corrupting JSON-RPC communication
  - Fixes "Unexpected token" JSON parse errors for some users

---

## [1.1.8] - 2025-01-15

### Added
- **Sphere Skybox Mode**: New `sphere`/`dome`/`landscape` type for `unity_create_skybox_from_image`
  - Creates inverted sphere mesh with texture on inside
  - Perfect for regular landscape photos (non-360°)
  - Parameters: `radius`, `followCamera`, `objectName`
  - `SkySphereCameraFollow` component keeps sphere centered on camera
  - Best for wide panoramic shots and scenic backgrounds

### Fixed
- **Shader PackageRequirements**: Added `PackageRequirements` tags to URP SubShaders
  - SynapticWater.shader: URP SubShader now skipped when URP package not installed
  - SynapticCaustics.shader: Same fix applied
  - Resolves shader compilation errors for users without URP
  - Built-in fallback SubShader works correctly on all pipelines

### Changed
- **MCP Server Name**: Changed from `unity` to `unity-synaptic` across all configurations
  - Prevents conflicts with other Unity MCP integrations
  - Updated: Claude Desktop, Claude Code, Windsurf, Gemini CLI, Codex CLI, Cline
  - TOML configs use quoted key: `[mcp_servers."unity-synaptic"]`
  - Permissions updated: `mcp__unity-synaptic`
  - Mentions updated: `@unity-synaptic`

### Improved
- **HTTP Server Auto-Start**: Server automatically restarts after Unity recompilation
  - New "Auto-Start on Load" toggle in HTTP Server tab
  - Settings persist via EditorPrefs
  - Similar to MCP Auto-Reconnect feature

- **HTTP Server Tool Registry**: Now loads all tools from tool-registry.json
  - Previously only 13 hardcoded tools available
  - Now exposes all 351 tools via HTTP API
  - `/tools` endpoint shows full tool catalog

---

## [1.1.7] - 2025-01-13

### Added
- **HTTP Server Tab**: Direct HTTP API access to Unity tools
  - New "HTTP Server" tab in Synaptic Setup window
  - Start/Stop HTTP server with one click
  - Default port: 8086 (configurable)
  - Endpoints: `/health`, `/tools`, `/scene`, `/tool/{name}`
  - CORS support, API docs in UI, cURL examples

- **NexusHTTPServer.cs**: Runtime HTTP server component
  - HttpListener-based, thread-safe request handling

- **unity_instantiate_prefab Tool**: Place prefabs/FBX assets from project into scene
  - Supports any asset path (e.g., `Assets/Models/Chair.fbx`, `Assets/Prefabs/Player.prefab`)
  - Optional parameters: name, position, rotation, scale, parent
  - Works with .prefab, .fbx, and other importable 3D formats
  - Resolves user request for placing existing project assets via CLI

- **execute_menu_item Tool**: Execute Unity menu items via MCP
  - Trigger any Unity Editor menu command (e.g., "File/Save", "Edit/Play")
  - Enables automation of Editor workflows

- **InstanceID Support**: GameObject lookup by InstanceID
  - `FindGameObjectByNameOrId()` helper method
  - Accepts both name strings and integer InstanceIDs
  - More reliable object identification in complex scenes

- **Start MCP Server Menu**: Tools/Synaptic Pro/Start MCP Server
  - Handles paths with spaces correctly
  - Port-in-use check prevents duplicate server instances
  - Cross-platform support (Windows/macOS)

### Improved
- **MCP Auto-Retry System**: Enhanced for long compilations
  - Increased from 10 to 30 retries
  - Total wait time: up to 5 minutes (was 2.5 min)
  - Applies to: index.js, index-essential.js, hub-server.js
  - Prevents timeout errors during large project recompilation

### Changed
- **Menu Items**: Removed emojis from Auto Reconnect menus

---

## [1.1.6] - 2025-12-29

### Fixed - Build Errors
- **NexusEventMonitor.cs**: Fixed Editor-only API usage causing build failures
  - `CompilationPipeline` now wrapped in `#if UNITY_EDITOR && UNITY_2019_1_OR_NEWER`
  - `OnGUI()` method with `EditorStyles`, `EditorGUILayout` now wrapped in `#if UNITY_EDITOR`
  - Resolves: CS0103 errors for `CompilationPipeline`, `EditorApplication`, `EditorUtility`, `EditorStyles`, `EditorGUILayout`

---

## [1.1.5] - 2025-12-13

### 🚀 Major Update - 42,600+ lines added!

### Added - AI Systems
- **GOAP (Goal-Oriented Action Planning) Runtime Engine**
  - `GOAPAgent.cs` - Full agent implementation with planning & execution
  - `GOAPPlanner.cs` - A* search-based planner for action sequences
  - `GOAPActionBase.cs` - Base class for custom actions
  - `GOAPDynamicAction.cs` - Runtime-configurable actions
  - `GOAPGoal.cs` - Goal definition with priorities
  - `WorldState.cs` - State representation for planning
  - MCP Tools: CREATE_GOAP_AGENT, ADD_GOAP_ACTION, ADD_GOAP_GOAL, SET_WORLD_STATE, etc.

- **Behavior Tree Runtime**
  - `BTNode.cs` - Base node with Success/Failure/Running states
  - `BTComposites.cs` - Selector, Sequence, Parallel nodes
  - `BTDecorators.cs` - Inverter, Repeater, Succeeder, UntilFail, Cooldown
  - `BTLeaves.cs` - Wait, Log, SetBlackboard, CheckBlackboard, MoveTo, etc.
  - `BehaviorTreeRunner.cs` - Runtime executor with blackboard
  - MCP Tools: CREATE_BEHAVIOR_TREE, ADD_BT_NODE, SET_BT_BLACKBOARD, etc.

### Added - Shaders (URP/HDRP/Built-in Compatible)
- **SynapticWaterPro.shader** - Advanced ocean with Gerstner waves, foam, refraction, caustics
- **SynapticSkyPro.shader** - Procedural sky with volumetric clouds, day/night cycle
- **SynapticToonPro.shader** - Anime-style cel shading with outline, rim light
- **SynapticGrassPro.shader** - GPU-instanced grass with wind animation
- **GrassInstancer.compute** - Compute shader for grass placement
- **CloudNoise.compute** - Procedural cloud generation
- Runtime controllers: DissolveController, ShieldController, GrassRenderer

### Added - Water System
- **OceanSystem.cs** - Wave simulation and water surface management
- **Buoyancy.cs** - Physics-based floating objects
- **WaterPhysics.cs** - Water interaction and splash effects
- MCP Tools: CREATE_OCEAN_SYSTEM, ADD_BUOYANCY

### Added - VFX Graph Editing (6 New Tools)
- `unity_vfx_set_output` - Change blendMode, texture, softParticle
- `unity_vfx_set_block_value` - Modify block values (color, size, turbulence)
- `unity_vfx_set_spawn_rate` - Adjust spawn rate
- `unity_vfx_list_blocks` - List all contexts/blocks with indices
- `unity_vfx_remove_block` - Remove blocks from contexts
- `unity_vfx_get_block_info` - Get detailed block information
- New VFXBuilder methods: SetOutputSettings, SetBlockValue, SetSpawnRate, ListBlocks, RemoveBlock, GetBlockInfo

### Added - VFX Textures (150+ CC0 Kenney Textures)
- Fire/Flame: flame_01~06, fire_01~02, flare_01
- Smoke: smoke_01~10, whitePuff00~24, blackSmoke00~24
- Explosion: explosion00~08
- Sparks: spark_01~07
- Magic: magic_01~05, star_01~09, twirl_01~03
- Trails: trace_01~07
- Effects: muzzle_01~05, slash_01~04, light_01~03, circle_01~05
- CC0 License - free to use and redistribute
- `SetParticleTexture()` method for easy texture assignment

### Added - MCP Auto-Retry System
- Automatic 3 retry attempts with 3-second intervals
- Handles Unity recompilation gracefully
- Success response includes retry info when applicable
- Clear error messages after all retries exhausted

### Added - Script Safety Features
- `READ_SCRIPT` requirement before editing (like Claude Code)
- `UpdateScriptVariable` now requires prior read

### Fixed - VFX
- **Fire Preset**: Now with natural flickering
  - Random lifetime (0.8-2.0s), size (0.3-0.7), angle (0-360°)
  - Random velocity spread, angular velocity for rotation
  - Enhanced turbulence (intensity 3.0, frequency 5)
- **Color Attribute**: Uses Vector3 (RGB) instead of Vector4
- **VFXSlotFloat3 Angle**: Float → Vector3 conversion added

### Fixed - Serialization
- **Vector3 circular reference**: GET_TERRAIN_INFO, GET_LIGHTING_INFO, GET_UI_INFO, GET_PHYSICS_INFO
- Using `Vector3ToString()` helper throughout

### Fixed - MCP Tools
- `unity_update_component`: Accepts both `gameObject`/`component` and `gameObjectName`/`componentName`
- `unity_set_property`: Fixed `propertyName` → `property` mapping
- Weather system controllers now properly attached (Rain, Snow, Wind, Lightning, Thunderstorm)

### Fixed - Build Errors
- Editor-only code properly wrapped in `#if UNITY_EDITOR`
- Debug namespace ambiguity resolved
- Shader function redefinition and LerpWhiteTo errors

### Changed
- **Auto Reconnect Menu**: Shows "Enable" when OFF, "Disable" when ON
- **Tool Registry**: Regenerated with 342+ tools
- **VFX Presets**: Now use Kenney textures by default

### Technical Stats
- 93 files changed
- 42,641 insertions, 6,561 deletions
- New Runtime scripts: 15+
- New Shaders: 6
- New MCP Tools: 20+

## [1.1.4] - 2025-11-26

### Added
- **HTTP API Endpoints**: Direct Unity control from AI CLIs without MCP
  - `GET /health` - Check Unity connection status
  - `GET /tools` - List all 246 available tools with descriptions
  - `POST /tool/:toolName` - Execute any Unity tool via HTTP
  - Compatible with Claude Code, Codex CLI, Gemini CLI, and custom AI tools
  - Full tool-registry.json integration for tool discovery

### Fixed
- **WebSocket Message Handling**: Improved operationId recognition
  - NexusEditorMCPService now correctly reads operationId from parameters
  - Fixed timeout issues when calling tools via HTTP endpoints
  - Unified response format between MCP stdio and HTTP/WebSocket paths

### Changed
- Version bumped to 1.1.4 across all components
  - package.json, MCPServer/package.json
  - NexusWebSocketClient.cs, NexusEditorMCPService.cs
  - NexusSetupWindow.cs, NexusSetupManager.cs

## [1.1.3] - 2025-11-25

### Added
- **LM Studio Essential Mode**: New lightweight 80-tool configuration optimized for LM Studio and Cursor
  - 80 carefully selected essential tools (67% reduction from full 246 tools)
  - 62% smaller file size (90KB vs 239KB)
  - Perfect for local AI workflows without subscription costs
  - Includes: GameObject, Camera, Scene, UI, Screenshot, Animation basics
  - Removed: Scripting, GOAP, Weather, Advanced VFX, Batch operations
- **3-Mode Setup Window**: Easy selection for different AI clients
  - Claude Desktop/Cursor (Full 246 tools)
  - Cursor/LM Studio Essential (80 tools) ← NEW!
  - GitHub Copilot (Dynamic 8→N tools)
- **index-essential.js**: Optimized server script for essential mode

### Fixed
- **LM Studio Configuration**: Added missing `cwd` parameter to LM Studio mcp.json
  - Tools now correctly loaded and recognized by LM Studio
  - Proper working directory ensures Node.js modules load correctly

## [1.1.2] - 2025-11-24

### Fixed
- **Cinemachine 3.x Complete Support**: Full API compatibility
  - Fixed all 27 compilation errors when using Cinemachine 3.1.5
  - API Changes: Enum names (PositionModes, RotationModes, UpdateMethods)
  - API Changes: LensSettings.Orthographic → ModeOverride pattern
  - API Changes: ICinemachineCamera → CinemachineCamera casting
  - API Changes: Component property structs (PositionComposer, RotationComposer, Deoccluder)
  - API Changes: InputAxisController.Controllers.Length → Count
  - API Changes: ImpulseSource.SignalDefinition → ImpulseDefinition
  - API Changes: CinemachineDeoccluder.ObstacleAvoidance → AvoidObstacles
  - FreeLook implementation: Added missing CinemachineRotationComposer component
  - Added GetNoiseProfile() helper function for CM3
  - Readonly struct property handling (get-modify-set pattern)

- **Advanced Material Properties**: JSON parsing
  - Fixed nested `properties` parameter parsing
  - Properties now correctly applied (color, metallic, smoothness, emission)
  - Added detailed debug logging for property parsing
  - Backward compatibility maintained for direct parameter format

### Improved
- **FreeLook Camera UX**: Clearer error handling
  - Made `follow` parameter required (removed confusing auto-dummy-target)
  - Detailed error messages with cause + solutions + example code
  - Tool description improvements in index.js and tool-registry.json
  - Success messages show all created components
  - Cleanup on error (removes partially-created camera objects)

### Technical
- Both Cinemachine 2.9.x and 3.1.x fully supported via preprocessor directives
- No breaking changes - all existing code continues to work
- Enhanced logging throughout Cinemachine operations

## [1.1.1] - 2025-11-23

### Fixed
- **Cinemachine Compatibility**: Both v2 and v3 support
  - Fixed assembly reference issues for Cinemachine 2.9.7
  - Added proper version detection for Cinemachine v2 (`CINEMACHINE_2`) and v3 (`CINEMACHINE_3`)
  - Assembly definition now handles both `Cinemachine` (v2) and `Unity.Cinemachine` + `Unity.Splines` (v3)

- **URP Material Support**: Universal Render Pipeline compatibility
  - Added render pipeline detection with caching for performance
  - `CreatePrimitiveWithMaterial()` helper automatically applies URP/HDRP/Legacy shaders
  - Fixed pink material issue in URP projects when creating primitives
  - Updated all GameObject creation functions to use pipeline-compatible materials
  - Updated all game template functions (FPS, Platformer, RPG, Puzzle, Racing, Strategy)
  - Shader not found warnings added for debugging

### Improved
- **Code Quality**: Refactored primitive creation
  - Eliminated code duplication in `CreateGameObject()` function
  - Centralized material creation logic
  - Performance optimization: Cached render pipeline detection
  - Updated 50+ primitive creation locations across codebase

## [1.1.0] - 2025-11-23

### Added

#### Dynamic Tool Loading System (GitHub Copilot Support)
- **MCP Hub Server** (`hub-server.js`): Dynamic tool loading for GitHub Copilot
  - Starts with 8 essential management tools
  - Dynamic tool loading via `select_tools()` by category or keywords
  - Support for `notifications/tools/list_changed` (MCP spec 2025-06-18)
  - Automatic tool list refresh in GitHub Copilot (VS Code)
  - **No OpenAI API required**: Text-based keyword matching and category search
  - 31 tool categories for comprehensive Unity feature coverage
  - Access all 246 tools dynamically without hitting IDE tool limits
  - Unity WebSocket integration (port 8080) matching index.js protocol

#### Prompt Caching Support (Claude Desktop)
- **Tool Catalog Resource** (`unity://tools/catalog`): Dramatic session capacity improvement
  - **Verified Results (11 actual tool calls)**:
    - First request: 57,511 tokens (tool definitions cached)
    - 2-11 requests average: 1,596 tokens/call
    - **Token reduction: 97.2%** per subsequent call
    - Total used: 73,476 tokens (11 calls)
    - Remaining: 116,524 tokens (190,000 budget)
  - **Session Capacity**: ~84 total tool calls per session
    - Practical development: 70-90 operations
    - With heavy data fetching: 50-60 operations
  - **Use Cases Enabled**:
    - Create 3 UI screens (20 ops each) = 60 calls
    - 10 debugging iterations
    - 20 adjustments/confirmations
    - Total: 90 operations in single session

#### Lightweight Scene Information Tools
- **unity_get_scene_summary**: Fast scene overview (<200KB)
  - Scene name, GameObject count, cameras, lights
  - Root GameObjects list (max 50)
  - Replaces heavy `unity_get_scene_info` for large scenes

- **unity_get_gameobjects_list**: Filtered GameObject queries (<50KB)
  - Filter by layer, tag, name (contains), active state
  - Returns GameObject names, IDs, paths, metadata
  - Max 100 results per query

- **unity_get_gameobject_detail**: Individual GameObject inspection (<10KB)
  - Detailed transform, components, children, parent info
  - Find by name or instanceId
  - Component-specific details (MeshRenderer, Light, Camera, etc.)

- **unity_get_scene_changes_since**: Incremental scene updates
  - Timestamp-based differential updates
  - Returns added, removed, modified GameObjects
  - Efficient monitoring for large scenes

#### Setup Window AI Client Selection
- **AI Tool Selection UI**: Choose between Full Mode and Dynamic Mode
  - Claude Desktop / Cursor: Full Mode (246 tools, prompt caching)
  - GitHub Copilot: Dynamic Mode (8→dynamic tools, notifications)
  - Mode-specific setup instructions and success messages
  - Automatic server path selection (index.js or hub-server.js)

#### Infrastructure
- **Tool Loader System** (`utils/tool-loader.js`)
  - **31 tool categories**: GameObject, Transform, Material, Lighting, Camera, Physics, UI, Animation, Cinemachine, Scene, GOAP, Audio, Input, VFX, Shader, Weather, TimeOfDay, Editor, Package, Build, Monitoring, AssetManagement, Optimization, Batch, GameSystems, AI, Debug, Timeline, Scripting, Screenshot, Utility
  - **Text-based keyword matching** with relevance scoring (no API required)
  - Category-based filtering with default presets
  - Multi-keyword support with score boosting
  - Input normalization (arrays, strings, comma/pipe separated)

- **Tool Registry** (`tool-registry.json`)
  - Pre-generated metadata for all 246 tools
  - Category assignments and descriptions
  - **No external API dependency**: Generated via `generate-simple-registry.js`
  - Tool name, title, description, and category information

- **Optional OpenAI Integration** (`utils/embedding.js`)
  - Semantic search enhancement (if OPENAI_API_KEY provided)
  - Automatic fallback to text matching when unavailable
  - Not required for normal operation

### Changed
- **MCP Server Architecture**: Dual-mode support
  - `index.js`: Full Mode server (246 tools, all clients)
  - `hub-server.js`: Dynamic Mode server (GitHub Copilot only)

- **Setup Window**: Enhanced with AI client selection
  - Visual selection buttons with mode explanations
  - Dynamic info boxes based on selection
  - Mode-specific success messages

- **Tool Count**: Corrected to 246 unique tools
  - Verified count (247 registrations, 1 duplicate removed)
  - Updated all user-facing documentation

### Technical Details
- **MCP Capabilities**:
  - Full Mode: `tools: {}, resources: {}` (Prompt Caching via unity://tools/catalog)
  - Dynamic Mode: `tools: { listChanged: true }` (Notifications for dynamic tool updates)

- **Hub Server Communication**:
  - Unity WebSocket server on port 8080
  - Request/response tracking with pendingRequests Map
  - Protocol matching index.js for consistency
  - Proper error handling and message routing

- **Tool Search System**:
  - Primary: Text-based keyword matching with relevance scoring
  - Fallback chain: Multi-keyword matching → category filtering
  - No external API calls required for normal operation
  - Optional: OpenAI Embedding enhancement (if API key provided)

- **Supported AI Clients**:
  - ✅ Claude Desktop: Full Mode with Prompt Caching (~52% token reduction verified)
  - ✅ Cursor: Full Mode (246 tools, 80-tool warning can be ignored)
  - ✅ GitHub Copilot: Dynamic Mode with notifications support (tested)

- **Session Lifespan Improvements**:
  - First call: 57,511 tokens (tool definitions cached)
  - Subsequent calls: ~1,596 tokens average
  - **Result: ~84 total tool calls per session** (vs ~13 without caching)
  - **6.5x capacity increase** verified with real usage

### Dependencies
- **Added**: `openai` ^4.20.0 (optional - for enhanced semantic search only)
  - **Not required**: Hub server works with text-based keyword matching by default
  - Only needed if you want embedding-based semantic search enhancement

### Performance
- Scene information retrieval: 80-95% size reduction for large scenes
- Claude Desktop token usage: 52% reduction verified
- GitHub Copilot: All 246 tools accessible within 80-tool limit

## [1.0.3] - 2025-11-21 

### Added
- **One-Touch MCP Setup**: Automatic configuration for multiple AI tools with a single click
  - Configure Claude Desktop, Cursor, and VS Code simultaneously
  - `~/.cursor/mcp.json` automatically created for Cursor
  - `.vscode/mcp.json` automatically created for VS Code
  - **Smart Merge**: Preserves existing MCP server configurations, only adds/updates unity-synaptic
  - No manual path configuration needed
  - Setup Window button: "Complete MCP Setup"

- **unity_capture_grid**: Auto-split Game View into grid and capture all cells
  - Grid sizes: "2x2", "3x3", "4x4", up to "5x5"
  - Each cell saved as separate file with position info (basename_r0_c0.png, etc.)
  - Perfect for systematic UI analysis and debugging
  - Works with Canvas Overlay UI

- **unity_capture_ui_element**: Capture specific UI element by GameObject name
  - Automatically finds element and calculates screen bounds
  - Works with all Canvas render modes (Overlay, Camera, World Space)
  - No need to manually specify coordinates
  - Example: `elementName: "MoveButton"` captures just that button

- **unity_get_screenshot_result**: Async result retrieval for Play mode captures
  - Call after 3 seconds when capture returns `"status": "pending"`
  - Returns screenshot path, width, height
  - Works with all async capture operations

### Fixed
- **Screenshot Capture**: Complete overhaul of all screenshot tools to properly capture Canvas/UI
  - **CaptureGameView**:
    - Automatically enters Play mode if needed to capture Canvas Overlay
    - 60-frame wait for rendering stabilization before capture
    - Captures at native Game View resolution (fixed 3024x40 bug)
    - Exits Play mode automatically after capture
    - MCP tool description enhanced to force LLM to wait 3 seconds
  - **CaptureRegion**:
    - Game View mode now requires Play mode for Canvas Overlay capture
    - Captures full screenshot then extracts specified region
    - Uses actual screenshot dimensions instead of fixed resolution
    - Correctly captures Canvas elements in all render modes
  - **CaptureUIElement**:
    - Fixed Canvas Overlay coordinate conversion (use GetWorldCorners directly)
    - Fixed Y-coordinate inversion bug (both GetWorldCorners and GetPixels use bottom-left origin)
    - Added dimension validation before texture creation
    - Added debug logging for troubleshooting
  - **All Capture Tools**:
    - Auto-append .png extension if not specified
    - Case-insensitive extension check
    - Improved error messages

### Technical Details
- **60-Frame Wait Mechanism**: `ScreenshotFrameUpdate()` method ensures UI is fully rendered
- **EditorPrefs State Persistence**: Survives domain reload when entering Play mode
- **Canvas Overlay Support**: Direct coordinate usage without WorldToScreenPoint conversion
- **Y-Coordinate Fix**: No coordinate transformation needed (both systems use bottom-left origin)
- **Async Workflow**: LLM instructions updated to "WAIT EXACTLY 3 SECONDS" before result retrieval
- All screenshot tools now use proper resolution detection
- Canvas Overlay (Screen Space - Overlay) now correctly captured in Play mode
- Canvas Camera and World Space modes work in both Edit and Play mode

## [1.0.2] - 2025-11-20

### Fixed
- **Windows Compatibility**: Resolved MCP server startup issues on Windows
- **Cinemachine 3.x Support**: Added automatic API detection for Cinemachine 3.0+
  - Package now supports both Cinemachine 2.9.7 and 3.x versions
  - Automatic detection and adaptation to installed version

## [1.0.1] - 2025-11-19

### Added
- **Screenshot Capture Tools**: Three new MCP tools for visual analysis
  - `CaptureGameView`: Capture the Game View window
  - `CaptureSceneView`: Capture the Scene View window
  - `CaptureRegion`: Capture specific regions with coordinates
  - Enables Claude Vision to analyze Unity UI layouts

### Known Issues
- Screenshot capture may not correctly capture Canvas/UI elements (fixed in v1.0.3)

## [1.0.0] - 2025-11-15

### Added
- Initial release of Synaptic AI Pro for Unity
- 235+ professional MCP tools for Unity Editor control
- Natural language interface through Claude AI
- Scene management and GameObject manipulation
- Advanced lighting and rendering controls
- Physics and animation systems
- GOAP AI system with natural language planning
- Comprehensive documentation and examples
- Unity 2022.3+ and Unity 6.0+ support
