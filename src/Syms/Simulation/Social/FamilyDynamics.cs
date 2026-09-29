using System;
using System.Collections.Generic;
using Syms.Simulation.People;

namespace Syms.Simulation.Social
{
    public enum RelationshipTone
    {
        Hostile,
        Bad,
        Neutral,
        Good,
        Devoted
    }

    [Serializable]
    public sealed class SocialBiasRule
    {
        public string Id;
        public bool HasObserverGender;
        public Gender ObserverGender;
        public bool HasTargetGender;
        public Gender TargetGender;
        public int MinimumObserverAge = -1;
        public int MaximumObserverAge = -1;
        public int MinimumTargetAge = -1;
        public int MaximumTargetAge = -1;
        public bool HasRequiredRelationshipKind;
        public RelationshipKind RequiredRelationshipKind;
        public int AffinityModifier;

        public bool Matches(Sim observer, Sim target, SocialRelationship relationship)
        {
            if (observer == null || target == null) return false;
            if (HasObserverGender && observer.Demographics.Gender != ObserverGender) return false;
            if (HasTargetGender && target.Demographics.Gender != TargetGender) return false;
            if (MinimumObserverAge >= 0 && observer.Demographics.AgeYears < MinimumObserverAge) return false;
            if (MaximumObserverAge >= 0 && observer.Demographics.AgeYears > MaximumObserverAge) return false;
            if (MinimumTargetAge >= 0 && target.Demographics.AgeYears < MinimumTargetAge) return false;
            if (MaximumTargetAge >= 0 && target.Demographics.AgeYears > MaximumTargetAge) return false;
            if (HasRequiredRelationshipKind
                && (relationship == null || !relationship.HasKind(RequiredRelationshipKind))) return false;
            return true;
        }
    }

    public interface ISocialBiasPolicy
    {
        int Evaluate(Sim observer, Sim target, SocialRelationship relationship);
    }

    [Serializable]
    public sealed class SocialBiasProfile : ISocialBiasPolicy
    {
        public List<SocialBiasRule> Rules = new List<SocialBiasRule>();

        public int Evaluate(Sim observer, Sim target, SocialRelationship relationship)
        {
            int total = 0;
            for (int i = 0; i < Rules.Count; i++)
                if (Rules[i] != null && Rules[i].Matches(observer, target, relationship))
                    total += Rules[i].AffinityModifier;
            return Clamp(total);
        }

        private static int Clamp(int value)
        {
            if (value < -100) return -100;
            return value > 100 ? 100 : value;
        }
    }

    public sealed class FamilyDynamicsService
    {
        private readonly RelationshipGraph _relationships;
        private readonly ISocialBiasPolicy _biasPolicy;

        public FamilyDynamicsService(RelationshipGraph relationships, ISocialBiasPolicy biasPolicy)
        {
            _relationships = relationships ?? throw new ArgumentNullException(nameof(relationships));
            _biasPolicy = biasPolicy ?? throw new ArgumentNullException(nameof(biasPolicy));
        }

        public int GetEffectiveAffinity(Sim observer, Sim target)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (target == null) throw new ArgumentNullException(nameof(target));
            SocialRelationship relationship = _relationships.Find(observer.Id, target.Id);
            int baseAffinity = relationship == null ? 0 : relationship.Affinity;
            return Clamp(baseAffinity + _biasPolicy.Evaluate(observer, target, relationship));
        }

        public RelationshipTone GetTone(Sim observer, Sim target)
        {
            int affinity = GetEffectiveAffinity(observer, target);
            if (affinity <= -50) return RelationshipTone.Hostile;
            if (affinity < 0) return RelationshipTone.Bad;
            if (affinity < 25) return RelationshipTone.Neutral;
            if (affinity < 75) return RelationshipTone.Good;
            return RelationshipTone.Devoted;
        }

        private static int Clamp(int value)
        {
            if (value < -100) return -100;
            return value > 100 ? 100 : value;
        }
    }
}
