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
        public override string ModuleVersion => "1.0.2";
        public override string ModuleAuthor => "SkullMedia & Optimized";

        private bool isOptimizationEnabled = true;

        [cite_start]// 官方原始檔的清理清單 
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

            AddCommand("css_sw_toggle", "切換優化器狀態", OnToggleOptimizerCommand);

            Server.PrintToConsole($"[SmoothWizard] Plugin Loaded. Status: {isOptimizationEnabled}");
        }

        private void OnToggleOptimizerCommand(CCSPlayerController? player, CommandInfo info)
        {
            isOptimizationEnabled = !isOptimizationEnabled;
            
            [cite_start]// 讀取語系檔中的開啟/關閉文字 
            string status = isOptimizationEnabled ? Localizer["fps.enabled"] : Localizer["fps.disabled"];
            
            // 組合訊息並發送到聊天室
            player?.PrintToChat($" {ChatColors.Red}[SmoothWizard]{ChatColors.White} 優化器已 {status}");
        }

        private void CleanupMapAndTempEntities(string context)
        {
            int removedTotal = 0;
            int totalBatches = 0;

            foreach (var pattern in entitiesToCleanup)
            {
                var entities = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern).ToList();

                [cite_start]// 官方原始檔的分批處理邏輯 (Chunk 50) 
                foreach (var batch in entities.Chunk(50))
                {
                    totalBatches++;
                    Server.NextFrame(() =>
                    {
                        foreach (var ent in batch)
                        {
                            if (ent == null || !ent.IsValid) continue;

                            [cite_start]// 官方保護邏輯：不刪除門、武器、通風口等 
                            bool isProtected = ent.DesignerName.Contains("door") || ent.DesignerName.Contains("breakable") || 
                                             ent.DesignerName.StartsWith("weapon_") || ent.DesignerName.Contains("vent");

                            if (!isProtected)
                            {
                                ent.Remove();
                                removedTotal++;
                            }
                        }

                        totalBatches--;
                        [cite_start]// 當所有分批處理完畢後，發送語系訊息 
                        if (totalBatches == 0 && removedTotal > 0)
                        {
                            // 伺服器後台日誌 (對應 fps.console_log)
                            Console.WriteLine(Localizer["fps.console_log", context, removedTotal]);
                            
                            // 全服玩家提示 (對應 fps.cleanup_done)
                            Server.PrintToChatAll($" {ChatColors.Red}[SmoothWizard]{ChatColors.Default} " + Localizer["fps.cleanup_done", removedTotal]);
                        }
                    });
                }
            }
        }
    }
}
