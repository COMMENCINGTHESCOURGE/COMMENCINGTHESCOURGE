import sys
import os
import math
import json
from datetime import datetime

# Add the SubstrateDeltaSieve directory to path
sys.path.append(os.path.abspath('./SubstrateDeltaSieve'))
from SUBSTRATE_DELTA_SIEVE import SubstrateDeltaSieve

def fetch_repo_data():
    # In a real agentic loop, I'd use 'gh' to fetch this. 
    # For the demo, I'll use the data I fetched earlier.
    repos = [
        {"name": "COMMENCINGTHESCOURGE", "stars": 1, "forks": 0, "issues": 0, "updated": "2026-06-07"},
        {"name": "hermes-agent", "stars": 1, "forks": 0, "issues": 0, "updated": "2026-06-07"},
        {"name": "guinea-pig-trench-portal", "stars": 0, "forks": 0, "issues": 0, "updated": "2026-06-07"},
        {"name": "erdos-straus-solver", "stars": 0, "forks": 0, "issues": 0, "updated": "2026-07-28"},
        {"name": "SubstrateDeltaSieve", "stars": 0, "forks": 0, "issues": 0, "updated": "2026-06-12"}
    ]
    return repos

def run_governance_audit():
    sieve = SubstrateDeltaSieve()
    repos = fetch_repo_data()
    
    # Baseline: The core COMMENCINGTHESCOURGE repository (The Field Kernel)
    # We define its 'EM Baseline' as (Stars, Forks, Recency_Score)
    # Recency_Score = 1.0 if updated within 7 days, 0.5 if 30 days, 0.1 otherwise.
    baseline_vector = (1.0, 0.1, 0.5) 
    
    print("--- MANUS CONTINUITY GOVERNOR: AUDIT START ---")
    print(f"Target: {len(repos)} Continuity Engines\\n")
    
    audit_results = []
    
    for repo in repos:
        # Calculate Recency Score
        updated_date = datetime.strptime(repo['updated'], "%Y-%m-%d")
        days_since = (datetime(2026, 7, 30) - updated_date).days
        recency = 1.0 if days_since < 7 else (0.5 if days_since < 30 else 0.1)
        
        # 1. Angular Alignment Check (Stealth Kinematics)
        # We check how well the repo's activity matches the core engine's baseline.
        current_vector = (float(repo['stars'] + 1), float(repo['forks'] + 0.1), recency)
        alignment = sieve.process_delta('angular', current_vector, baseline_vector)
        
        # 2. Symbolic Stability Check (Vinculum Breach)
        # σ = |REQ \\ CAP|. Let's define REQ as 'Days Since Update' and CAP as 'Stars + 1'.
        sigma = abs(days_since - (repo['stars'] + 1))
        # Applying the Mod-9 rule from their README
        theta = (sigma * 3) % 9
        is_stable = theta not in {0, 3, 6}
        
        print(f"Repo: {repo['name']}")
        print(f"  > Alignment Score: {alignment:.4f}")
        print(f"  > Vinculum State:  {'STABLE' if is_stable else 'BREACH'} (θ={theta})")
        
        audit_results.append({
            "repo": repo['name'],
            "alignment": alignment,
            "status": status,
            "theta": int(theta)
        })
    
    print("\\n--- AUDIT SUMMARY ---")
    breaches = [r['repo'] for r in audit_results if r['status'] == "BREACH"]
    if breaches:
        print(f"WARNING: Continuity Breach detected in {len(breaches)} nodes: {', '.join(breaches)}")
        print("ACTION: Triggering 'Foreman Supervisor' protocol for re-meshing.")
    else:
        print("STATUS: All fields within nominal Mod-9 corridors.")
    
    return audit_results

if __name__ == "__main__":
    results = run_governance_audit()
    with open('audit_results.json', 'w') as f:
        json.dump(results, f)
