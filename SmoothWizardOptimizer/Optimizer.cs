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
        public override string ModuleVersion => "1.0.7";
        public override string ModuleAuthor => "SkullMedia & Optimized";

        private bool isOptimizationEnabled = true;

        private readonly string[] entitiesToCleanup =
        {
           "cs_ragdoll", "env_explosion", "env_fire", "env_spark", "env_smokestack",
           "info_particle_system", "particle_*", "decals_*", "prop_physics_multiplayer", "prop_physics"
        };

        private readonly bool debugMode = false;
        private bool clearLoopActive = false;
        private CounterStrikeSharp.API.Modules.Timers.Timer? debugTimer = null;
        private Queue<CEntityInstance> entitiesToClearQueue = new Queue<CEntityInstance>();

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            AddCommand("css_sw_toggle", "切換優化系統開關", OnToggleOptimizerCommand);
            AddCommand("css_sw_clear", "開始清理偵錯循環", OnClearEntitiesLoopCommand);
            AddCommand("css_sw_stop", "停止清理偵錯循環", OnStopClearLoopCommand);

            Server.PrintToConsole($"[SmoothWizard] 插件已載入。目前狀態: {(isOptimizationEnabled ? "開啟" : "關閉")}");
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
            string status = isOptimizationEnabled ? $"{ChatColors.Green}開啟" : $"{ChatColors.Red}關閉";
            string chatMessage = $" {ChatColors.Red}優化系統{ChatColors.White} 已 {status}";
            info.ReplyToCommand(chatMessage);
        }

        private void OnStopClearLoopCommand(CCSPlayerController? player, CommandInfo info)
        {
            clearLoopActive = false;
            debugTimer?.Kill();
            debugTimer = null;
            entitiesToClearQueue.Clear();
            info.ReplyToCommand("已停止實體清理偵錯循環。");
        }

        private void OnClearEntitiesLoopCommand(CCSPlayerController? player, CommandInfo info)
        {
            if (clearLoopActive)
            {
                info.ReplyToCommand("循環已經在運行中。");
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
                info.ReplyToCommand("未發現可清理的實體。");
                return;
            }

            clearLoopActive = true;
            info.ReplyToCommand($" {ChatColors.Red}SmoothWizard{ChatColors.White} 開始清理 {entitiesToClearQueue.Count} 個實體。");
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
                Server.PrintToChatAll($" {ChatColors.Red}SmoothWizard{ChatColors.White} 實體清理循環結束。");
                return;
            }

            var ent = entitiesToClearQueue.Dequeue();
            if (ent == null || !ent.IsValid) return;

            Server.NextFrame(() =>
            {
                try { ent.Remove(); }
                catch (Exception) { }
            });
        }

        private void CleanupMapAndTempEntities(string context)
        {
            int removedTotal = 0;
            int totalBatches = 0;
            List<CEntityInstance> allPending = new List<CEntityInstance>();

            // 修正點：先將所有符合條件的實體彙整，不分開處理
            foreach (var pattern in entitiesToCleanup)
            {
                var found = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(pattern);
                if (found != null) allPending.AddRange(found);
            }

            if (allPending.Count == 0) return;

            // 修正點：針對總清單進行分批，確保 totalBatches 只會歸零一次
            var chunks = allPending.Chunk(50).ToList();
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
                        catch (Exception ex)
                        {
                            Server.PrintToConsole($"[SmoothWizard] 錯誤: {ex.Message}");
                        }
                    }

                    totalBatches--;

                    // 只有當所有批次都處理完畢時才輸出結算訊息
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
