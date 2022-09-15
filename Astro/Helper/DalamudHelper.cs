#nullable enable
using System;
using System.Reactive.Disposables;
using Dalamud;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Astro.Helper
{
    public static class DalamudHelper
    {
        public static PlayerCharacter? LocalPlayer => DalamudApi.ClientState.LocalPlayer;
        
        public static unsafe bool AddQueueAction(uint actionId, uint targetId) => AddQueueAction((IntPtr)ActionManager.Instance(), ActionType.Spell, actionId, targetId, 0);

        private static bool AddQueueAction(IntPtr actionManager, ActionType actionType, uint actionId, uint targetId, uint param) 
        {
            SafeMemory.Read<bool>(actionManager + 0x68, out var inQueue);
            if (!inQueue)
                return false;

            SafeMemory.Write(actionManager + 0x68, true);
            SafeMemory.Write(actionManager + 0x6C, (byte)actionType);
            SafeMemory.Write(actionManager + 0x70, actionId);
            SafeMemory.Write(actionManager + 0x78, targetId);
            SafeMemory.Write(actionManager + 0x80, 0);
            SafeMemory.Write(actionManager + 0x84, param);
            return true;
        }
        
        public static unsafe float GetActionRecast(uint actionId, int chargeTime)
        {
            var recast = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, actionId);
            return recast == 0 ? 0 : chargeTime - recast;
        }

        public static unsafe double GetActionChargeCount(uint actionId, int maxChargeCount, int chargeTime)
        {
            var recast = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, actionId);
            return Math.Round(recast == 0 ? maxChargeCount : recast / chargeTime, 1);
        }

        public static void RegisterCommand(string cmdName, string helpMessage, CommandInfo.HandlerDelegate cmdHandler)
        {
            var cmdInfo = new CommandInfo(cmdHandler)
            {
                HelpMessage = helpMessage
            };
            DalamudApi.CommandManager.AddHandler(cmdName, cmdInfo);
        }
        
        public static void AddTo(this IDisposable disposable, CompositeDisposable compositeDisposable) => compositeDisposable.Add(disposable);
    }
}