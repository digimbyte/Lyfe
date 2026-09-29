using System;

namespace Syms.Simulation.People
{
    public enum Conduct
    {
        Disciplined,
        Conventional,
        Flexible,
        Rebellious
    }

    public enum Concern
    {
        Altruistic,
        Communal,
        Pragmatic,
        SelfServing
    }

    public enum PersonalityType
    {
        DisciplinedAltruistic,
        DisciplinedCommunal,
        DisciplinedPragmatic,
        DisciplinedSelfServing,
        ConventionalAltruistic,
        ConventionalCommunal,
        ConventionalPragmatic,
        ConventionalSelfServing,
        FlexibleAltruistic,
        FlexibleCommunal,
        FlexiblePragmatic,
        FlexibleSelfServing,
        RebelliousAltruistic,
        RebelliousCommunal,
        RebelliousPragmatic,
        RebelliousSelfServing
    }

    [Serializable]
    public sealed class Personality
    {
        public Conduct Conduct = Conduct.Conventional;
        public Concern Concern = Concern.Communal;

        public PersonalityType Type
        {
            get { return (PersonalityType)(((int)Conduct * 4) + (int)Concern); }
        }
    }
}

