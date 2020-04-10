class Clan:
    def CalculateSettlementValue(kingdom):
        """
        This is a sum of how valuable each settlement is, where value is defined as basically the sqrt
        of the average distance to the 6 nearest settlements of the same faction vs the average distance
        between settlements on the map, multiplied by a proportional term ~ the prosperity of the settlement.
        Note that the prosperity is arbitrarily increased to match the kingdom's culture.
        """
        return sum([CalculateValueForFaction(settlement, kingdom) for settlement in self.Settlements])

def GetScoreOfClanToJoinKingdom(clan, kingdom):
    if (clan.Kingdom is not None and clan.Kingdom.RulingClan == clan)
        return float('-inf')

    relationBetweenClans = GetRelationBetweenClans(kingdom.RulingClan, clan)
    hostilityMult = -0.06 if relationBetweenClans < 0 else 0.04
    relationMult = min(2.0, max(0.5, 1.0 + sqrt(abs(relationBetweenClans)) * hostilityMult))
    sameCultureMult = 1.15 if kingdom.Culture == clan.Culture else 0.85
    settlementValueBeforeJoiningKingdom = clan.CalculateSettlementValue(None)
    settlementValueAfterJoiningKingdom = clan.CalculateSettlementValue(kingdom)
    numClanHeroes = len(clan.CommanderHeroes)
    hypotheticalFortificationValuePerHero = 0
    tooManyExistingHeroesPenalty = 0

    if not clan.IsMinorFaction:
        totalFortificationValueOwnedByKingdom = 0
        for settlement in Settlement.All:
            if settlement.IsFortification and settlement.MapFaction == kingdom:
                totalFortificationValueOwnedByKingdom += CalculateValueForFaction(settlement, kingdom)
        existingHeroesInKingdom = 0
        for existingClan in kingdom.Clans:
            if not existingClan.IsMinorFaction or existingClan == Clan.PlayerClan:
                existingHeroesInKingdom += len(existingClan.CommanderHeroes)
        hypotheticalFortificationValuePerHero = totalFortificationValueOwnedByKingdom / (existingHeroesInKingdom + numClanHeroes)
        tooManyExistingHeroesPenalty = ((existingHeroesInKingdom * existingHeroesInKingdom) * 50.0)
    
    # Start with a basis of 0.
    value = 0

    # Add value for the possibility of getting slices of the pie available in the kingdom.
    value += hypotheticalFortificationValuePerHero * sqrt(numClanHeroes) * 0.300000011920929

    # Increase the value for happier relations.
    value *= relationMult

    # Increase the value for the same culture.
    value *= sameCultureMult

    # Subtract value based on how the settlement values change when you leave your current kingdom.
    # NOTE(dvd): I think this is a NOP?
    value += settlementValueAfterJoiningKingdom - settlementValueBeforeJoiningKingdom

    # Apply a penalty ~ to the number of heroes already in the kingdom.
    value -= tooManyExistingHeroesPenalty

    # Result: lords defect to the weakest, smallest factions _if_ they have land available.
    return value

def GetScoreOfClanToLeaveKingdom(clan, kingdom):
    relationBetweenClans = GetRelationBetweenClans(kingdom.RulingClan, clan)
    hostilityMult = -0.06 if relationBetweenClans < 0 else 0.04
    relationMult = min(2.0, max(0.5, 1.0 + sqrt(abs(relationBetweenClans)) * hostilityMult))
    sameCultureMult = 1.15 if kingdom.Culture == clan.Culture else 0.85
    settlementValueAfterLeavingKingdom = clan.CalculateSettlementValue(None)
    settlementValueBeforeLeavingKingdom = clan.CalculateSettlementValue(kingdom)
    numClanHeroes = len(clan.CommanderHeroes)
    hypotheticalFortificationValuePerHero = 0

    if not clan.IsMinorFaction:
        totalFortificationValueOwnedByKingdom = 0
        for fortification in kingdom.Fortifications:
            totalFortificationValueOwnedByKingdom += CalculateValueForFaction(fortification.Owner.Settlement, kingdom)
        remainingHeroesInKingdom = 0
        for remainingClan in kingdom.Clans:
            if not remainingClan.IsMinorFaction or remainingClan == Clan.PlayerClan:
                remainingHeroesInKingdom += len(remainingClan.CommanderHeroes)
        hypotheticalFortificationValuePerHero = totalFortificationValueOwnedByKingdom / (remainingHeroesInKingdom + numClanHeroes)
    
    clanStrength = ((clan.TotalStrength + 150.0 * numClanHeroes) * 10.0)
    reliabilityConstant = CalculateReliabilityConstant(clan.Leader, 1)
    kingdomSwapPenalty = 2000 * (10.0 - sqrt(min(100.0, (CampaignTime.Now - clan.LastFactionChangeTime).ToDays)))
    townMult = 40000 + len(clan.Towns) * 20000

    # Start with a basis of 0.
    value = 0

    # Apply a penalty for losing the possibility of getting slices of the pie available in the kingdom.
    value -= hypotheticalFortificationValuePerHero * sqrt(numClanHeroes) * 0.300000011920929

    # Apply a penalty ~ number of towns and honor of the lord.
    value -= townMult * reliabilityConstant

    # Apply a penalty ~ the clan strength and honor of the lord.
    value -= clanStrength * reliabilityConstant

    # Apply a penalty for swapping kingdoms too often.
    value -= kingdomSwapPenalty

    # Increase the value for happier relations.
    value *= relationMult

    # Increase the value for the same culture.
    value *= sameCultureMult

    # Add value based on how the settlement values change when you leave your current kingdom.
    value += settlementValueAfterLeavingKingdom - settlementValueBeforeLeavingKingdom

    # Result: lords want to defect when they are dishonorable, there are lots of heroes in their existing kingdom, they haven't defected recently.
    return value

def GetPowerRatioToEnemies(kingdom):
    """ Returns kingdom.Strength / kingdom.Enemies.Strength """
    pass

def CalculateReliabilityConstant(leader):
    """ Measures how honorable the leader is. """
    pass

def GetScoreOfKingdomToGetClan(kingdom, clan):
    relationMult = min(2, max(0.33, (1.0 + 0.0199999995529652 * GetRelationBetweenClans(kingdom.RulingClan, clan))))
    sameCultureMult = 2 if kingdom.Culture == clan.Culture else 1
    numClanHeroes =  len(clan.CommanderHeroes)
    clanStrength = (clan.TotalStrength + 150.0 * numClanHeroes) * 10.0
    powerRatioToEnemies = GetPowerRatioToEnemies(kingdom)
    reliabilityConstant = CalculateReliabilityConstant(clan.Leader, 1)
    howBadlyDoesThisKingdomNeedSupport = 1 / max(0.4, min(2.5, sqrt(powerRatioToEnemies)))
    adjustedClanStrength = clanStrength * howBadlyDoesThisKingdomNeedSupport

    # Start with a basis of 0.
    value = 0

    # Add value for the settlements the clan will bring to the kingdom, and the adjusted clan strength.
    # Adjusted clan strength is the clan strength ~ how badly the kingdom needs allies.
    value += clan.CalculateSettlementValue(kingdom) * 0.1 + adjustedClanStrength

    # Increase the value for happier relations.
    value *= relationMult

    # Increase the value for the same culture.
    value *= sameCultureMult

    # Increase the value if the lord of the clan is known to be honorable.
    value *= reliabilityConstant

    # Result: kingdoms will pay more for lords when they have enemies, and they prefer lords with more settlements, happier relations, of the same culture, and high honor.
    return value

class JoinKingdomAsClanBarterable:
    def __init__(self, owner, target): 
        self.Owner = owner
        self.Target = target

    def GetUnitValueForFaction(self, faction):
        if self.Target.IsNotKingdom:
            # The clan doesn't want to join a non-kingdom.
            return -1000000

        if faction == self.Owner:
            # Check to see how we (the clan) feel about joining the target kingdom.
            joinValue = GetScoreOfClanToJoinKingdom(self.Owner, self.Target)
            if self.Owner.Kingdom is None:
                return joinValue
            
            # Check to see how we (the clan) feel about leaving our current kingdom.
            leaveValue = LeaveKingdomAsClanBarterable(Owner, None).GetValueForFaction(Owner)

            if not self.Target.IsAtWarWith(self.Owner.Kingdom):
                # Subtract our expectation of how much we'll lose giving up our settlements.
                leaveValue -= self.Owner.Clan.CalculateSettlementValue(self.Owner.Kingdom)
            
            # Note that if we are at war, we're taking our settlements with us.
            return joinValue + leaveValue
        else if faction == self.Target:
            # Check to see how we (the kingdom) feel about acquiring this new clan.
            return GetScoreOfKingdomToGetClan(self.Target, self.Owner)

        # This barter option is worth nothing to a party outside of
        # the owner (the clan considering the join) or the target (the
        # kingdom that would receive the new clan))
        return -1000000

class LeaveKingdomAsClanBarterable:
    def __init__(self, owner, target): 
        self.Owner = owner
        self.Target = target

    def GetUnitValueForFaction(self, faction):
        if faction == self.Owner:
            # Check to see how we (the clan) feel about leaving our current kingdom.
            if self.Owner.Clan.IsMinorFaction:
                # We (the clan) are a mercenary.
                return GetScoreOfMercenaryToLeaveKingdom(self.Owner.Clan, self.Owner.Kingdom)
            # We (the clan) are a vassal.
            return GetScoreOfClanToLeaveKingdom(self.Owner.Clan, self.Owner.Kingdom)
        else if faction == self.Owner.MapFaction:
            # Check to see how we (a kingdom) feel about some kingdom (maybe us!) losing this clan.
            # The multiplier is negative if we are losing the clan and positive if someone else is losing the clan.
            mult = -1.0 if faction == self.Owner.Clan or faction == self.Owner.Clan.Kingdom else 1.0
            if self.Owner.Clan.IsUnderMercenaryService:
                # They are a mercenary.
                return GetScoreOfMercenaryToLeaveKingdom(self.Owner.Clan, self.Owner.Kingdom) * mult
            # They are not a mercenary.
            return GetScoreOfClanToLeaveKingdom(self.Owner.Clan, self.Owner.Kingdom) * mult

        # What about someone else?
        clanStrength = GetClanStrength(self.Owner.Clan)
        if not faction.IsClan or not IsAtWarAgainstFaction(faction, self.Owner.Clan.Kingdom):
            # How do we (someone who isn't a clan OR who isn't at war with this clan's kingdom) feel about that clan's kingdom losing the clan?
            if not IsAlliedWithFaction(faction, self.Owner.Clan.Kingdom):
                # If we aren't allied with them, this is a small positive number ~ clan's strength.
                return clanStrength * 0.01
            else:
                # If we are allied, this is a negative number ~ clan's strength.
                return clanStrength * -0.5
        else:
            # We're at war with the kingdom.
            # This is a positive number ~ clan's strength.
            return clanStrength * 0.5