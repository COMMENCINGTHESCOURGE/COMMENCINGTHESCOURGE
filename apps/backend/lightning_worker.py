import sys
import json
import uvicorn
from fastapi import FastAPI, Request
from typing import Dict, Any

# Import the Substrate Sieve
sys.path.append(r"C:\Users\dasha\Projects\SubstrateDeltaSieve")
try:
    from SUBSTRATE_DELTA_SIEVE import SubstrateDeltaSieve
except ImportError as e:
    print(f"Failed to import SUBSTRATE_DELTA_SIEVE: {e}")
    # Fallback mock for testing if path fails
    class SubstrateDeltaSieve:
        def process_delta(self, t, *args): return 0.0

app = FastAPI(title="Cognitive Matrix Backend", version="1.0")
sieve = SubstrateDeltaSieve()

# Store the latest state to inject into LLM prompts
_latest_substrate_state: Dict[str, Any] = {}
_latest_corridor: str = "NEUTRAL"


def erdos_straus_hot_corridor(tick_id: int, base_offset: int = 24) -> str:
    """
    Erdős-Straus staircase scheduling.
    Stride-24 loop mapping to Mod9 classification for zero-conflict distribution
    across A100/L40S nodes.
    """
    corridor_depth = tick_id // base_offset
    mod9 = (base_offset + corridor_depth) % 9
    
    if mod9 in [0, 3, 6]:
        return "STABLE"
    elif mod9 in [1, 4, 7]:
        return "NEUTRAL"
    else:
        return "BREACH"

@app.post("/ingest_state")
async def ingest_state(request: Request):
    """
    Ingests serialized state from Godot ConstraintField.
    """
    state: Dict[str, Any] = await request.json()
    
    # 1. Stride-24 loop routing
    # Using Day + TimeOfDay as a pseudo-tick for scheduling
    day = state.get("Day", 0)
    time_of_day = state.get("TimeOfDay", 0)
    tick_id = int((day + 10) * 24 + time_of_day) # Absolute hours since start
    
    classification = erdos_straus_hot_corridor(tick_id)
    
    # 2. Process deviations through the Sieve
    morale = float(state.get("Morale", 1.0))
    # Historical mean for morale is expected to be around 0.6
    stat_diff = sieve.process_delta('statistical', [morale], 0.6)
    
    # 3. Check Glass-To-Wall Ratio metric symbolic grounding
    glass_ratio = state.get("GlassToWallRatio", None)
    sym_check = False
    if glass_ratio is not None:
        sym_check = sieve.process_delta('symbolic', 'GlassToWallRatio', ['GlassToWallRatio', 'Pangea Principle', 'Ghost Braid'])
        
    print(f"Processed State Tick [{tick_id}] | Corridor: {classification} | Morale Var: {stat_diff:.4f} | Glass Ratio Valid: {sym_check}")
    
    global _latest_substrate_state, _latest_corridor
    _latest_substrate_state = state
    _latest_corridor = classification

    return {
        "status": "processed",
        "corridor_classification": classification,
        "statistical_variance": stat_diff,
        "symbolic_valid": sym_check
    }

import httpx

@app.post("/generate_dialogue")
async def generate_dialogue(request: Request):
    data = await request.json()
    npc_name = data.get("NpcName", "Survivor")
    
    # Construct Context from Substrate Sieve
    morale = _latest_substrate_state.get("Morale", 1.0)
    glass_ratio = _latest_substrate_state.get("GlassToWallRatio", 0.0)
    threat = _latest_substrate_state.get("HordeThreat", 0.0)
    
    system_prompt = f"""You are {npc_name}, a survivor in a harsh post-apocalyptic settlement.
You must speak exactly ONE short sentence. No actions, no markdown.
Current Reality Substrate (DO NOT mention numbers, just react to the feeling):
- Settlement Morale is at {morale*100:.1f}%.
- Glass-to-Wall ratio is {glass_ratio:.2f} (higher means dangerously exposed to breaches).
- Horde Threat level is {threat*100:.1f}%.
- Erdős-Straus Hot Corridor Status: {_latest_corridor}.
Speak your truth to the player based on these conditions."""

    try:
        async with httpx.AsyncClient() as client:
            resp = await client.post("http://10.0.0.102:11434/api/generate", json={
                "model": "hermes3:latest",
                "prompt": system_prompt,
                "stream": False
            }, timeout=30.0)
            
            resp_data = resp.json()
            reply = resp_data.get("response", "I have nothing to say right now.")
            return {"dialogue": reply.strip()}
    except Exception as e:
        print(f"Ollama Matrix Error: {e}")
        return {"dialogue": f"The matrix is silent. ({e})"}

if __name__ == "__main__":
    print("=== COGNITIVE MATRIX BACKEND: ONLINE ===")
    print("Binding to local interface (127.0.0.1) simulating 10.0.0.102...")
    uvicorn.run("lightning_worker:app", host="127.0.0.1", port=8080, reload=True)
