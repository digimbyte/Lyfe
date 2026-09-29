using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Simulation.Activities
{
    public interface IActivityCandidateSource
    {
        IReadOnlyList<ActivityCandidate> GetCandidates(Sim sim, double cityTime);
    }

    /// <summary>
    /// Minimal candidate source. Games can replace it with a source backed by
    /// authored venues, relationships, dangers, and object affordances.
    /// </summary>
    public sealed class BasicActivityCandidateSource : IActivityCandidateSource
    {
        public IReadOnlyList<ActivityCandidate> GetCandidates(Sim sim, double cityTime)
        {
            PlaceId place = sim.Spatial.Address.Place;
            RoomId room = sim.Spatial.Room;
            CellId cell = sim.Spatial.Address.Cell;
            var result = new List<ActivityCandidate>
            {
                Candidate(ActivityKind.Idle, place, cell, 600d, 1f),
                Candidate(ActivityKind.Eat, place, cell, 1200d, 0f),
                Candidate(ActivityKind.Drink, place, cell, 300d, 0f),
                Candidate(ActivityKind.Toilet, place, cell, 600d, 0f),
                Candidate(ActivityKind.Sleep, place, cell, 28800d, 0f),
                Candidate(ActivityKind.Hygiene, place, cell, 1200d, 0f),
                Candidate(ActivityKind.Relax, place, cell, 1800d, 4f),
                Candidate(ActivityKind.Socialize, place, cell, 1800d, 2f)
            };
            for (int i = 0; i < result.Count; i++) result[i].Room = room;

            int minuteOfWeek = (int)(Math.Max(0d, cityTime) / 60d) % (7 * 24 * 60);
            DayOfWeek day = (DayOfWeek)(minuteOfWeek / (24 * 60));
            int minuteOfDay = minuteOfWeek % (24 * 60);
            if (sim.Employment != null
                && sim.Employment.IsEmployed
                && !string.IsNullOrEmpty(sim.Employment.WorkRoom.Value)
                && sim.Employment.Schedule.IsWorking(day, minuteOfDay))
            {
                result.Add(new ActivityCandidate
                {
                    Kind = ActivityKind.WorkTask,
                    Place = sim.Employment.Workplace,
                    Room = sim.Employment.WorkRoom,
                    Anchor = sim.Employment.WorkAnchor,
                    DurationSeconds = 1800d,
                    BaseUtility = 5f,
                    WorkObligation = 60f,
                    RequiresFacility = sim.Employment.RequiresFacility,
                    Facility = sim.Employment.WorkFacility
                });
                result.Add(new ActivityCandidate
                {
                    Kind = ActivityKind.WorkBreak,
                    Place = sim.Employment.Workplace,
                    Room = sim.Employment.WorkRoom,
                    Anchor = sim.Employment.WorkAnchor,
                    DurationSeconds = 900d,
                    BaseUtility = 1f,
                    WorkObligation = 5f
                });
            }

            return result;
        }

        private static ActivityCandidate Candidate(ActivityKind kind, PlaceId place, CellId cell, double duration, float baseUtility)
        {
            return new ActivityCandidate
            {
                Kind = kind,
                Place = place,
                Cell = cell,
                DurationSeconds = duration,
                BaseUtility = baseUtility
            };
        }
    }
}
