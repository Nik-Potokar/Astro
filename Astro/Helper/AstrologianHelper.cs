using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace Astro.Helper
{
    public static class AstrologianHelper
    {
        [Flags]
        private enum ArcanumType
        {
            Melee = 1 << 0,
            Range = 1 << 1,
            Burst = 1 << 2,
            MiniBurst = 1 << 3,
        }
        
        public static unsafe AstrologianCard CurrentCard =>
            JobGaugeManager.Instance()->Astrologian.CurrentCard & ~AstrologianCard.Lord & ~AstrologianCard.Lady;

        public static unsafe bool IsAstroSignFilled =>
            JobGaugeManager.Instance()->Astrologian.CurrentSeals.All(x => x != 0);

        public static unsafe bool IsAstroSignDuplicated =>
            JobGaugeManager.Instance()->Astrologian.CurrentSeals.Any(seal => Seals[CurrentCard] == seal);

        public static bool IsDivinationCloseToReady => 
            DalamudHelper.GetActionRecast(Divination, 120) <= DalamudApi.Configuration.BurstRange;

        public static bool IsDrawCloseToReady =>
            DalamudHelper.GetActionRecast(Draw, 60) <= DalamudApi.Configuration.MiniBurstRange;
        
        public static bool IsRedrawInStatusList => 
            DalamudHelper.LocalPlayer!.StatusList.Any(x => x.StatusId == RedrawExecutableInStatus);
        
        public static bool IsDivinationInStatusList => 
            DalamudHelper.LocalPlayer!.StatusList.Any(x => x.StatusId == DivinationInStatus);

        public const uint Redraw = 3593, Play = 17055, Draw = 3590;
        private const uint Divination = 16552;
        private const uint RedrawExecutableInStatus = 2713, DivinationInStatus = 1878;

        private static readonly Dictionary<AstrologianCard, AstrologianSeal> Seals = new()
        {
            { AstrologianCard.Balance, AstrologianSeal.Solar }, { AstrologianCard.Bole, AstrologianSeal.Solar },
            { AstrologianCard.Arrow, AstrologianSeal.Lunar }, { AstrologianCard.Ewer, AstrologianSeal.Lunar },
            { AstrologianCard.Spear, AstrologianSeal.Celestial }, { AstrologianCard.Spire, AstrologianSeal.Celestial },
        };

        private static readonly Dictionary<ArcanumType, List<string>> Weights = new()
        {
            { ArcanumType.Melee, DalamudApi.Configuration.MeleePriority },
            { ArcanumType.Range, DalamudApi.Configuration.RangePriority },
            { ArcanumType.Melee | ArcanumType.MiniBurst, DalamudApi.Configuration.MeleeMiniBurstPriority },
            { ArcanumType.Range | ArcanumType.MiniBurst, DalamudApi.Configuration.RangeMiniBurstPriority },
            { ArcanumType.Melee | ArcanumType.Burst, DalamudApi.Configuration.MeleeBurstPriority },
            { ArcanumType.Range | ArcanumType.Burst, DalamudApi.Configuration.RangeBurstPriority }
        };
        
        private static readonly Random Random = new();
        private const uint Weakness = 43, BrinkOfDeath = 44;

        public static uint GetActionId(AstrologianCard card)
        {
            return card switch
            {
                AstrologianCard.Balance => 4401,
                AstrologianCard.Bole => 4404,
                AstrologianCard.Arrow => 4402,
                AstrologianCard.Spear => 4403,
                AstrologianCard.Ewer => 4405,
                AstrologianCard.Spire => 4406,
                _ => 0
            };
        }

        public static uint GetOptimumTargetId()
        {
            if (DalamudApi.PartyList.Length == 0)
                return DalamudApi.ClientState.LocalPlayer!.ObjectId;
            
            var cardType = GetCardType(CurrentCard);
            if (IsDivinationInStatusList || IsDivinationCloseToReady)
                cardType |= ArcanumType.Burst;
            else if (IsDrawCloseToReady && !IsDivinationCloseToReady)
                cardType |= ArcanumType.MiniBurst;

            if(DalamudApi.Configuration.ShowDebugMessage)
                PluginLog.Log($"Card({CurrentCard}) => {cardType:F}");

            for (var i = 0; i < 2; i++)
            {
                var member = DalamudApi.PartyList
                    .Where(x => cardType.HasFlag(GetCardType(x.ClassJob.GameData?.Role ?? 0)))
                    .Where(x => Weights[cardType].Exists(y => y == x.ClassJob.GameData.Abbreviation))
                    .Where(x => !x.Statuses.Any(y => y.StatusId is >= 1882 and <= 1887 or Weakness or BrinkOfDeath))
                    .Where(x => x.Statuses.Any(y => y.GameData.Name != DamageDownString()))
                    .OrderBy(x => Weights[cardType].IndexOf(x.ClassJob.GameData?.Abbreviation))
                    .FirstOrDefault();

                if (member != null)
                {
                    if(DalamudApi.Configuration.ShowDebugMessage)
                        PluginLog.Log($"Play({CurrentCard}) => {cardType:F} => {member.ClassJob.GameData?.Abbreviation ?? "none"}");

                    return member.ObjectId;
                }

                if (cardType.HasFlag(ArcanumType.Melee))
                {
                    cardType &= ~ArcanumType.Melee;
                    cardType |= ArcanumType.Range;
                } 
                else if (cardType.HasFlag(ArcanumType.Range))
                {
                    cardType &= ~ArcanumType.Range;
                    cardType |= ArcanumType.Melee;
                }
                
                if(DalamudApi.Configuration.ShowDebugMessage)
                    PluginLog.Log($"Turn it over => {cardType:F}");
            }

            var random = DalamudApi.PartyList[Random.Next(DalamudApi.PartyList.Length)]!;
            
            if (DalamudApi.Configuration.ShowDebugMessage)
                PluginLog.Log($"Random({CurrentCard}) => {cardType:F} => {random.ClassJob.GameData?.Abbreviation ?? "none"}");
            
            return random.ObjectId;
        }

        private static ArcanumType GetCardType(byte role)
        {
            return role switch
            {
                1 => ArcanumType.Melee,
                2 => ArcanumType.Melee,
                3 => ArcanumType.Range,
                4 => ArcanumType.Range,
                _ => ArcanumType.Range
            };
        }

        private static string DamageDownString()
        {
            switch (DalamudApi.ClientState.ClientLanguage)
            {
                case ClientLanguage.Japanese:
                    return "ダメージ低下";
                case ClientLanguage.French:
                case ClientLanguage.German:
                case ClientLanguage.English:
                    return "Damage Down";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static ArcanumType GetCardType(AstrologianCard arcanum)
        {
            return arcanum switch
            {
                AstrologianCard.Balance or AstrologianCard.Arrow or AstrologianCard.Spear => ArcanumType.Melee,
                AstrologianCard.Bole or AstrologianCard.Ewer or AstrologianCard.Spire => ArcanumType.Range,
                _ => throw new ArgumentOutOfRangeException(nameof(arcanum), arcanum, null)
            };
        }

        private static unsafe float RecastTimeElapsed(uint actionId) => ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Spell, actionId);
    }
}