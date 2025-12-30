using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers; 
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;

namespace SmoothWizardOptimizer
{
    public class SmoothWizardOptimizer : BasePlugin
    {
        public override string ModuleName => "SmoothWizard Server Optimizer";
        public override string ModuleVersion => "1.0.5"; // 更新版本號
        public override string ModuleAuthor => "SkullMedia & Optimized";

        private bool isOptimizationEnabled = true;

        private readonly string[] entitiesToCleanup =
        {
           "cs_ragdoll", "env_explosion", "env_fire", "env_spark", "env_smokestack",
           "info_particle_system", "particle_*", "decals_*", "prop_physics_multiplayer", "prop_physics"
        };

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundStart>((ev, info) =>
            {
                if (isOptimizationEnabled) CleanupMapAndTempEntities("RoundStart");
                return HookResult.Continue;
            });

            AddCommand("css_sw_toggle", "Toggle Optimizer", OnToggleOptimizerCommand);
            Server.PrintToConsole($"[SmoothWizard] Plugin Loaded. Status: {isOptimizationEnabled}");
        }

        private void OnToggleOptimizerCommand(CCSPlayerController? player, CommandInfo info)
        {
            isOptimizationEnabled = !isOptimizationEnabled;
            // 使用你的 Localizer 進行翻譯
            string status = isOptimizationEnabled ? Localizer["fps.enabled"] : Localizer["fps.disabled"];
            
            // 由於 chatMessage 包含顏色，我們手動組合或使用翻譯檔
            player?.PrintToChat($" {ChatColors.Red}優化系統{ChatColors.White} 已 {status}");
        }

        private void CleanupMapAndTempEntities(string context)
        {
            int removedTotal = 0;
            int totalBatches = 0;
            
            // 核心修正：先建立一個大的總清單，避開在迴圈中重複觸發輸出
            List<CEntityInstance> allPendingEntities = new List<CEntityInstance>();
            
            foreach (var pattern in entitiesToCleanup)
            {
                var found = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern);
                if (found != null)
                {
                    allPendingEntities.AddRange(found);
                }
            }

            // 如果沒有東西要清，直接結束
            if (allPendingEntities.Count == 0) return;

            // 針對「所有實體」統籌分批
            var chunks = allPendingEntities.Chunk(50).ToList();
            totalBatches = chunks.Count;

            foreach (var batch in chunks)
            {
                Server.NextFrame(() =>
                {
                    foreach (var ent in batch)
                    {
                        try
                        {
                            if (ent == null || !ent.IsValid) continue;

                            // 官方提供的保護名單
                            bool isProtected = ent.DesignerName.Contains("door") || 
                                             ent.DesignerName.Contains("breakable") || 
                                             ent.DesignerName.StartsWith("weapon_") || 
                                             ent.DesignerName.Contains("vent") || 
                                             ent.DesignerName.Contains("shield") || 
                                             ent.DesignerName.Contains("movable_platform") || 
                                             ent.DesignerName.Contains("parent");

                            if (!isProtected)
                            {
                                ent.Remove();
                                removedTotal++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SmoothWizard] Error removing entity: {ex.Message}");
                        }
                    }

                    // 批次計數遞減
                    totalBatches--;

                    // 只有最後一個批次完成後，才進行「唯一一次」的翻譯輸出
                    if (totalBatches == 0 && removedTotal > 0)
                    {
                        // 輸出到控制台 (對應你的 JSON: fps.console_log)
                        Console.WriteLine(Localizer["fps.console_log", context, removedTotal]);
                        
                        // 輸出到聊天室 (對應你的 JSON: fps.cleanup_done)
                        Server.PrintToChatAll(Localizer["fps.cleanup_done", removedTotal]);
                    }
                });
            }
        }
    }
}
