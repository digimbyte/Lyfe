using System;
using Syms.Simulation.Buildings;

namespace Syms.Simulation.Jobs
{
    public sealed class EmploymentService
    {
        public EmploymentContract Assign(BuildingRoleSlot slot, string simId, double cityTime)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (string.IsNullOrWhiteSpace(simId)) throw new ArgumentException("A Sim ID is required.", nameof(simId));
            if (!slot.IsVacant) throw new InvalidOperationException("The building role slot is already occupied.");

            slot.AssignedSimId = simId;
            return new EmploymentContract
            {
                Id = "employment/" + simId + "/" + slot.Id.Value,
                RoleSlot = slot.Id,
                Job = slot.Job,
                Building = slot.Building,
                Workplace = slot.Workplace,
                Room = slot.Room,
                WorkAnchor = slot.WorkAnchor,
                Schedule = slot.Schedule,
                HourlyPay = slot.HourlyPay,
                RequiresFacility = slot.RequiresFacility,
                WorkFacility = slot.WorkFacility,
                StartTime = cityTime,
                Active = true
            };
        }

        public void Vacate(BuildingRoleSlot slot, EmploymentContract contract)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (slot.Id != contract.RoleSlot) throw new InvalidOperationException("Contract does not belong to this role slot.");
            slot.AssignedSimId = null;
            contract.Active = false;
        }
    }
}
