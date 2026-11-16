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
