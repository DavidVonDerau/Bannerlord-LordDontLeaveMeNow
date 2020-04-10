using System;
using System.Runtime.CompilerServices;
using Helpers;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.GameComponents.Map;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace LordDontLeaveMeNow
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            AddModels(gameStarterObject as CampaignGameStarter);
        }

        private void AddModels(CampaignGameStarter gameStarter)
        {
            gameStarter?.AddModel(new TweakedDiplomacyModel());
        }

        private class TweakedDiplomacyModel : DefaultDiplomacyModel
        {
            public override float GetScoreOfClanToJoinKingdom(Clan clan, Kingdom kingdom)
            {
                var numClanHeroes = clan.CommanderHeroes.Count;
                var existingHeroesInKingdom = GetNumberOfHeroesInKingdom(kingdom);

                if ((clan.Kingdom != null && clan.Kingdom.RulingClan == clan) ||
                    (numClanHeroes == 0) ||
                    (existingHeroesInKingdom == 0))
                {
                    return -1E+08f;
                }

                // Start with a basis of 0.
                var valueProposition = 0.0f;

                if (!clan.IsMinorFaction)
                {
                    // Add value for the possibility of getting slices of the pie available in the kingdom.
                    // This takes into account how successful the kingdom has been at war, as a reflection
                    // on the likelihood of _keeping_ the pie.
                    var hypotheticalValuePerClanHero = GetValueOfKingdomFortifications(kingdom) / (existingHeroesInKingdom + numClanHeroes);
                    var warMult = GetWarMultForClan(clan, kingdom);
                    valueProposition += hypotheticalValuePerClanHero * (float)Math.Sqrt(numClanHeroes) * 0.300000011920929f * warMult;

                    // Increase the value for happier relations.
                    var relationMult = GetRelationMult(clan, kingdom);
                    valueProposition *= relationMult;

                    // Increase the value for the same culture.
                    var sameCultureMult = GetSameCultureMult(clan, kingdom);
                    valueProposition *= sameCultureMult;

                    // Apply a penalty ~ to the number of heroes already in the kingdom.
                    var tooManyExistingHeroesPenalty = existingHeroesInKingdom * existingHeroesInKingdom * 50.0f;
                    valueProposition -= tooManyExistingHeroesPenalty;
                }

                // Add the expected change in settlement value.
                var expectedChangeInSettlementValue = clan.CalculateSettlementValue(kingdom) - clan.CalculateSettlementValue(clan.Kingdom);
                valueProposition += expectedChangeInSettlementValue;

                return valueProposition;
            }

            public override float GetScoreOfClanToLeaveKingdom(Clan clan, Kingdom kingdom)
            {
                var numClanHeroes = clan.CommanderHeroes.Count;

                if ((kingdom.RulingClan == clan) ||
                    (numClanHeroes == 0))
                {
                    return -1E+08f;
                }

                // Start with a basis of 0.
                var valueProposition = 0.0f;

                if (!clan.IsMinorFaction)
                {
                    // Apply a penalty for losing the possibility of getting slices of pie in the kingdom.
                    // This also reflects on the kingdom's success at war as a reflection of how likely it is
                    // the pie will increase or decrease.
                    var warMult = GetWarMultForClan(clan, kingdom);
                    var existingHeroesInKingdom = GetNumberOfHeroesInKingdom(kingdom);
                    var hypotheticalValuePerClanHero = GetValueOfKingdomFortifications(kingdom) / (existingHeroesInKingdom + numClanHeroes);
                    valueProposition -= hypotheticalValuePerClanHero * (float)Math.Sqrt(numClanHeroes) * 0.300000011920929f * warMult;
                }

                // Apply a penalty ~ the number of towns and honor of the lord.
                var reliabilityConstant = HeroHelper.CalculateReliabilityConstant(clan.Leader);
                var townMult = 40000f + clan.Fortifications.Count * 20000f;
                valueProposition -= townMult * reliabilityConstant;

                // Apply a penalty ~ the strength of the clan and honor of the lord.
                var clanStrength = GetClanStrength(clan, numClanHeroes);
                valueProposition -= clanStrength * reliabilityConstant;

                // Apply a penalty ~ how recently the clan has swapped kingdoms.
                const float maxDaysForPenalty = 365;
                var timeRemainingPenalty = (maxDaysForPenalty - Math.Min(maxDaysForPenalty, (float)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays)) / maxDaysForPenalty;
                var kingdomSwapPenalty = 100000f * reliabilityConstant * timeRemainingPenalty;
                valueProposition -= kingdomSwapPenalty;

                // Increase the value for happier relations.
                var relationMult = GetRelationMult(clan, kingdom);
                valueProposition *= relationMult;

                // Increase the value for the same culture.
                var sameCultureMult = GetSameCultureMult(clan, kingdom);
                valueProposition *= sameCultureMult;

                // Add the expected change in settlement value.
                var expectedChangeInSettlementValue = clan.CalculateSettlementValue() - clan.CalculateSettlementValue(clan.Kingdom);
                valueProposition += expectedChangeInSettlementValue;

                return valueProposition;
            }

            public override float GetScoreOfKingdomToGetClan(Kingdom kingdom, Clan clan)
            {
                var numClanHeroes = clan.CommanderHeroes.Count;
                var existingHeroesInKingdom = GetNumberOfHeroesInKingdom(kingdom);

                if ((numClanHeroes == 0) ||
                    (existingHeroesInKingdom == 0))
                {
                    return -1E+08f;
                }

                // Start with a basis of 0.
                var valueProposition = 0.0f;

                // Add value for the settlements the clan will bring to the kingdom, and the adjusted clan strength.
                // Adjusted clan strength is the clan strength ~ how badly the kingdom needs allies.
                var clanStrength = GetClanStrength(clan, numClanHeroes);
                var powerRatioToEnemies = FactionHelper.GetPowerRatioToEnemies(kingdom);
                var howBadlyDoesThisKingdomNeedSupport = 1f / Clamp(powerRatioToEnemies > 1 ? (float)Math.Sqrt(powerRatioToEnemies) : powerRatioToEnemies, 0.1f, 2.5f);
                var adjustedClanStrength = clanStrength * howBadlyDoesThisKingdomNeedSupport;
                valueProposition += clan.CalculateSettlementValue(kingdom) * 0.1f + adjustedClanStrength;

                // Increase the value for happier relations.
                var relationMult = Clamp(1.0f + 0.0199999995529652f * FactionManager.GetRelationBetweenClans(kingdom.RulingClan, clan), 0.33f, 2f);
                valueProposition *= relationMult;

                // Increase the value for the same culture.
                var sameCultureMult = (float)(1.0 + (kingdom.Culture == clan.Culture ? 1.0 : 0.0));
                valueProposition *= sameCultureMult;

                // Increase the value if the lord is known to be honorable.
                var reliabilityConstant = HeroHelper.CalculateReliabilityConstant(clan.Leader);
                valueProposition *= reliabilityConstant;

                return valueProposition;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetWarMultForClan(Clan clan, Kingdom kingdom)
            {
                var campaignWars = Campaign.Current.FactionManager.FindCampaignWarsOfFaction(kingdom);
                if (campaignWars.Count == 0)
                {
                    return 1.0f;
                }

                // Start with the current raw kingdom strength. This represents total men, garrisons, etc.
                var totalKingdomScore = kingdom.TotalStrength;
                var totalOppositionScore = FactionHelper.GetTotalEnemyKingdomPower(kingdom);

                // Add additional points for renown gained, raids, and sieges in the current wars.
                foreach (var campaignWar in campaignWars)
                {
                    FindSides(campaignWar, kingdom, out var kingdomSide, out var oppositionSide);

                    var (kingdomScore, oppositionScore) = CalculateWarScore(campaignWar, kingdomSide, oppositionSide);
                    totalKingdomScore += kingdomScore;
                    totalOppositionScore += oppositionScore;
                }
                var warMult = totalKingdomScore / totalOppositionScore;

                if ((warMult < 1) && 
                    (clan.Culture == kingdom.Culture))
                {
                    // Clans do not leave their own culture when losing wars.
                    return 1.0f;
                }

                return Clamp(warMult, 0, 1.5f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void FindSides(CampaignWar campaignWar, Kingdom kingdom, out MBReadOnlyList<IFaction> kingdomSide, out MBReadOnlyList<IFaction> oppositionSide)
            {
                if (campaignWar.Side1.Contains(kingdom))
                {
                    kingdomSide = campaignWar.Side1;
                    oppositionSide = campaignWar.Side2;
                }
                else
                {
                    kingdomSide = campaignWar.Side2;
                    oppositionSide = campaignWar.Side1;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Tuple<float, float> CalculateWarScore(CampaignWar campaignWar, MBReadOnlyList<IFaction> kingdomSide, MBReadOnlyList<IFaction> oppositionSide)
            {
                var kingdomWarScore = CalculateWarScoreForSideAgainst(campaignWar, kingdomSide, oppositionSide);
                var oppositionWarScore = CalculateWarScoreForSideAgainst(campaignWar, oppositionSide, kingdomSide);
                return Tuple.Create(kingdomWarScore, oppositionWarScore);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float CalculateWarScoreForSideAgainst(CampaignWar campaignWar, MBReadOnlyList<IFaction> kingdomSide, MBReadOnlyList<IFaction> oppositionSide)
            {
                var warScore = 0.0f;

                var factionCasualties = 0;
                foreach (var faction in kingdomSide)
                {
                    // Each point of renown is 1 point.
                    var factionRenown = campaignWar.GetWarScoreOfFaction(faction);
                    warScore += factionRenown * 1f;

                    // Each raid counts as 50 points.
                    var factionRaids = campaignWar.GetSuccessfulRaidsOfFaction(faction);
                    warScore += factionRaids * 50f;

                    // Each siege counts as 300 points.
                    var factionSieges = campaignWar.GetSuccessfulSiegesOfFaction(faction);
                    warScore += factionSieges * 300f;

                    // Track, but do not add, the number of allies killed.
                    factionCasualties += campaignWar.GetCasualtiesOfFaction(faction);
                }

                // Each enemy casualty is 1 point.
                var oppositionCasualties = 0;
                foreach (var faction in oppositionSide)
                {
                    oppositionCasualties += campaignWar.GetCasualtiesOfFaction(faction);
                }
                warScore += oppositionCasualties * 1f;

                // Scale the score by the a multiplier reflecting how many
                // of the total losses are on the opponent's side.
                var totalCasualties = factionCasualties + oppositionCasualties;
                var casualtyMult = (totalCasualties > 0) ? (oppositionCasualties / (float)totalCasualties) : 1;
                warScore *= casualtyMult;

                return warScore;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Clamp(float value, float min, float max)
            {
                return Math.Min(max, Math.Max(min, value));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetRelationMult(Clan clan, Kingdom kingdom)
            {
                var relationBetweenClans = FactionManager.GetRelationBetweenClans(kingdom.RulingClan, clan);
                var relationMult = 1.0f + (float)Math.Sqrt(Math.Abs(relationBetweenClans)) * (relationBetweenClans < 0 ? -0.0599999986588955f : 0.0399999991059303f);
                return Clamp(relationMult, 0.5f, 2.0f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetSameCultureMult(Clan clan, Kingdom kingdom)
            {
                return 1.0f + (kingdom.Culture == clan.Culture ? 0.150000005960464f : -0.150000005960464f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetValueOfKingdomFortifications(Kingdom kingdom)
            {
                var fortificationValueSum = 0f;
                foreach (var fortification in kingdom.Fortifications)
                {
                    fortificationValueSum += fortification.Owner.Settlement.GetSettlementValueForFaction(kingdom);
                }
                return fortificationValueSum;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GetNumberOfHeroesInKingdom(Kingdom kingdom)
            {
                var numHeroes = 0;
                foreach (var clan in kingdom.Clans)
                {
                    if (clan.IsMinorFaction && clan != Clan.PlayerClan)
                    {
                        continue;
                    }

                    numHeroes += clan.CommanderHeroes.Count;
                }
                return numHeroes;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float GetClanStrength(Clan clan, int numClanHeroes)
            {
                return (clan.TotalStrength + 150.0f * numClanHeroes) * 10.0f;
            }
        }
    }
}
