using System;
using System.Collections.Generic;
using Syms.Simulation.People;
using Syms.Simulation.Stats;

namespace Syms.Simulation.Activities
{
    public interface IActivityPlanner
    {
        ActivityCandidate Choose(
            Sim sim,
            IReadOnlyList<ActivityCandidate> candidates,
            long timeBucket,
            IList<ActivityScore> diagnostics);
    }

    public sealed class DeterministicActivityPlanner : IActivityPlanner
    {
        public ActivityCandidate Choose(
            Sim sim,
            IReadOnlyList<ActivityCandidate> candidates,
            long timeBucket,
            IList<ActivityScore> diagnostics)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (candidates == null || candidates.Count == 0) return null;

            ActivityCandidate best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                ActivityCandidate candidate = candidates[i];
                if (candidate == null) continue;
                ActivityScore score = Score(sim, candidate, timeBucket, i);
                if (diagnostics != null) diagnostics.Add(score);
                if (best == null || score.Total > bestScore)
                {
                    best = candidate;
                    bestScore = score.Total;
                }
            }

            return best;
        }

        private static ActivityScore Score(Sim sim, ActivityCandidate candidate, long timeBucket, int candidateIndex)
        {
            var result = new ActivityScore
            {
                Kind = candidate.Kind,
                BaseScore = candidate.BaseUtility,
                NeedScore = NeedScore(sim.Stats, candidate.Kind),
                PersonalityScore = PersonalityScore(sim.Personality, candidate.Kind),
                TraitScore = TraitScore(sim.Traits, candidate.Kind),
                MoodScore = MoodScore(sim.Mood, candidate.Kind),
                ObligationScore = candidate.WorkObligation * WorkCommitment(sim),
                TravelPenalty = -(float)(candidate.TravelSeconds / 600d),
                Variation = StableVariation(sim.Id, sim.DecisionSequence, timeBucket, candidateIndex)
            };
            result.Total = result.BaseScore + result.NeedScore + result.PersonalityScore
                + result.TraitScore + result.MoodScore + result.ObligationScore
                + result.TravelPenalty + result.Variation;
            return result;
        }

        private static float NeedScore(SimStats stats, ActivityKind kind)
        {
            SimStat stat;
            switch (kind)
            {
                case ActivityKind.Eat: stat = SimStat.Hunger; break;
                case ActivityKind.Drink: stat = SimStat.Thirst; break;
                case ActivityKind.Toilet: stat = SimStat.Bladder; break;
                case ActivityKind.Sleep: stat = SimStat.Energy; break;
                case ActivityKind.Hygiene: stat = SimStat.Hygiene; break;
                case ActivityKind.Socialize: stat = SimStat.Social; break;
                case ActivityKind.Relax:
                    return 60f * Math.Max(stats.GetUrgency(SimStat.Fun),
                        Math.Max(stats.GetUrgency(SimStat.Comfort), stats.GetUrgency(SimStat.Stress)));
                case ActivityKind.Play:
                case ActivityKind.Exercise:
                    return 60f * stats.GetUrgency(SimStat.Fun);
                case ActivityKind.Healthcare:
                    return 120f * Math.Max(stats.GetUrgency(SimStat.Health), stats.GetUrgency(SimStat.Poison));
                default: return 0f;
            }

            float urgency = stats.GetUrgency(stat);
            return (urgency * 100f) + (urgency >= 0.9f ? 100f : 0f);
        }

        private static float PersonalityScore(Personality personality, ActivityKind kind)
        {
            if (personality == null) return 0f;
            float score = 0f;
            if (IsWork(kind))
            {
                if (personality.Conduct == Conduct.Disciplined) score += 20f;
                if (personality.Conduct == Conduct.Flexible) score -= 5f;
                if (personality.Conduct == Conduct.Rebellious) score -= 15f;
            }
            if (kind == ActivityKind.Relax || kind == ActivityKind.WorkBreak)
            {
                if (personality.Conduct == Conduct.Flexible) score += 8f;
                if (personality.Conduct == Conduct.Rebellious) score += 12f;
                if (personality.Concern == Concern.SelfServing) score += 8f;
            }
            if (kind == ActivityKind.Socialize && personality.Concern == Concern.Communal) score += 10f;
            if ((kind == ActivityKind.Healthcare || kind == ActivityKind.Flee) && personality.Concern == Concern.Altruistic) score += 5f;
            return score;
        }

        private static float TraitScore(List<Trait> traits, ActivityKind kind)
        {
            if (traits == null) return 0f;
            float score = 0f;
            for (int i = 0; i < traits.Count; i++)
            {
                Trait trait = traits[i];
                if (trait == Trait.Lazy && IsWork(kind)) score -= 25f;
                if (trait == Trait.Lazy && (kind == ActivityKind.Relax || kind == ActivityKind.WorkBreak)) score += 20f;
                if (trait == Trait.Workaholic && IsWork(kind)) score += 35f;
                if (trait == Trait.Workaholic && kind == ActivityKind.WorkBreak) score -= 15f;
                if (trait == Trait.Ambitious && IsWork(kind)) score += 20f;
                if ((trait == Trait.Social || trait == Trait.Outgoing) && kind == ActivityKind.Socialize) score += 15f;
            }
            return score;
        }

        private static float MoodScore(Mood mood, ActivityKind kind)
        {
            float score = 0f;
            if ((mood == Mood.Anxious || mood == Mood.Stressed || mood == Mood.Angry)
                && (kind == ActivityKind.Relax || kind == ActivityKind.WorkBreak)) score += 15f;
            if (mood == Mood.Depressed && IsWork(kind)) score -= 10f;
            if (mood == Mood.Depressed && kind == ActivityKind.Relax) score += 10f;
            if ((mood == Mood.Happy || mood == Mood.Excited) && kind == ActivityKind.Socialize) score += 8f;
            if (mood == Mood.Afraid && kind == ActivityKind.Flee) score += 30f;
            return score;
        }

        private static float WorkCommitment(Sim sim)
        {
            float factor = 1f;
            if (sim.Personality != null)
            {
                switch (sim.Personality.Conduct)
                {
                    case Conduct.Disciplined: factor = 1.25f; break;
                    case Conduct.Conventional: factor = 1.1f; break;
                    case Conduct.Flexible: factor = 0.85f; break;
                    case Conduct.Rebellious: factor = 0.65f; break;
                }
            }
            if (sim.Traits != null && sim.Traits.Contains(Trait.Workaholic)) factor += 0.3f;
            if (sim.Traits != null && sim.Traits.Contains(Trait.Lazy)) factor -= 0.25f;
            return Math.Max(0.1f, factor);
        }

        private static bool IsWork(ActivityKind kind)
        {
            return kind == ActivityKind.Work || kind == ActivityKind.WorkTask
                || kind == ActivityKind.DeliverMeal;
        }

        private static float StableVariation(string simId, long decisionSequence, long timeBucket, int candidateIndex)
        {
            uint hash = 2166136261u;
            string text = simId ?? string.Empty;
            for (int i = 0; i < text.Length; i++) hash = (hash ^ text[i]) * 16777619u;
            hash = (hash ^ (uint)decisionSequence) * 16777619u;
            hash = (hash ^ (uint)timeBucket) * 16777619u;
            hash = (hash ^ (uint)candidateIndex) * 16777619u;
            return ((hash % 4001u) / 1000f) - 2f;
        }
    }
}
