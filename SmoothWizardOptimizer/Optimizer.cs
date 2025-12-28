using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers; 
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization; // 必須加入語系引用

namespace SmoothWizardOptimizer
{
    public class SmoothWizardOptimizer : BasePlugin
    {
        public override string ModuleName => "SmoothWizard Server Optimizer";
        public override string ModuleVersion => "1.0.2";
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

            // 註冊切換指令
            AddCommand("css_sw_toggle", "切換優化器狀態", OnToggleOptimizerCommand);

            Server.PrintToConsole($"[SmoothWizard] Plugin Loaded. Status: {isOptimizationEnabled}");
        }

        private void OnToggleOptimizerCommand(CCSPlayerController? player, CommandInfo info)
        {
            isOptimizationEnabled = !isOptimizationEnabled;
            
            [cite_start]// 使用 Localizer 讀取翻譯 
            string status = isOptimizationEnabled ? Localizer["fps.enabled"] : Localizer["fps.disabled"];
            string chatMessage = $" {ChatColors.Red}[SmoothWizard]{ChatColors.White} " + Localizer["fps.toggle_status", status];
            
            info.ReplyToCommand(chatMessage);
        }

        private void CleanupMapAndTempEntities(string context)
        {
            int removedTotal = 0;
            int totalBatches = 0;

            foreach (var pattern in entitiesToCleanup)
            {
                var entities = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern).ToList();

                [cite_start]// 官方的分批處理邏輯 
                foreach (var batch in entities.Chunk(50))
                {
                    totalBatches++;
                    Server.NextFrame(() =>
                    {
                        foreach (var ent in batch)
                        {
                            if (ent == null || !ent.IsValid) continue;

                            bool isProtected = ent.DesignerName.Contains("door") || ent.DesignerName.Contains("breakable") || 
                                             ent.DesignerName.StartsWith("weapon_") || ent.DesignerName.Contains("vent");

                            if (!isProtected)
                            {
                                ent.Remove();
                                removedTotal++;
                            }
                        }

                        totalBatches--;
                        if (totalBatches == 0 && removedTotal > 0)
                        {
                            // 使用語系檔輸出清理訊息
                            Server.PrintToChatAll($" {ChatColors.Red}[SmoothWizard]{ChatColors.Default} " + Localizer["fps.cleanup_done", removedTotal]);
                        }
                    });
                }
            }
        }
    }
}
