using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Astro.Helper;
using Dalamud;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.IoC;
using Dalamud.Plugin;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace Astro
{
    public unsafe class Astro : IDalamudPlugin
    {
        private static class Functions
        {
            internal delegate void ReceiveAbility(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
            internal delegate bool TryAction(IntPtr actionManager, ActionType actionType, uint actionId, ulong targetId, uint param, uint origin, uint unknown, void* location);
        }

        string IDalamudPlugin.Name => "Astro";
        private const string CommandName = "/astro";
        private readonly Subject<Unit> abilitySubject = new();
        private readonly CompositeDisposable compositeDisposable = new();

        public Astro([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(pluginInterface);
            Resolver.Initialize();
            
            HookHelper.Enable<Functions.ReceiveAbility>("4C 89 44 24 ?? 55 56 41 54 41 55 41 56", ReceiveAbilityDetour);
            HookHelper.Enable<Functions.TryAction>((IntPtr)ActionManager.fpUseAction, TryActionDetour);
            
            DalamudApi.Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            DalamudApi.Configuration.Init();

            var parent = abilitySubject
                .Where(_ => DalamudApi.Configuration.AstroStatus)
                .Where(_ => DalamudHelper.LocalPlayer?.ClassJob.GameData?.Abbreviation == "AST")
                .Where(_ => DalamudHelper.LocalPlayer!.StatusFlags.HasFlag(StatusFlags.InCombat))
                .Where(_ => !AstrologianHelper.IsAstroSignFilled && AstrologianHelper.CurrentCard is not AstrologianCard.None)
                .Where(_ =>
                {
                    SafeMemory.Read((IntPtr)ActionManager.Instance() + 0x61C, out float totalGcd);
                    SafeMemory.Read((IntPtr)ActionManager.Instance() + 0x618, out float elapsedGcd);
                    return !(totalGcd - elapsedGcd <= 1.3f);
                })
                .Publish()
                .RefCount();

            parent
                .Where(_ => DalamudApi.Configuration.EnableAutoRedraw)
                .Where(_ => AstrologianHelper.IsRedrawInStatusList)
                .Where(_ => AstrologianHelper.IsAstroSignDuplicated)
                .Subscribe(_ => DalamudHelper.AddQueueAction(AstrologianHelper.Redraw, DalamudApi.TargetManager.Target?.ObjectId ?? 0))
                .AddTo(compositeDisposable);
            
            Observable
                .Merge
                (
                    parent
                        .Where(_ => DalamudApi.Configuration.EnableAutoPlay),
                    parent
                        .Where(_ => DalamudApi.Configuration.EnableBurstCard)
                        .Where(_ => AstrologianHelper.IsDivinationInStatusList),
                    parent
                        .Where(_ => DalamudApi.Configuration.EnableNSecBeforeBurst)
                        .Where(_ => AstrologianHelper.IsDivinationCloseToReady),
                    parent
                        .Where(_ => DalamudApi.Configuration.EnableNSecMiniBurst)
                        .Where(_ => AstrologianHelper.IsDrawCloseToReady)
                )
                .Subscribe(_ => DalamudHelper.AddQueueAction(AstrologianHelper.GetActionId(AstrologianHelper.CurrentCard), AstrologianHelper.GetOptimumTargetId()))
                .AddTo(compositeDisposable);

            IUi ui = new Ui();
            DalamudApi.PluginInterface.UiBuilder.Draw += ui.Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += () => ui.Visible = true;
            DalamudHelper.RegisterCommand(CommandName, "Open config window for Astro.\n/astro on → Enable Astro\n/astro off → Disable Astro\n/astro toggle → Toggle Astro status", (_, arguments) =>
            {
                if (arguments == "") ui.Visible = true;
                DalamudApi.Configuration.AstroStatus = arguments switch
                {
                    "off" => false,
                    "on" => true,
                    "toggle" => !DalamudApi.Configuration.AstroStatus,
                    _ => DalamudApi.Configuration.AstroStatus,
                };
            });
        }

        private void ReceiveAbilityDetour(uint sourceId, IntPtr sourceCharacter, IntPtr position, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
        {
            HookHelper.Get<Functions.ReceiveAbility>()(sourceId, sourceCharacter, position, effectHeader, effectArray, effectTrail);
            abilitySubject.OnNext(Unit.Default); 
        }
        
        private static bool TryActionDetour(IntPtr actionManager, ActionType actionType, uint actionId, ulong targetId, uint param, uint origin, uint unknown, void* location)
        {
            var tryAction = HookHelper.Get<Functions.TryAction>();

            if (DalamudHelper.LocalPlayer?.ClassJob.GameData?.Abbreviation != "AST" || !DalamudHelper.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat))
                return tryAction(actionManager, actionType, actionId, targetId, param, origin, unknown, location);
            
            if (DalamudApi.Configuration.EnableManualRedraw && AstrologianHelper.IsRedrawInStatusList && AstrologianHelper.IsAstroSignDuplicated)
                return tryAction(actionManager, actionType, AstrologianHelper.Redraw, targetId, param, origin, unknown, location);
            
            if (actionId != AstrologianHelper.Play || AstrologianHelper.CurrentCard is AstrologianCard.None)
                return tryAction(actionManager, actionType, actionId, targetId, param, origin, unknown, location);

            if (!DalamudApi.Configuration.EnableManualPlay)
                return tryAction(actionManager, actionType, actionId, targetId, param, origin, unknown, location);
            
            var cardId = AstrologianHelper.GetActionId(AstrologianHelper.CurrentCard);
            var optimumTargetId = AstrologianHelper.GetOptimumTargetId();
            return tryAction(actionManager, actionType, cardId, optimumTargetId, param, origin, unknown, location);
        }

        void IDisposable.Dispose()
        {
            DalamudApi.CommandManager.RemoveHandler(CommandName);
            HookHelper.Disable<Functions.ReceiveAbility>();
            HookHelper.Disable<Functions.TryAction>();
            abilitySubject.Dispose();
            compositeDisposable.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
