namespace Hearthline.Sim
{
    /// <summary>Something that happened in the simulation. Presentation reacts to these; it never causes them directly.</summary>
    public abstract class DomainEvent
    {
        /// <summary>The day the thing happened (can be earlier than the current day after a time skip).</summary>
        public int Day;
    }

    public sealed class MarkAdded : DomainEvent { public int CharacterId; public int MarkId; public string DefId; }
    public sealed class MarkRemoved : DomainEvent { public int CharacterId; public int MarkId; public string DefId; }
    public sealed class MarkStageChanged : DomainEvent { public int CharacterId; public int MarkId; public string FromStage; public string ToStage; }
    /// <summary>A mark became a different kind of mark (a wound became a scar or a loss). The mark id is unchanged.</summary>
    public sealed class MarkTransformed : DomainEvent { public int CharacterId; public int MarkId; public string FromDefId; public string ToDefId; public string Outcome; }
    public sealed class WoundTreated : DomainEvent { public int CharacterId; public int MarkId; public bool AtInfirmary; public string Outcome; }
    public sealed class DateAdvanced : DomainEvent { public int FromDay; public int ToDay; }
    public sealed class SeasonPassed : DomainEvent { public Season Ended; public Season Began; }
    public sealed class CharacterAgeSet : DomainEvent { public int CharacterId; public int AgeYears; }
    /// <summary>The family did something that will move a settlement value when the season turns.</summary>
    public sealed class SettlementInfluenced : DomainEvent { public string SettlementId; public SettlementValue Value; public int Delta; }
    public sealed class SettlementValueChanged : DomainEvent { public string SettlementId; public SettlementValue Value; public int OldValue; public int NewValue; }
    public sealed class ServiceOpened : DomainEvent { public string SettlementId; public string ServiceId; }
    public sealed class ServiceClosed : DomainEvent { public string SettlementId; public string ServiceId; }
    public sealed class ResidentSucceeded : DomainEvent { public string SettlementId; public string Role; public int PreviousId; public int NewId; }
    public sealed class GrudgeChanged : DomainEvent { public string FactionId; public bool Held; public string Reason; }
    public sealed class TripStarted : DomainEvent { public System.Collections.Generic.List<int> PartyIds; }
    public sealed class TripEnded : DomainEvent { public int DaysAway; public bool PackLost; }
    public sealed class ItemsSpoiled : DomainEvent { public string ItemId; public int Count; public bool InPack; }
    public sealed class StructureChanged : DomainEvent { public string StructureId; public int OldTier; public int NewTier; }
    public sealed class JobAssigned : DomainEvent { public int CharacterId; public string JobId; }
    public sealed class MoraleChanged : DomainEvent { public int OldValue; public int NewValue; }
    /// <summary>The season's account of the home: who did what, who went hungry. Show it when the head returns.</summary>
    public sealed class HomeReported : DomainEvent { public System.Collections.Generic.List<HomeReportLine> Lines; public int HungryMembers; public int Morale; }
    public sealed class CharacterLeft : DomainEvent { public int CharacterId; public string StolenItemId; }
    public sealed class EventStarted : DomainEvent { public string EventId; }
    public sealed class EventBeatEntered : DomainEvent { public string EventId; public string BeatId; }
    public sealed class EventChoiceMade : DomainEvent { public string EventId; public string BeatId; public int ChoiceIndex; }
    public sealed class EventEnded : DomainEvent { public string EventId; }
    /// <summary>Presentation should stage this fight. The sim does not resolve combat.</summary>
    public sealed class AmbushRequested : DomainEvent { public string EncounterId; public string EventId; }
    public sealed class ReputationChanged : DomainEvent { public string FactionId; public int Delta; public int NewValue; }
    public sealed class InventoryChanged : DomainEvent { public string ItemId; public int Delta; public int NewCount; }
    public sealed class OpinionChanged : DomainEvent { public int CharacterId; public int AboutCharacterId; public int Delta; public int NewValue; }
    public sealed class FlagChanged : DomainEvent { public string Flag; public bool Set; }
    public sealed class CharacterDowned : DomainEvent { public int CharacterId; }
    public sealed class CharacterRevived : DomainEvent { public int CharacterId; }
    public sealed class CharacterJoined : DomainEvent { public int CharacterId; }
    public sealed class CharacterBorn : DomainEvent { public int CharacterId; public int ParentAId; public int ParentBId; }
    public sealed class PartnersJoined : DomainEvent { public int CharacterAId; public int CharacterBId; }
    public sealed class TraitChanged : DomainEvent { public int CharacterId; public string TraitId; public bool Gained; }
    public sealed class HeadRetired : DomainEvent { public int CharacterId; }
    public sealed class HeadSucceeded : DomainEvent { public int NewHeadId; public int? PreviousHeadId; }
    /// <summary>The head died and nobody can follow. The campaign ends with the chronicle.</summary>
    public sealed class LineEnded : DomainEvent { public int LastHeadId; }
    public sealed class CharacterDied : DomainEvent { public int CharacterId; public string Cause; public bool WasHeadOfHousehold; }
}
