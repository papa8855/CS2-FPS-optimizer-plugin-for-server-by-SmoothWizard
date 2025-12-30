using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Localization;

namespace SmoothWizardOptimizer
{
    public class SmoothWizardOptimizer : BasePlugin
    {
        public override string ModuleName => "SmoothWizard Server Optimizer";
        public override string ModuleVersion => "1.0.1";
        public override string ModuleAuthor => "SkullMedia Artur Spychalski";

        private bool isOptimizationEnabled = true;

        private readonly string[] entitiesToCleanup =
        {
           "cs_ragdoll",
           "env_explosion",
           "env_fire",
           "env_spark",
           "env_smokestack",
           "info_particle_system",
           "particle_*",
           "decals_*",
           "prop_physics_multiplayer",
           "prop_physics",
        };

        private readonly bool debugMode = false;
        private bool clearLoopActive = false;
        private readonly HashSet<uint> removedDebugEntityIds = new();

        private CounterStrikeSharp.API.Modules.Timers.Timer? debugTimer = null;

        private Queue<CEntityInstance> entitiesToClearQueue = new Queue<CEntityInstance>();

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            AddCommand("css_sw_toggle", "Toggles the SmoothWizard server optimization cleanup on/off.", OnToggleOptimizerCommand);
            AddCommand("css_sw_clear", "Starts the entity removal loop from the list. DEBUG", OnClearEntitiesLoopCommand);
            AddCommand("css_sw_stop", "Stops the entity removal loop. DEBUG", OnStopClearLoopCommand);

            Server.PrintToConsole($"[SmoothWizard Server Optimizer] Loaded successfully (v{ModuleVersion}). Optimization: {(isOptimizationEnabled ? "ON" : "OFF")}.");
        }

        public override void Unload(bool hotReload)
        {
            debugTimer?.Kill();
        }

        private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
        {
            if (isOptimizationEnabled && !debugMode)
            {
                CleanupMapAndTempEntities("RoundStart");
            }
            return HookResult.Continue;
        }

        private void OnToggleOptimizerCommand(CCSPlayerController? player, CommandInfo info)
        {
            isOptimizationEnabled = !isOptimizationEnabled;
            string status = isOptimizationEnabled ? Localizer["fps.enabled"] : Localizer["fps.disabled"];
            player?.PrintToChat($" {ChatColors.Red}優化系統{ChatColors.White} 已 {status}");
        }

        private void OnStopClearLoopCommand(CCSPlayerController? player, CommandInfo info)
        {
            clearLoopActive = false;
            debugTimer?.Kill();
            debugTimer = null;
            entitiesToClearQueue.Clear();
            info.ReplyToCommand("Stopped entity removal debug loop (css_sw_dclear).");
            Server.PrintToConsole("[SW DEBUG] Stopped entity removal loop.");
        }

        private void OnClearEntitiesLoopCommand(CCSPlayerController? player, CommandInfo info)
        {
            if (clearLoopActive)
            {
                info.ReplyToCommand("The loop is already running. Use css_sw_dstop to stop.");
                return;
            }

            entitiesToClearQueue.Clear();
            foreach (var pattern in entitiesToCleanup)
            {
                var entities = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern).Where(e => e != null && e.IsValid);
                foreach (var ent in entities)
                {
                    bool isProtected = ent.DesignerName.Contains("door") || ent.DesignerName.Contains("breakable") || ent.DesignerName.StartsWith("weapon_");
                    if (!isProtected)
                    {
                        entitiesToClearQueue.Enqueue(ent);
                    }
                }
            }

            if (entitiesToClearQueue.Count == 0)
            {
                info.ReplyToCommand("No entities found for removal in the debug loop list.");
                return;
            }

            clearLoopActive = true;
            info.ReplyToCommand($"[{ChatColors.Red}SmoothWizard{ChatColors.White}]Started removing {entitiesToClearQueue.Count} entities. Use css_sw_dstop to stop.");

            debugTimer = AddTimer(0.01f, ClearEntitiesLoop, TimerFlags.REPEAT);
        }

        private void ClearEntitiesLoop()
        {
            if (!clearLoopActive || entitiesToClearQueue.Count == 0)
            {
                clearLoopActive = false;
                debugTimer?.Kill();
                debugTimer = null;
                entitiesToClearQueue.Clear();
                Server.PrintToChatAll($"[{ChatColors.Red}SmoothWizard{ChatColors.White}] Entity removal loop finished.");
                return;
            }

            var ent = entitiesToClearQueue.Dequeue();

            if (ent == null || !ent.IsValid)
            {
                return;
            }

            Server.NextFrame(() =>
            {
                try
                {
                    string details = $"{ent.DesignerName} [ID: {ent.Index}]";
                    ent.Remove();
                    removedDebugEntityIds.Add(ent.Index);
                }
                catch (Exception ex)
                {
                    Server.PrintToConsole($"[SW DEBUG ERROR] Error removing {ent.DesignerName} [ID: {ent.Index}]: {ex.Message}");
                }
            });
        }

        private void CleanupMapAndTempEntities(string context)
        {
            int removedTotal = 0;
            int totalBatches = 0;

            foreach (var pattern in entitiesToCleanup)
            {
                var entities = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern).ToList();

                foreach (var batch in entities.Chunk(50))
                {
                    totalBatches++;

                    Server.NextFrame(() =>
                    {
                        foreach (var ent in batch)
                        {
                            try
                            {
                                if (ent == null || !ent.IsValid)
                                    continue;

                                bool isProtected = ent.DesignerName.Contains("door") || ent.DesignerName.Contains("breakable") || ent.DesignerName.StartsWith("weapon_") ||
                                                     ent.DesignerName.Contains("vent") || ent.DesignerName.Contains("shield") || ent.DesignerName.Contains("movable_platform") ||
                                                     ent.DesignerName.Contains("parent");

                                if (!isProtected)
                                {
                                    ent.Remove();
                                    removedTotal++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Server.PrintToConsole($"[SmoothWizard] Warning: failed to process {ent?.DesignerName ?? "unknown"} - {ex.Message}");
                            }
                        }

                        totalBatches--;

                        if (totalBatches == 0)
                        {
                            if (removedTotal > 0)
                            {
                                Console.WriteLine(Localizer["fps.console_log", context, removedTotal]);
                                Server.PrintToChatAll(Localizer["fps.cleanup_done", removedTotal]);
                            }
                        }
                    });
                }
            }
        }
    }
}
