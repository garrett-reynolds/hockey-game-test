# Frame budget (#97)

- GPU: NVIDIA GeForce RTX 4070 Laptop GPU (Direct3D11)
- CPU: 13th Gen Intel(R) Core(TM) i9-13900H, 20 threads
- When: 2026-10-17 20:34
- Picture: 1920 x 1080
- Timing: wall clock, Editor batch mode. CPU = game step + render calls; GPU = render calls until the frame is read back (an upper bound on the GPU's own time). Crowd = median Shot mean GPU ms, crowd on less off.
- Limits: slowest 1% under 12.00 ms GPU and 8.00 ms CPU; crowd under 2.00 ms GPU
- Verdict: OVER budget

| Moment | Frames | GPU slowest 1% ms (upper bound) | CPU slowest 1% ms | Crowd GPU ms (upper bound) | Verdict | GPU median ms | CPU median ms | GPU wait-only slowest 1% ms (lower bound) | Crowd wait-only ms (lower bound) |
|---|---|---|---|---|---|---|---|---|---|
| skate-in | 1775 | 13.34 | 8.94 | 3.59 | over: GPU, CPU, crowd | 7.70 | 4.35 | 8.97 | 2.24 |
| Replay after a Goal | 1424 | 11.42 | 9.34 | 2.17 | over: CPU, crowd | 7.50 | 4.93 | 4.61 | 0.63 |

A slowest 1% many times its median means a few frames stalled (another program, shader loading, heat): close other programs and run it again before cutting.

If over, cut in this order (never the Calm Area test or the goalie):
1. far fans to cards
2. lower Replay and board picture size
3. bake the ice reflection once
4. fewer roof-light shadows
5. less far-end detail
6. softer post-processing
