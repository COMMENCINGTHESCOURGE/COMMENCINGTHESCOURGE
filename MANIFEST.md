# MANIFOLD Ecosystem Manifest

This organization curates the MANIFOLD field computation ecosystem. It abandons traditional discrete asset engines in favor of continuous substrate simulations.

## The Continuity Nodes

1. **[hyperpoly-terrain](./hyperpoly-terrain)**
   - **Role:** The Physics Kernel
   - **Invariant:** Zero host-GPU sync. 6-channel material tensors are simulated and resolved strictly via cohesion-weighted QEF on the GPU.

2. **[trench-builder](./trench-builder)**
   - **Role:** Open-World Integration
   - **Invariant:** Continuity over assets. Infinite chunk streaming must not stall the main thread or break field boundaries.

3. **[sovereign-resonance-node](./sovereign-resonance-node)**
   - **Role:** The Observer & WebGL Avatar
   - **Invariant:** Telemetry must be diegetic. HUD logic cannot bottleneck the WebRTC Swarm or the underlying physics simulation.

4. **[COMMENCINGTHESCOURGE](./)**
   - **Role:** WebGL Avatar Gateway & Profile
   - **Invariant:** Projecting the ecosystem state.

> *"Continuity is not a feature. It's the foundation."*
