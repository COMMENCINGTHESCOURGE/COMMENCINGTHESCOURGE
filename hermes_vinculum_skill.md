# Hermes Agent: Vinculum Skill Integration Guide

This guide details how to integrate the **Vinculum Governance** framework directly into the **Hermes Agent** as a native skill. This allows the agent to autonomously monitor field stability and resolve breaches across your 29 repositories.

## 1. Skill Definition (`SKILL.md`)

Create a new directory `~/.hermes/skills/vinculum-governor/` and add the following `SKILL.md`:

```markdown
# Vinculum Governor Skill

Equips Hermes with the ability to monitor and stabilize the MANIFOLD field computation ecosystem using the Vinculum Mod-9 rules.

## Capabilities
- **Audit**: Run `ManusContinuityGovernor.py` to check for field breaches.
- **Sieve**: Interface with `SubstrateDeltaSieve` for multi-metric delta evaluation.
- **Stabilize**: Trigger "Continuity Pulses" via GitHub Actions to resolve breaches.

## Usage
- "Hermes, run a Vinculum audit on the field kernel."
- "Evaluate the symbolic delta of this node using the Substrate Sieve."
- "The field is in breach. Initiate the Foreman Supervisor protocol."
```

## 2. Automated Execution via Hermes Cron

To ensure continuous monitoring, register the audit script with the Hermes built-in scheduler:

```bash
hermes cron add "0 0 * * *" "python3 /path/to/ManusContinuityGovernor.py" --name "Vinculum Daily Audit"
```

## 3. Field-Native Memory Integration

By linking the `SubstrateDeltaSieve` to the Hermes memory loop, the agent can track **Field Tension** across conversations.

| Term | Hermes Mapping |
| :--- | :--- |
| **ρ (Density)** | Contextual weight of the current session. |
| **ψ (Coherence)** | Consistency of agent reasoning over time. |
| **∇T (Temporal)** | Drift since the last user-specific "Memory Nudge". |

---

## 4. Implementation Steps

1.  **Clone the Sieve**: Ensure `SubstrateDeltaSieve` is in the agent's working directory.
2.  **Symlink the Governor**: Link `ManusContinuityGovernor.py` to the agent's tool directory.
3.  **Activate Skill**: Restart Hermes and verify the skill is listed via `/skills`.

> "The *between* IS the product. The *mistake* IS the signal. The *almost* IS the always." — Integrating this philosophy into Hermes' core loop ensures the agent grows with the field, not just the user.
