# Convoy Domed PvP Event Plugin

A complete hybrid PVP/PVE event plugin for Rust servers that creates dynamic convoy encounters with dome-based PvP zones and economy accumulation mechanics.

## Features

### 🚛 Moving Convoy System
- Uses configurable van prefab (`assets/prefabs/npc/vehicles/van/van.prefab`)
- High configurable health and movement speed
- Procedural waypoint generation and movement
- Visual damage effects and tracking

### 🎭 Special Head Item Mechanics
- Configurable helmet or custom head attire (default: metal facemask)
- Drops when player dies or leaves dome for >20 seconds
- Wearer accumulates economy while holding the item
- Automatic item return to convoy if abandoned

### 🏟️ Dome Generation System
- Auto-generates protective dome around convoy when damaged
- Integrates with ZoneManager for advanced zone control
- Inventory protection while inside dome
- PVP zone creation with customizable rules
- Exit warnings and time limits (default: 20 seconds)

### ⏱️ 3-Phase Event Structure
1. **Phase 1**: 2 minutes, max 200 economy, no NPCs
2. **Phase 2**: 3 minutes, max 300 economy, no NPCs  
3. **Phase 3**: 3 minutes, max 300 economy, 10-15 hostile NPCs

### 🎮 Advanced Game Mechanics
- Economy accumulation system (5 points per second default)
- Respawn inside dome on death
- Distance warnings when >50m from dome
- Automatic player selection with permission system
- Real-time UI updates and notifications

### 🖥️ Comprehensive UI System
- Real-time economy accumulation progress bar
- Phase timer with countdown
- Current special item holder display
- Dome entry/exit notifications
- Customizable colors and positions
- Responsive notification system

## Installation

1. Copy `ConvoyDomedPvP.cs` to your server's `oxide/plugins/` directory
2. Copy `ConvoyDomedPvP.json` to `oxide/config/` directory (optional - will auto-generate)
3. Restart server or use `oxide.reload ConvoyDomedPvP`
4. Configure permissions and settings as needed

## Dependencies

### Required Plugins
- **ZoneManager**: For dome/zone creation and management
- **Economics** (optional): For economy rewards integration

### Recommended Plugins  
- **ImageLibrary**: For enhanced UI graphics (not implemented in current version)

## Permissions

```
convoydomedpvp.admin    - Admin commands and event control
convoydomedpvp.use      - Participate in convoy events
```

## Console Commands

```
convoy.start    - Manually start a convoy event (admin only)
convoy.stop     - Stop the current convoy event (admin only)
```

## Configuration

### Event Settings
- `Enabled`: Enable/disable the plugin
- `StartCooldown`: Time between automatic events (seconds)
- `MinPlayers`: Minimum players required to start event
- `MaxPlayers`: Maximum participants per event
- `AutoStart`: Automatically start events
- `AnnounceToServer`: Broadcast event announcements

### Convoy Settings
- `Health`: Convoy vehicle health points
- `Speed`: Movement speed of convoy
- `WaypointCount`: Number of waypoints in patrol route
- `WaypointRadius`: Distance of waypoints from spawn
- `PrefabPath`: Vehicle prefab path
- `ShowDamageEffects`: Enable damage visual effects

### Dome Settings
- `Radius`: Dome/zone radius in meters
- `InventoryProtection`: Protect inventory while in dome
- `ExitWarningDistance`: Distance for exit warnings
- `ExitTimeLimit`: Time limit outside dome before penalties
- `ZoneID`: Base zone identifier
- `PVPEnabled`: Enable PVP inside dome

### Economy Settings
- `AccumulationRate`: Economy points per interval
- `AccumulationInterval`: Time between accumulation ticks
- `UseEconomics`: Integrate with Economics plugin
- `CurrencyName`: Display name for currency

### UI Settings
- `MainColor`: Primary UI color (hex)
- `SecondaryColor`: Secondary UI color (hex)
- `TextColor`: Text color (hex)
- `EconomyBarPosition`: Position of economy bar
- `TimerPosition`: Position of phase timer
- `LeaderPosition`: Position of leader display

### Phase Settings
Array of phase configurations:
- `Duration`: Phase duration in seconds
- `MaxEconomy`: Maximum economy for this phase
- `SpawnNPCs`: Whether to spawn NPCs in this phase
- `NPCCount`: Number of NPCs to spawn

## Event Flow

1. **Event Start**: Convoy spawns at random location away from players
2. **Movement**: Convoy begins patrolling procedurally generated waypoints
3. **Engagement**: Players locate and attack the convoy
4. **Dome Creation**: First damage triggers protective dome around convoy
5. **Phase Progression**: Event progresses through 3 timed phases
6. **Competition**: Players compete for special item to accumulate economy
7. **NPC Phase**: Hostile NPCs spawn in final phase for additional challenge
8. **Event End**: Final rewards distributed, cleanup performed

## Advanced Features

### Automatic Player Management
- Permission-based player selection
- Random selection if too many candidates
- Automatic cleanup on disconnect/timeout
- Dome exit monitoring and penalties

### Smart Spawning System
- Convoy spawns away from active players
- Waypoint generation considers terrain
- NPC spawning within dome boundaries
- Height adjustment for terrain compatibility

### Robust Error Handling
- Graceful fallbacks for missing dependencies
- Configuration validation and defaults
- Entity cleanup on plugin unload
- Safe timer and component management

## Customization

### Special Item Types
Modify `SPECIAL_ITEM_SHORTNAME` constant to use different items:
- `metal.facemask` (default)
- `coffeecan.helmet`
- `riot.helmet`
- Custom skin items

### NPC Types
Change NPC prefab in `SpawnPhaseNPCs()`:
- `scientist.prefab` (default)
- `murderer.prefab`
- `bandit_guard.prefab`
- Custom NPC prefabs

### Zone Integration
Supports ZoneManager zone flags:
- `pvpgod`: PVP god mode
- `pvegod`: PVE protection
- `sleepgod`: Sleep protection
- `undestr`: Indestructible items
- `nobuild`: Prevent building
- `notp`: Prevent teleporting

## Troubleshooting

### Common Issues

1. **Convoy not spawning**: Check prefab path in configuration
2. **Dome not creating**: Ensure ZoneManager is installed and loaded
3. **UI not showing**: Verify player has `convoydomedpvp.use` permission
4. **Economy not working**: Check Economics plugin integration
5. **NPCs not spawning**: Verify NPC prefab paths are correct

### Debug Commands
Use F1 console for debugging:
```
oxide.grant user <steamid> convoydomedpvp.admin
convoy.start
convoy.stop
```

## Version History

- **v1.0.0**: Initial release with full feature set

## Support

For support, customization, or bug reports, please contact the development team or visit the plugin's official documentation.

## License

This plugin is provided for educational and server enhancement purposes. Modification and redistribution are subject to the terms of use.