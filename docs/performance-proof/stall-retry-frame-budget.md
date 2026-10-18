# Frame budget (#97)

- GPU: NVIDIA GeForce RTX 4070 Laptop GPU (Direct3D11)
- CPU: 13th Gen Intel(R) Core(TM) i9-13900H, 20 threads
- When: 2026-10-17 20:10
- Picture: 1920 x 1080
- Timing: wall clock, Editor batch mode. CPU = game step + render calls; GPU = render calls until the frame is read back (an upper bound on the GPU's own time). Crowd = median Shot mean GPU ms, crowd on less off.
- Limits: slowest 1% under 12.00 ms GPU and 8.00 ms CPU; crowd under 2.00 ms GPU
- Verdict: OVER budget

| Moment | Frames | GPU slowest 1% ms (upper bound) | CPU slowest 1% ms | Crowd GPU ms (upper bound) | Verdict | GPU median ms | CPU median ms | GPU wait-only slowest 1% ms (lower bound) | Crowd wait-only ms (lower bound) |
|---|---|---|---|---|---|---|---|---|---|
| skate-in | 1775 | 20.79 | 17.29 | 4.93 | over: GPU, CPU, crowd | 10.17 | 6.99 | 9.79 | 1.89 |
| Replay after a Goal | 1424 | 26.15 | 28.20 | 5.57 | over: GPU, CPU, crowd | 11.73 | 8.93 | 10.33 | 1.67 |

A slowest 1% many times its median means a few frames stalled (another program, shader loading, heat): close other programs and run it again before cutting.

If over, cut in this order (never the Calm Area test or the goalie):
1. far fans to cards
2. lower Replay and board picture size
3. bake the ice reflection once
4. fewer roof-light shadows
5. less far-end detail
6. softer post-processing
