# Security Policy

## Experimental Software Warning

> [!CAUTION]
> This is experimental field computation software. **Do not deploy to production** without validating physical bounds.

The MANIFOLD ecosystem runs heavy asynchronous GPU computations (WebGPU / WebGL).
- **Memory Leaks:** Incorrectly tuned chunk streaming (`trench-builder`) can exhaust VRAM rapidly.
- **Substrate Failure:** Invalid values in the 6-channel material tensor (`hyperpoly-terrain`) can crash the QEF solver.
- **WebRTC Desync:** The `sovereign-resonance-node` signaling server may drop packets if overwhelmed by Thermodynamic Subduction events.

## Reporting Vulnerabilities

If you discover a memory leak, GPU stall, or security vulnerability within the MANIFOLD pipeline, please open a private GitHub issue or contact the maintainers directly. Do not submit GPU-crashing shaders as public bug reports until patched.
