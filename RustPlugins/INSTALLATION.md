# Convoy Domed PvP - Quick Installation Guide

## Step 1: Prerequisites
Ensure your Rust server has the following:
- **Oxide/uMod framework** installed and running
- **ZoneManager plugin** (required for dome functionality)
- **Economics plugin** (optional, for reward integration)

## Step 2: Install Files
1. Copy `ConvoyDomedPvP.cs` to your server's `oxide/plugins/` directory
2. Restart your server or reload with: `oxide.reload ConvoyDomedPvP`

## Step 3: Configure Permissions
Grant permissions to users who should participate:
```
oxide.grant group default convoydomedpvp.use
oxide.grant user <admin_steamid> convoydomedpvp.admin
```

## Step 4: Start Your First Event
Use the admin console command:
```
convoy.start
```

## Step 5: Customize Configuration
The plugin will auto-generate `ConvoyDomedPvP.json` in your `oxide/config/` directory.
Modify settings as needed and reload the plugin.

## Quick Test
1. Grant yourself admin permission
2. Run `convoy.start` in console
3. Look for convoy spawn announcement
4. Attack the convoy to trigger the dome
5. Pick up the special item to start accumulating economy

## Dependencies Installation
If you don't have the required plugins:

### ZoneManager
- Download from uMod.org
- Place in `oxide/plugins/` directory
- Reload with `oxide.reload ZoneManager`

### Economics (Optional)
- Download from uMod.org  
- Place in `oxide/plugins/` directory
- Reload with `oxide.reload Economics`

## Default Event Schedule
- Events auto-start every hour (3600 seconds)
- Minimum 3 players required
- Maximum 20 participants
- 3 phases: 2min, 3min, 3min

You're ready to go! The convoy event will now run automatically on your server.