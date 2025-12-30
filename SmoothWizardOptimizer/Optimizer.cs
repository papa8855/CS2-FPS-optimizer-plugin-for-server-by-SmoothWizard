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

namespace SmoothWizardOptimizer
{
    public class SmoothWizardOptimizer : BasePlugin
    {
        public override string ModuleName => "SmoothWizard Server Optimizer";
        public override string ModuleVersion => "1.0.8";
        public override string ModuleAuthor => "SkullMedia & Optimized";

        private bool isOptimizationEnabled = true;
        private DateTime _lastRunTime = DateTime.MinValue; // 用於防止短時間內重複執行

        private readonly string[] entitiesToCleanup =
        {
           "cs_ragdoll", "env_explosion", "env_fire", "env_spark", "env_smokestack",
           "info_particle_system", "particle_*", "decals_*", "prop_physics_multiplayer", "prop_physics"
        };

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundStart>((ev, info) =>
            {
                // 如果距離上次執行不到 5 秒，則跳過 (防止重複觸發)
                if (isOptimizationEnabled && (DateTime.Now - _lastRunTime).TotalSeconds > 5)
                {
                    _lastRunTime = DateTime.Now;
                    CleanupMapAndTempEntities("RoundStart");
                }
                return HookResult.Continue;
            });

            AddCommand("css_sw_toggle", "切換優化系統開關", (player, info) => {
                isOptimizationEnabled = !isOptimizationEnabled;
                string status = isOptimizationEnabled ? $"{ChatColors.Green}開啟" : $"{ChatColors.Red}關閉";
                info.ReplyToCommand($" {ChatColors.Red}優化系統{ChatColors.White} 已 {status}");
            });

            Server.PrintToConsole($"[SmoothWizard] 插件已載入。目前狀態: {(isOptimizationEnabled ? "開啟" : "關閉")}");
        }

        private void CleanupMapAndTempEntities(string context)
        {
            int removedTotal = 0;
            List<CEntityInstance> allPending = new List<CEntityInstance>();

            foreach (var pattern in entitiesToCleanup)
            {
                var found = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern);
                if (found != null) allPending.AddRange(found);
            }

            if (allPending.Count == 0) return;

            var chunks = allPending.Chunk(50).ToList();
            int totalBatches = chunks.Count;

            foreach (var batch in chunks)
            {
                Server.NextFrame(() =>
                {
                    foreach (var ent in batch)
                    {
                        try
                        {
                            if (ent == null || !ent.IsValid) continue;

                            bool isProtected = ent.DesignerName.Contains("door") || ent.DesignerName.Contains("breakable") || 
                                             ent.DesignerName.StartsWith("weapon_") || ent.DesignerName.Contains("vent") ||
                                             ent.DesignerName.Contains("shield") || ent.DesignerName.Contains("movable_platform") || 
                                             ent.DesignerName.Contains("parent");

                            if (!isProtected)
                            {
                                ent.Remove();
                                removedTotal++;
                            }
                        }
                        catch (Exception) { }
                    }

                    totalBatches--;

                    if (totalBatches == 0 && removedTotal > 0)
                    {
                        Console.WriteLine($"[系統優化] [{context}] 任務執行完畢。總共清理實體：{removedTotal}");
                        Server.PrintToChatAll($" {ChatColors.Green}[ 優化系統 ]{ChatColors.White} 任務執行完畢，總共清理實體：{ChatColors.LightRed}{removedTotal}{ChatColors.White}。");
                    }
                });
            }
        }
    }
}
