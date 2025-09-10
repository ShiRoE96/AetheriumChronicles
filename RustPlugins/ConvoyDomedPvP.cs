using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("Convoy Domed PvP", "AetheriumChronicles", "1.0.0")]
    [Description("Hybrid PVP/PVE event with moving convoy and dome mechanics")]
    public class ConvoyDomedPvP : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ZoneManager, Economics, ImageLibrary;

        private Configuration config;
        private EventData activeEvent;
        private Timer eventTimer;
        private Timer economyTimer;
        private Dictionary<ulong, PlayerEventData> eventPlayers = new Dictionary<ulong, PlayerEventData>();

        private const string CONVOY_PREFAB = "assets/prefabs/npc/vehicles/van/van.prefab";
        private const string SPECIAL_ITEM_SHORTNAME = "metal.facemask"; // Can be configured
        
        #endregion

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Event Settings")]
            public EventSettings Event = new EventSettings();

            [JsonProperty("Convoy Settings")]
            public ConvoySettings Convoy = new ConvoySettings();

            [JsonProperty("Dome Settings")]
            public DomeSettings Dome = new DomeSettings();

            [JsonProperty("Economy Settings")]
            public EconomySettings Economy = new EconomySettings();

            [JsonProperty("UI Settings")]
            public UISettings UI = new UISettings();

            [JsonProperty("Phase Settings")]
            public PhaseSettings[] Phases = new PhaseSettings[]
            {
                new PhaseSettings { Duration = 120, MaxEconomy = 200, SpawnNPCs = false, NPCCount = 0 },
                new PhaseSettings { Duration = 180, MaxEconomy = 300, SpawnNPCs = false, NPCCount = 0 },
                new PhaseSettings { Duration = 180, MaxEconomy = 300, SpawnNPCs = true, NPCCount = 12 }
            };
        }

        private class EventSettings
        {
            public bool Enabled = true;
            public float StartCooldown = 3600f; // 1 hour
            public int MinPlayers = 3;
            public int MaxPlayers = 20;
            public bool AutoStart = true;
            public bool AnnounceToServer = true;
        }

        private class ConvoySettings
        {
            public float Health = 5000f;
            public float Speed = 8f;
            public int WaypointCount = 8;
            public float WaypointRadius = 200f;
            public string PrefabPath = CONVOY_PREFAB;
            public bool ShowDamageEffects = true;
        }

        private class DomeSettings
        {
            public float Radius = 100f;
            public bool InventoryProtection = true;
            public float ExitWarningDistance = 50f;
            public float ExitTimeLimit = 20f;
            public string ZoneID = "ConvoyDome";
            public bool PVPEnabled = true;
        }

        private class EconomySettings
        {
            public int AccumulationRate = 5; // Economy per second
            public float AccumulationInterval = 1f;
            public bool UseEconomics = true;
            public string CurrencyName = "RP";
        }

        private class UISettings
        {
            public string MainColor = "#FF6B35";
            public string SecondaryColor = "#004E89";
            public string TextColor = "#FFFFFF";
            public Vector2 EconomyBarPosition = new Vector2(0.5f, 0.9f);
            public Vector2 TimerPosition = new Vector2(0.5f, 0.85f);
            public Vector2 LeaderPosition = new Vector2(0.5f, 0.95f);
        }

        private class PhaseSettings
        {
            public int Duration; // seconds
            public int MaxEconomy;
            public bool SpawnNPCs;
            public int NPCCount;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                LoadDefaultConfig();
                PrintWarning("Creating new configuration file");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data Classes

        private class EventData
        {
            public BaseVehicle Convoy;
            public Vector3 DomeCenter;
            public List<Vector3> Waypoints = new List<Vector3>();
            public int CurrentWaypoint = 0;
            public int CurrentPhase = 0;
            public DateTime StartTime;
            public DateTime PhaseStartTime;
            public ulong SpecialItemHolder;
            public List<BasePlayer> Participants = new List<BasePlayer>();
            public List<BaseEntity> SpawnedNPCs = new List<BaseEntity>();
            public bool IsActive = false;
            public bool DomeCreated = false;
            public string ZoneID;
        }

        private class PlayerEventData
        {
            public int CurrentEconomy = 0;
            public DateTime LastExitTime;
            public bool IsOutsideDome = false;
            public bool HasSpecialItem = false;
        }

        #endregion

        #region Hooks

        void Init()
        {
            LoadConfig();
            
            // Register permissions
            permission.RegisterPermission("convoydomedpvp.admin", this);
            permission.RegisterPermission("convoydomedpvp.use", this);
            
            // Add console commands
            cmd.AddConsoleCommand("convoy.start", this, "ConsoleStartEvent");
            cmd.AddConsoleCommand("convoy.stop", this, "ConsoleStopEvent");
        }

        void OnServerInitialized()
        {
            if (config.Event.AutoStart)
            {
                timer.Once(config.Event.StartCooldown, () => TryStartEvent());
            }
        }

        void Unload()
        {
            if (activeEvent != null)
            {
                EndEvent();
            }
            
            // Clean up UI for all players
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        void OnEntityTakeDamage(BaseEntity entity, HitInfo hitinfo)
        {
            if (activeEvent == null) return;
            if (entity != activeEvent.Convoy) return;

            // Create dome when convoy takes damage
            if (!activeEvent.DomeCreated)
            {
                CreateDome();
                activeEvent.DomeCreated = true;
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (activeEvent == null || !eventPlayers.ContainsKey(player.userID)) return;

            var playerData = eventPlayers[player.userID];
            
            // Handle special item drop
            if (playerData.HasSpecialItem)
            {
                DropSpecialItem(player);
                playerData.HasSpecialItem = false;
                activeEvent.SpecialItemHolder = 0;
                
                // Notify players
                BroadcastToParticipants($"{player.displayName} has dropped the special item!");
            }

            // Respawn player inside dome
            timer.Once(2f, () => RespawnPlayerInDome(player));
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (activeEvent == null || !eventPlayers.ContainsKey(player.userID)) return;

            var playerData = eventPlayers[player.userID];
            
            // Handle special item drop on disconnect
            if (playerData.HasSpecialItem)
            {
                DropSpecialItem(player);
                activeEvent.SpecialItemHolder = 0;
            }

            // Remove from event
            RemovePlayerFromEvent(player);
        }

        void OnItemPickup(BasePlayer player, Item item)
        {
            if (activeEvent == null || item.info.shortname != SPECIAL_ITEM_SHORTNAME) return;
            if (!eventPlayers.ContainsKey(player.userID)) return;

            // Check if this is the special event item
            if (item.GetHeldEntity()?.GetComponent<SpecialItemComponent>() != null)
            {
                var oldHolder = activeEvent.SpecialItemHolder;
                if (oldHolder != 0 && oldHolder != player.userID)
                {
                    var oldPlayer = BasePlayer.FindByID(oldHolder);
                    if (oldPlayer != null && eventPlayers.ContainsKey(oldHolder))
                    {
                        eventPlayers[oldHolder].HasSpecialItem = false;
                    }
                }

                activeEvent.SpecialItemHolder = player.userID;
                eventPlayers[player.userID].HasSpecialItem = true;
                
                BroadcastToParticipants($"{player.displayName} now holds the special item!");
                RefreshAllUI();
            }
        }

        #endregion

        #region Event Management

        private bool TryStartEvent()
        {
            if (activeEvent != null) return false;
            if (BasePlayer.activePlayerList.Count < config.Event.MinPlayers) return false;

            return StartEvent();
        }

        private bool StartEvent()
        {
            activeEvent = new EventData
            {
                StartTime = DateTime.Now,
                PhaseStartTime = DateTime.Now,
                ZoneID = config.Dome.ZoneID + "_" + UnityEngine.Random.Range(1000, 9999)
            };

            // Spawn convoy
            if (!SpawnConvoy())
            {
                PrintError("Failed to spawn convoy!");
                activeEvent = null;
                return false;
            }

            // Generate waypoints
            GenerateWaypoints();

            // Start movement
            StartConvoyMovement();

            // Add eligible players
            AddEligiblePlayersToEvent();

            // Start timers
            StartEventTimers();

            // Create special item
            CreateSpecialItem();

            // UI and announcements
            if (config.Event.AnnounceToServer)
            {
                Server.Broadcast($"<color={config.UI.MainColor}>[Convoy Event]</color> A convoy has been spotted! Join the event for rewards!");
            }

            RefreshAllUI();
            activeEvent.IsActive = true;

            return true;
        }

        private void EndEvent()
        {
            if (activeEvent == null) return;

            // Destroy convoy
            if (activeEvent.Convoy != null && !activeEvent.Convoy.IsDestroyed)
            {
                activeEvent.Convoy.Kill();
            }

            // Clean up dome/zone
            if (activeEvent.DomeCreated && ZoneManager != null)
            {
                ZoneManager.Call("EraseZone", activeEvent.ZoneID);
            }

            // Clean up NPCs
            foreach (var npc in activeEvent.SpawnedNPCs.Where(x => x != null && !x.IsDestroyed))
            {
                npc.Kill();
            }

            // Award final rewards
            AwardFinalRewards();

            // Clean up player data and UI
            foreach (var playerID in eventPlayers.Keys.ToList())
            {
                var player = BasePlayer.FindByID(playerID);
                if (player != null)
                {
                    DestroyUI(player);
                }
            }

            eventPlayers.Clear();
            
            // Clean up timers
            eventTimer?.Destroy();
            economyTimer?.Destroy();

            // Announce end
            if (config.Event.AnnounceToServer)
            {
                Server.Broadcast($"<color={config.UI.MainColor}>[Convoy Event]</color> The convoy event has ended!");
            }

            activeEvent = null;

            // Schedule next event
            if (config.Event.AutoStart)
            {
                timer.Once(config.Event.StartCooldown, () => TryStartEvent());
            }
        }

        #endregion

        #region Convoy Management

        private bool SpawnConvoy()
        {
            var spawnPos = GetRandomSpawnPosition();
            var convoy = GameManager.server.CreateEntity(config.Convoy.PrefabPath, spawnPos) as BaseVehicle;
            
            if (convoy == null) return false;

            convoy.Spawn();
            convoy.health = config.Convoy.Health;
            
            // Add custom component for tracking
            convoy.gameObject.AddComponent<ConvoyComponent>().Initialize(this);
            
            activeEvent.Convoy = convoy;
            return true;
        }

        private Vector3 GetRandomSpawnPosition()
        {
            // Find a suitable spawn position away from players
            for (int i = 0; i < 10; i++)
            {
                var pos = new Vector3(
                    UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2),
                    0,
                    UnityEngine.Random.Range(-TerrainMeta.Size.z / 2, TerrainMeta.Size.z / 2)
                );
                
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                
                // Check if position is clear of players
                bool clear = true;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (Vector3.Distance(player.transform.position, pos) < 200f)
                    {
                        clear = false;
                        break;
                    }
                }
                
                if (clear) return pos;
            }
            
            return Vector3.zero;
        }

        private void GenerateWaypoints()
        {
            var center = activeEvent.Convoy.transform.position;
            activeEvent.Waypoints.Clear();
            
            for (int i = 0; i < config.Convoy.WaypointCount; i++)
            {
                var angle = (360f / config.Convoy.WaypointCount) * i;
                var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var waypoint = center + direction * config.Convoy.WaypointRadius;
                
                waypoint.y = TerrainMeta.HeightMap.GetHeight(waypoint);
                activeEvent.Waypoints.Add(waypoint);
            }
        }

        private void StartConvoyMovement()
        {
            if (activeEvent.Convoy == null) return;
            
            var component = activeEvent.Convoy.GetComponent<ConvoyComponent>();
            if (component != null)
            {
                component.StartMovement(activeEvent.Waypoints, config.Convoy.Speed);
            }
        }

        #endregion

        #region Dome Management

        private void CreateDome()
        {
            if (ZoneManager == null)
            {
                PrintWarning("ZoneManager not found! Dome cannot be created.");
                return;
            }

            activeEvent.DomeCenter = activeEvent.Convoy.transform.position;
            
            var zoneConfig = new Dictionary<string, object>
            {
                {"name", activeEvent.ZoneID},
                {"radius", config.Dome.Radius},
                {"location", $"{activeEvent.DomeCenter.x} {activeEvent.DomeCenter.y} {activeEvent.DomeCenter.z}"},
                {"eject", false},
                {"pvpgod", !config.Dome.PVPEnabled},
                {"pvegod", config.Dome.InventoryProtection},
                {"sleepgod", config.Dome.InventoryProtection},
                {"undestr", config.Dome.InventoryProtection},
                {"nobuild", true},
                {"notp", true},
                {"nokits", true}
            };

            var result = ZoneManager.Call("CreateOrUpdateZone", activeEvent.ZoneID, zoneConfig);
            
            if (result != null)
            {
                BroadcastToParticipants("A protective dome has formed around the convoy!");
                
                // Start monitoring players for dome exit
                InvokeRepeating(nameof(CheckPlayerPositions), 1f, 1f);
            }
        }

        private void CheckPlayerPositions()
        {
            if (activeEvent == null || !activeEvent.DomeCreated) return;

            foreach (var kvp in eventPlayers.ToList())
            {
                var player = BasePlayer.FindByID(kvp.Key);
                if (player == null) continue;

                var distance = Vector3.Distance(player.transform.position, activeEvent.DomeCenter);
                var playerData = kvp.Value;
                
                if (distance > config.Dome.Radius)
                {
                    if (!playerData.IsOutsideDome)
                    {
                        playerData.IsOutsideDome = true;
                        playerData.LastExitTime = DateTime.Now;
                        
                        SendNotification(player, "You have left the dome! Return within 20 seconds or lose your progress!", config.UI.MainColor);
                    }
                    else
                    {
                        var timePassed = (DateTime.Now - playerData.LastExitTime).TotalSeconds;
                        if (timePassed >= config.Dome.ExitTimeLimit)
                        {
                            // Player has been outside too long
                            if (playerData.HasSpecialItem)
                            {
                                ReturnSpecialItemToStart(player);
                                BroadcastToParticipants($"{player.displayName} stayed outside the dome too long! The special item has returned to the convoy.");
                            }
                            
                            RemovePlayerFromEvent(player);
                        }
                        else if (distance > config.Dome.ExitWarningDistance)
                        {
                            var remaining = config.Dome.ExitTimeLimit - timePassed;
                            SendNotification(player, $"Return to dome! {remaining:F0}s remaining", "#FF0000");
                        }
                    }
                }
                else
                {
                    if (playerData.IsOutsideDome)
                    {
                        playerData.IsOutsideDome = false;
                        SendNotification(player, "Welcome back to the dome!", "#00FF00");
                    }
                }
            }
        }

        #endregion

        #region Phase Management

        private void StartEventTimers()
        {
            eventTimer = timer.Every(1f, UpdateEventTimer);
            economyTimer = timer.Every(config.Economy.AccumulationInterval, UpdateEconomy);
        }

        private void UpdateEventTimer()
        {
            if (activeEvent == null) return;

            var currentPhase = config.Phases[activeEvent.CurrentPhase];
            var phaseElapsed = (DateTime.Now - activeEvent.PhaseStartTime).TotalSeconds;
            
            if (phaseElapsed >= currentPhase.Duration)
            {
                NextPhase();
            }
            
            RefreshAllUI();
        }

        private void NextPhase()
        {
            if (activeEvent.CurrentPhase >= config.Phases.Length - 1)
            {
                EndEvent();
                return;
            }

            activeEvent.CurrentPhase++;
            activeEvent.PhaseStartTime = DateTime.Now;
            
            var newPhase = config.Phases[activeEvent.CurrentPhase];
            
            BroadcastToParticipants($"Phase {activeEvent.CurrentPhase + 1} has begun! ({newPhase.Duration}s, Max: {newPhase.MaxEconomy} {config.Economy.CurrencyName})");
            
            // Spawn NPCs if needed
            if (newPhase.SpawnNPCs)
            {
                SpawnPhaseNPCs(newPhase.NPCCount);
            }
            
            RefreshAllUI();
        }

        private void SpawnPhaseNPCs(int count)
        {
            if (activeEvent?.DomeCenter == null) return;

            for (int i = 0; i < count; i++)
            {
                var spawnPos = GetRandomPositionInDome();
                var npc = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", spawnPos);
                
                if (npc != null)
                {
                    npc.Spawn();
                    activeEvent.SpawnedNPCs.Add(npc);
                }
            }
            
            BroadcastToParticipants($"Hostiles have entered the dome! ({count} enemies spawned)");
        }

        private Vector3 GetRandomPositionInDome()
        {
            var center = activeEvent.DomeCenter;
            var angle = UnityEngine.Random.Range(0, 360);
            var distance = UnityEngine.Random.Range(10f, config.Dome.Radius * 0.8f);
            
            var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var position = center + direction * distance;
            position.y = TerrainMeta.HeightMap.GetHeight(position);
            
            return position;
        }

        #endregion

        #region Economy System

        private void UpdateEconomy()
        {
            if (activeEvent == null || activeEvent.SpecialItemHolder == 0) return;

            var holder = BasePlayer.FindByID(activeEvent.SpecialItemHolder);
            if (holder == null || !eventPlayers.ContainsKey(holder.userID)) return;

            var playerData = eventPlayers[holder.userID];
            var currentPhase = config.Phases[activeEvent.CurrentPhase];
            
            if (playerData.CurrentEconomy < currentPhase.MaxEconomy)
            {
                playerData.CurrentEconomy += config.Economy.AccumulationRate;
                
                if (playerData.CurrentEconomy > currentPhase.MaxEconomy)
                {
                    playerData.CurrentEconomy = currentPhase.MaxEconomy;
                }
            }
            
            RefreshPlayerUI(holder);
        }

        private void AwardFinalRewards()
        {
            if (Economics == null && !config.Economy.UseEconomics) return;

            foreach (var kvp in eventPlayers)
            {
                var player = BasePlayer.FindByID(kvp.Key);
                if (player == null) continue;

                var economy = kvp.Value.CurrentEconomy;
                if (economy > 0)
                {
                    if (config.Economy.UseEconomics && Economics != null)
                    {
                        Economics.Call("Deposit", player.userID, economy);
                    }
                    
                    SendNotification(player, $"Event reward: {economy} {config.Economy.CurrencyName}!", config.UI.MainColor);
                }
            }
        }

        #endregion

        #region Special Item Management

        private void CreateSpecialItem()
        {
            if (activeEvent?.Convoy == null) return;

            var item = ItemManager.CreateByName(SPECIAL_ITEM_SHORTNAME, 1, config.Economy.UseEconomics ? 1234567890uL : 0);
            if (item == null) return;

            // Add custom component to identify this as the special event item
            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.gameObject.AddComponent<SpecialItemComponent>();
            }

            // Drop near convoy
            var dropPos = activeEvent.Convoy.transform.position + Vector3.up * 2f;
            item.Drop(dropPos, Vector3.up);
        }

        private void DropSpecialItem(BasePlayer player)
        {
            var item = player.inventory.FindItemByID(ItemManager.FindItemDefinition(SPECIAL_ITEM_SHORTNAME).itemid);
            if (item?.GetHeldEntity()?.GetComponent<SpecialItemComponent>() != null)
            {
                item.Drop(player.transform.position + Vector3.up, Vector3.up);
            }
        }

        private void ReturnSpecialItemToStart(BasePlayer player)
        {
            var item = player.inventory.FindItemByID(ItemManager.FindItemDefinition(SPECIAL_ITEM_SHORTNAME).itemid);
            if (item?.GetHeldEntity()?.GetComponent<SpecialItemComponent>() != null)
            {
                item.RemoveFromContainer();
                item.Drop(activeEvent.Convoy.transform.position + Vector3.up * 2f, Vector3.up);
            }
            
            if (eventPlayers.ContainsKey(player.userID))
            {
                eventPlayers[player.userID].HasSpecialItem = false;
            }
            
            activeEvent.SpecialItemHolder = 0;
        }

        #endregion

        #region Player Management

        private void AddEligiblePlayersToEvent()
        {
            var candidates = BasePlayer.activePlayerList.Where(p => 
                permission.UserHasPermission(p.UserIDString, "convoydomedpvp.use")).ToList();
            
            if (candidates.Count > config.Event.MaxPlayers)
            {
                candidates = candidates.OrderBy(x => UnityEngine.Random.value).Take(config.Event.MaxPlayers).ToList();
            }

            foreach (var player in candidates)
            {
                eventPlayers[player.userID] = new PlayerEventData();
                activeEvent.Participants.Add(player);
                
                SendNotification(player, "You have been added to the Convoy Event!", config.UI.MainColor);
            }
        }

        private void RemovePlayerFromEvent(BasePlayer player)
        {
            if (!eventPlayers.ContainsKey(player.userID)) return;

            eventPlayers.Remove(player.userID);
            activeEvent.Participants.Remove(player);
            DestroyUI(player);
            
            SendNotification(player, "You have been removed from the event.", "#FF0000");
        }

        private void RespawnPlayerInDome(BasePlayer player)
        {
            if (activeEvent?.DomeCenter == null) return;
            if (!eventPlayers.ContainsKey(player.userID)) return;

            var spawnPos = GetRandomPositionInDome();
            player.Respawn();
            player.Teleport(spawnPos);
            
            SendNotification(player, "You have respawned inside the dome!", config.UI.MainColor);
        }

        #endregion

        #region UI System

        private void RefreshAllUI()
        {
            foreach (var playerID in eventPlayers.Keys.ToList())
            {
                var player = BasePlayer.FindByID(playerID);
                if (player != null)
                {
                    RefreshPlayerUI(player);
                }
            }
        }

        private void RefreshPlayerUI(BasePlayer player)
        {
            if (activeEvent == null || !eventPlayers.ContainsKey(player.userID)) return;

            DestroyUI(player);
            CreateUI(player);
        }

        private void CreateUI(BasePlayer player)
        {
            var elements = new CuiElementContainer();
            var playerData = eventPlayers[player.userID];
            var currentPhase = config.Phases[activeEvent.CurrentPhase];
            var phaseElapsed = (DateTime.Now - activeEvent.PhaseStartTime).TotalSeconds;
            var phaseRemaining = currentPhase.Duration - phaseElapsed;

            // Main panel
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Overlay", "ConvoyEventUI");

            // Economy bar
            var economyPercent = (float)playerData.CurrentEconomy / currentPhase.MaxEconomy;
            elements.Add(new CuiPanel
            {
                Image = { Color = HexToRustColor(config.UI.SecondaryColor) },
                RectTransform = { 
                    AnchorMin = $"{config.UI.EconomyBarPosition.x - 0.1f} {config.UI.EconomyBarPosition.y - 0.02f}", 
                    AnchorMax = $"{config.UI.EconomyBarPosition.x + 0.1f} {config.UI.EconomyBarPosition.y + 0.02f}" 
                }
            }, "ConvoyEventUI", "EconomyBarBG");

            elements.Add(new CuiPanel
            {
                Image = { Color = HexToRustColor(config.UI.MainColor) },
                RectTransform = { 
                    AnchorMin = "0 0", 
                    AnchorMax = $"{economyPercent} 1" 
                }
            }, "EconomyBarBG", "EconomyBar");

            // Economy text
            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = $"Economy: {playerData.CurrentEconomy}/{currentPhase.MaxEconomy} {config.Economy.CurrencyName}",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = HexToRustColor(config.UI.TextColor)
                },
                RectTransform = { 
                    AnchorMin = $"{config.UI.EconomyBarPosition.x - 0.15f} {config.UI.EconomyBarPosition.y - 0.05f}",
                    AnchorMax = $"{config.UI.EconomyBarPosition.x + 0.15f} {config.UI.EconomyBarPosition.y + 0.05f}"
                }
            }, "ConvoyEventUI", "EconomyText");

            // Phase timer
            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = $"Phase {activeEvent.CurrentPhase + 1} - {FormatTime(phaseRemaining)}",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = HexToRustColor(config.UI.TextColor)
                },
                RectTransform = { 
                    AnchorMin = $"{config.UI.TimerPosition.x - 0.1f} {config.UI.TimerPosition.y - 0.025f}",
                    AnchorMax = $"{config.UI.TimerPosition.x + 0.1f} {config.UI.TimerPosition.y + 0.025f}"
                }
            }, "ConvoyEventUI", "PhaseTimer");

            // Current leader
            var leaderName = "None";
            if (activeEvent.SpecialItemHolder != 0)
            {
                var leader = BasePlayer.FindByID(activeEvent.SpecialItemHolder);
                leaderName = leader?.displayName ?? "Unknown";
            }

            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = $"Special Item Holder: {leaderName}",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = HexToRustColor(config.UI.MainColor)
                },
                RectTransform = { 
                    AnchorMin = $"{config.UI.LeaderPosition.x - 0.15f} {config.UI.LeaderPosition.y - 0.02f}",
                    AnchorMax = $"{config.UI.LeaderPosition.x + 0.15f} {config.UI.LeaderPosition.y + 0.02f}"
                }
            }, "ConvoyEventUI", "LeaderText");

            CuiHelper.AddUi(player, elements);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ConvoyEventUI");
        }

        private void SendNotification(BasePlayer player, string message, string color = "#FFFFFF")
        {
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiLabel
            {
                Text = { 
                    Text = message,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = HexToRustColor(color)
                },
                RectTransform = { AnchorMin = "0.25 0.8", AnchorMax = "0.75 0.85" }
            }, "Overlay", "ConvoyNotification");

            CuiHelper.AddUi(player, elements);
            timer.Once(5f, () => CuiHelper.DestroyUi(player, "ConvoyNotification"));
        }

        #endregion

        #region Utility Methods

        private void BroadcastToParticipants(string message)
        {
            foreach (var participant in activeEvent.Participants.Where(p => p != null))
            {
                participant.ChatMessage($"<color={config.UI.MainColor}>[Convoy Event]</color> {message}");
            }
        }

        private string HexToRustColor(string hex)
        {
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);
                
            if (hex.Length != 6) return "1 1 1 1";

            var r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            var g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            var b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            
            return $"{r} {g} {b} 1";
        }

        private string FormatTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        #endregion

        #region Console Commands

        private void ConsoleStartEvent(ConsoleSystem.Arg arg)
        {
            if (!permission.UserHasPermission(arg.Player()?.UserIDString ?? "", "convoydomedpvp.admin"))
            {
                arg.ReplyWith("You don't have permission to use this command.");
                return;
            }

            if (activeEvent != null)
            {
                arg.ReplyWith("Event is already active!");
                return;
            }

            if (StartEvent())
            {
                arg.ReplyWith("Convoy event started successfully!");
            }
            else
            {
                arg.ReplyWith("Failed to start convoy event.");
            }
        }

        private void ConsoleStopEvent(ConsoleSystem.Arg arg)
        {
            if (!permission.UserHasPermission(arg.Player()?.UserIDString ?? "", "convoydomedpvp.admin"))
            {
                arg.ReplyWith("You don't have permission to use this command.");
                return;
            }

            if (activeEvent == null)
            {
                arg.ReplyWith("No active event to stop.");
                return;
            }

            EndEvent();
            arg.ReplyWith("Convoy event stopped.");
        }

        #endregion

        #region Component Classes

        private class ConvoyComponent : MonoBehaviour
        {
            private ConvoyDomedPvP plugin;
            private List<Vector3> waypoints;
            private int currentWaypoint = 0;
            private float speed;
            private bool isMoving = false;

            public void Initialize(ConvoyDomedPvP pluginInstance)
            {
                plugin = pluginInstance;
            }

            public void StartMovement(List<Vector3> waypointList, float moveSpeed)
            {
                waypoints = waypointList;
                speed = moveSpeed;
                isMoving = true;
                InvokeRepeating(nameof(Move), 0f, 0.1f);
            }

            private void Move()
            {
                if (!isMoving || waypoints == null || waypoints.Count == 0) return;

                var currentPos = transform.position;
                var targetPos = waypoints[currentWaypoint];
                var direction = (targetPos - currentPos).normalized;

                // Move towards waypoint
                var newPos = currentPos + direction * speed * 0.1f;
                transform.position = newPos;

                // Check if reached waypoint
                if (Vector3.Distance(newPos, targetPos) < 5f)
                {
                    currentWaypoint = (currentWaypoint + 1) % waypoints.Count;
                }
            }

            void OnDestroy()
            {
                CancelInvoke();
            }
        }

        private class SpecialItemComponent : MonoBehaviour
        {
            // This component is used to identify the special event item
        }

        #endregion
    }
}