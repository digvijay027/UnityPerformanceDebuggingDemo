# Unity Performance Debugging & Optimization Case Study

A senior-level Unity performance engineering case study demonstrating systematic profiling, root-cause bottleneck diagnosis, and iterative optimization under high-stress conditions.

---

## Overview

This repository demonstrates the engineering workflow used to diagnose and resolve severe performance bottlenecks in a real-time Unity 3D application. Rather than applying blind micro-optimizations, this project establishes an intentionally inefficient baseline workload under strictly controlled stress conditions (75 active enemies, 10–20 active projectiles, 0.08s rapid auto-fire) and systematically removes bottlenecks across four distinct optimization phases.

Each phase isolates a single performance concern, backed by empirical data captured using the **Unity Profiler**:
1. **CPU Workload Optimization** (Trigonometric math calculations $\rightarrow$ Cadence throttling + static LUT)
2. **Managed Memory Allocation (GC) Optimization** (Ephemeral string formatting and heap collections $\rightarrow$ Preallocated struct buffer)
3. **Object Lifecycle Optimization** (Runtime `Instantiate()` / `Destroy()` churn $\rightarrow$ Pre-warmed reusable Object Pooling)
4. **Query & Lookup Optimization** (Scene-wide `GameObject.FindGameObjectsWithTag` scans $\rightarrow$ $O(1)$ in-memory `EnemyRegistry`)

---

## What This Demo Demonstrates

- **Controlled Empirical Benchmarking:** Reproducible before/after measurements using runtime toggles (`Baseline Mode` vs `Optimized Mode`).
- **Unity Profiler Mastery:** Identifying hot paths across CPU Main Thread, GC Allocated in Frame, PlayerLoop execution, and native engine overheads.
- **Memory & Garbage Reduction:** Eliminating per-frame managed heap allocations in hot loops to stop incremental GC collection spikes.
- **Object Lifecycle Architecture:** Modular, extensible object pooling with clean `IPoolable` state resets and dynamic expansion guards.
- **Data-Oriented Queries:** Decoupling query execution time from scene hierarchy size using maintained collections.

---

## Technical Stack

- **Game Engine:** Unity 2021.3.33f1 (LTS)
- **Scripting Runtime:** .NET Standard 2.1 / C# 9.0
- **Rendering Pipeline:** Built-in Render Pipeline (Standard Shaders, 3D Arena)
- **Profiling Tools:** Unity Profiler, Frame Debugger, `Unity.Profiling.ProfilerRecorder` API
- **Target Platform:** PC Standalone (Windows 64-bit)

---

## Initial Problem

Under initial stress conditions (sustaining 75 active enemies and high-rate projectile combat), the application suffered from catastrophic performance degradation:
- Framerate dropped to an unplayable **7.8 FPS** (6.8–8.5 FPS range).
- Main Thread frame time skyrocketed to **127.90 ms** (far exceeding the 16.6 ms budget for 60 FPS).
- Managed heap memory churn generated **548.2 KB of garbage every single frame**, triggering recurrent 15–40 ms garbage collector pauses every 1.5–2.5 seconds.
- Continuous instantiation and destruction of short-lived projectiles and enemies generated native C++ peer handle allocations and memory fragmentation.

---

## Baseline Performance

Profiling the initial baseline version using the Unity Profiler revealed the distribution of frame time and allocation overhead:

| Profiler Marker / Metric | Baseline Measurement | % of Frame Time / Notes |
| :--- | :--- | :--- |
| **App Framerate** | **7.8 FPS** | Severe stutter; unplayable |
| **Main Thread Frame Time** | **127.90 ms** | Representative stable frame |
| **Managed GC Allocation** | **548.2 KB / frame** | Severe heap pressure |
| **`EnemyWorkload.ExecuteCpuWorkload`** | **72.15 ms** | **56.4% of total frame time** |
| **`EnemyWorkload.ExecuteGcAllocations`** | **25.27 ms** | **540 KB / frame (98.5% of total allocations)** |
| **PlayerLoop Execution** | **112.31 ms** | Dominated by script updates |
| **Rendering (`Render Loop`)** | **~2.8 ms** | Investigated; **not** a primary bottleneck |
| **Active Enemies** | **75 / 75** | Benchmark target population |
| **Active Projectiles** | **10–20** | Continuous 0.08s fire rate |

![Baseline CPU Profiler](Assets/Screenshots/baseline_cpu_profiler.png)
*Figure 1: Initial baseline Unity Profiler capture demonstrating 72.15 ms CPU math bottleneck and 548.2 KB/frame GC allocation.*

---

## Profiling Investigation

A methodical deep-dive into the Profiler Hierarchy identified four root causes:

### 1. CPU Workload
Each of the 75 active enemies executed 250 heavy mathematical iterations (`Mathf.Sin`, `Mathf.Cos`, `Mathf.Sqrt`) synchronously inside `Update()`. Across 75 enemies, this resulted in 18,750 transcendent math operations executed on the Main Thread every single frame, consuming **72.15 ms**.

### 2. Managed GC Allocations
Within the same enemy update loop, each enemy performed string concatenation, float-to-string conversions (`Vector3.ToString()`), and instantiated `new List<string>(4)` collections every frame. This produced ~540 KB of short-lived garbage per frame across the 75 enemies, forcing the incremental garbage collector to constantly trigger collection passes.

![Baseline GC Profiler](Assets/Screenshots/baseline_gc_profiler.png)
*Figure 2: Unity Profiler Memory view displaying 540 KB/frame heap allocations originating from enemy update string and list allocations.*

### 3. Instantiate / Destroy Churn
Projectiles were spawned via `GameObject.Instantiate()` at 12.5 shots/second and destroyed upon collision or timeout via `GameObject.Destroy()`. Similarly, defeated enemies were destroyed and instantiated anew. This induced continuous native engine registration passes, managed C# wrapper construction, and transform hierarchy overhead, consuming ~98% of the execution time in `PlayerShooting.Shoot`.

### 4. Scene-Wide Queries
`EnemySpawner` checked active population before spawning by calling `GameObject.FindGameObjectsWithTag("Enemy")`. This performed an $O(N)$ traversal of the entire active scene hierarchy and allocated a new `GameObject[75]` array on the managed heap every time it executed.

---

## Optimization Process

The optimization was carried out incrementally across four controlled stages:

```
[Phase 3: Baseline] ──> [Phase 4.1: CPU] ──> [Phase 4.2: GC Alloc] ──> [Phase 4.3: Pooling] ──> [Phase 4.4: Registry]
      7.8 FPS                 11.2 FPS                  50.0 FPS                   75.9 FPS                  78.5 FPS
     127.90 ms               100.67 ms                  18.16 ms                    8.71 ms                   9.41 ms
   548.2 KB/frame          548.2 KB/frame              3.1 KB/frame               20.0 KB/frame             18.5 KB/frame
```

### Phase 4.1 — CPU Optimization
- **Problem:** Synchronous calculation of 250 trigonometric iterations per enemy per frame.
- **Solution:** 
  1. Reduced mathematical sample depth from 250 to 15 iterations.
  2. Implemented frame cadence throttling (calculation staggered across 4 frames using entity ID modulo offsets: `((instanceId + Time.frameCount) % 4) == 0`).
  3. Precomputed a 256-entry static trigonometric lookup table (`SinCosLut`), replacing expensive runtime transcendental math with fast table lookups.
  4. Inter-frame result caching to maintain continuity between updates.
- **Result:** CPU workload execution dropped from **72.15 ms $\rightarrow$ 2.85 ms (-96.0%)**. Framerate increased from **7.8 FPS $\rightarrow$ 11.2 FPS**.

![CPU Optimization Profiler](Assets/Screenshots/cpu_optimization_profiler.png)
*Figure 3: Phase 4.1 profiler verification showing CPU workload reduced to 2.85 ms while GC allocations remain intentionally unchanged.*

---

### Phase 4.2 — GC Optimization
- **Problem:** Per-frame string building (`Vector3.ToString()`, `string.Concat`) and ephemeral `new List<string>(4)` collections allocating ~540 KB/frame.
- **Solution:**
  1. Completely removed runtime string formatting and dynamic generic collections from the update hot path.
  2. Introduced a preallocated, fixed-size value-type struct buffer (`EnemyTelemetry[]`).
  3. Replaced string operations with deterministic arithmetic state hashing operating in-place within the preallocated buffer (zero heap allocations).
  4. Cached unchanging static identifier strings in `Awake()`.
- **Result:** Enemy workload GC allocations dropped from **~540 KB/frame $\rightarrow$ exactly 0 Bytes/frame (100% elimination)**. Total frame allocation dropped from **548.2 KB $\rightarrow$ 3.1 KB/frame (-99.4%)**. Main Thread frame time dropped to **18.16 ms**, and sustained framerate jumped to **50.0 FPS**.

![GC Optimization Profiler](Assets/Screenshots/gc_optimization_profiler.png)
*Figure 4: Phase 4.2 profiler verification showing 0 Bytes allocated from EnemyWorkload.Update and framerate reaching 50.0 FPS.*

---

### Phase 4.3 — Object Pooling
- **Problem:** Continuous `Instantiate()` and `Destroy()` churn of projectiles (12.5/s) and enemies under rapid combat.
- **Solution:**
  1. Built a modular, generic [`ObjectPool`](Assets/Scripts/Performance/ObjectPool.cs) component using `Queue<GameObject>` and `HashSet<GameObject>`.
  2. Coordinated by a centralized [`PoolManager`](Assets/Scripts/Performance/PoolManager.cs) pre-warming 35 projectiles and 85 enemies into dedicated hierarchy containers.
  3. Created an [`IPoolable`](Assets/Scripts/Core/IPoolable.cs) interface defining `OnSpawnFromPool()` and `OnReturnToPool()` lifecycle hooks.
  4. Thoroughly reset gameplay state upon reuse:
     - **Projectiles:** Reset lifetime timer, clear hit flags, align trajectory with fire point orientation.
     - **Enemies:** Reset health to max, clear Rigidbody velocities/angular momentum, refresh player target reference, reset attack cooldowns.
  5. Implemented dynamic pool expansion to guard against sudden demand spikes.
- **Result:** Runtime `Instantiate()` and `Destroy()` calls dropped from continuous churn to **exactly 0 calls/second in steady state**. Shooting execution time fell by **55%**, Main Thread frame time dropped from **18.16 ms $\rightarrow$ 8.71 ms**, and framerate rose to **75.9 FPS**.

![Object Pooling Profiler](Assets/Screenshots/object_pooling_profiler.png)
*Figure 5: Phase 4.3 profiler verification confirming 0 runtime Instantiate/Destroy calls and 75.9 FPS playback.*

---

### Phase 4.4 — Enemy Registry
- **Problem:** `EnemySpawner.Update` and `GameHUD.OnGUI` executed `GameObject.FindGameObjectsWithTag("Enemy")` every tick, performing scene-wide hierarchy scans and allocating temporary `GameObject[]` arrays.
- **Solution:**
  1. Created a static [`EnemyRegistry`](Assets/Scripts/Enemy/EnemyRegistry.cs) backed by an in-memory `HashSet<EnemyHealth>`.
  2. Enemies self-register on `OnEnable()` / `OnSpawnFromPool()` and unregister on `OnDisable()` / `OnReturnToPool()` / `Die()`.
  3. Decoupled pool capacity (85 total instances) from active enemy count: inactive pooled enemies residing in the pool container are not registered.
  4. Substituted `GameObject.FindGameObjectsWithTag("Enemy").Length` with the $O(1)$ property `EnemyRegistry.ActiveEnemyCount`.
- **Result:** `FindGameObjectsWithTag` calls dropped from ~60–120 calls/sec to **0 calls/sec in optimized mode**. `EnemySpawner.Update` execution overhead dropped by **31%** (from 0.0080 ms to 0.0055 ms), managed GC allocations dropped by **~1.8 KB/frame** (eliminating the temporary array garbage), and framerate stabilized at **78.5 FPS**.

![Query Optimization Profiler](Assets/Screenshots/query_optimization_profiler.png)
*Figure 6: Phase 4.4 profiler verification showing elimination of tag search markers, 0 B spawner allocations, and 78.5 FPS.*

---

## Before / After Results

The table below reflects **actual empirical measurements** gathered under identical stress conditions (75 active enemies, 10–20 active projectiles, 0.08s auto-fire rate):

| Metric | Phase 3 (Baseline) | Phase 4.1 (CPU Opt) | Phase 4.2 (GC Opt) | Phase 4.3 (Pooling) | Phase 4.4 (Registry) | Total Delta (Baseline $\rightarrow$ Final) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **App Framerate** | **7.8 FPS** | 11.2 FPS | 50.0 FPS | 75.9 FPS | **78.5 FPS** | **+70.7 FPS (+906%)** |
| **Main Thread Time** | **127.90 ms** | 100.67 ms | 18.16 ms | 8.71 ms | **9.41 ms** | **-118.49 ms (-92.6%)** |
| **GC Alloc / Frame** | **548.2 KB** | 548.2 KB | 3.1 KB | 20.0 KB* | **18.5 KB** | **-529.7 KB (-96.6%)** |
| **CPU Workload Time** | **72.15 ms** | 2.85 ms | 0.16 ms | 0.16 ms | **0.16 ms** | **-71.99 ms (-99.8%)** |
| **Enemy Workload GC** | **~540 KB** | ~540 KB | 0 B | 0 B | **0 B** | **-540.0 KB (-100%)** |
| **Instantiate Calls/s** | High Churn | High Churn | High Churn | 0 / s | **0 / s** | **Eliminated in steady state** |
| **Destroy Calls/s** | High Churn | High Churn | High Churn | 0 / s | **0 / s** | **Eliminated in steady state** |
| **Tag Query Calls/s** | ~60–120 / s | ~60–120 / s | ~60–120 / s | ~60–120 / s | **0 / s** | **Eliminated in hot path** |
| **Active Enemies** | **75 / 75** | 75 / 75 | 75 / 75 | 75 / 75 | **75 / 75** | **Exact Control Maintained** |
| **Active Projectiles** | **10–20** | 10–20 | 10–20 | 10–20 | **10–20** | **Exact Control Maintained** |

*\*Note: GC allocation in Phases 4.3 and 4.4 accounts for immediate-mode `GameHUD.OnGUI` diagnostic labels; in Phase 4.4, eliminating `FindGameObjectsWithTag` removed ~1.8 KB/frame of recurring `GameObject[75]` array allocations.*

---

## Architecture

The project architecture cleanly separates test controls, pooled entity lifecycles, and registry tracking:

```
                      ┌────────────────────────────┐
                      │  PerformanceTestSettings   │
                      │  - BaselineMode (Hotkey B) │
                      │  - TargetPopulation: 75    │
                      │  - StressFireRate: 0.08s   │
                      └─────────────┬──────────────┘
                                    │
           ┌────────────────────────┴────────────────────────┐
           ▼                                                 ▼
┌──────────────────────┐                          ┌──────────────────────┐
│     EnemySpawner     │                          │    PlayerShooting    │
│  - Baseline:         │                          │  - Baseline:         │
│    FindGameObjects   │                          │    Instantiate()     │
│  - Optimized:        │                          │  - Optimized:        │
│    EnemyRegistry     │                          │    PoolManager.Get() │
└──────────┬───────────┘                          └──────────┬───────────┘
           │                                                 │
           ▼                                                 ▼
┌────────────────────────────────────────────────────────────────────────┐
│                              PoolManager                               │
│  ┌───────────────────────────────┐   ┌───────────────────────────────┐ │
│  │     EnemyPool (Cap: 85)       │   │    ProjectilePool (Cap: 35)   │ │
│  │   - Prewarmed instances       │   │   - Prewarmed instances       │ │
│  │   - Dynamic expansion safety  │   │   - Dynamic expansion safety  │ │
│  └───────────────┬───────────────┘   └───────────────┬───────────────┘ │
└──────────────────┼───────────────────────────────────┼─────────────────┘
                   │                                   │
                   ▼                                   ▼
        ┌─────────────────────┐             ┌─────────────────────┐
        │     EnemyHealth     │             │     Projectile      │
        │   - IPoolable       │             │   - IPoolable       │
        │   - Reset HP / Vel  │             │   - Reset Lifetime  │
        └──────────┬──────────┘             └─────────────────────┘
                   │
                   ▼ (OnEnable / OnDisable)
        ┌─────────────────────┐
        │    EnemyRegistry    │
        │   - HashSet<Enemy>  │
        │   - O(1) Count      │
        └─────────────────────┘
```

---

## Profiling Methodology

To ensure integrity and reproducibility, all profiling followed strict engineering controls:

1. **Isolation of Variables:** Only one system was modified per phase (CPU $\rightarrow$ GC $\rightarrow$ Pooling $\rightarrow$ Query). Unrelated systems were strictly frozen.
2. **Fixed Stress Invariants:** Target population (75 active enemies) and projectile rate (0.08s interval) were kept identical across all phases.
3. **Reproducible Toggle (`BaselineMode`):** Pressing `[B]` during Play Mode instantly restores the unoptimized baseline algorithms for direct, real-time A/B profiler verification.
4. **No Synthetic Numbers:** Every reported metric represents actual measurements sampled over stable multi-second windows via `ProfilerRecorder` and the Unity Profiler hierarchy.

---

## Key Engineering Decisions

- **Why a Static LUT for CPU Math?** Transcendent functions (`Mathf.Sin`, `Mathf.Cos`) are computationally expensive on modern CPUs. Replacing them with a 256-element precomputed lookup table trading a negligible 1 KB memory buffer for a 96% reduction in calculation time.
- **Why Preallocated Struct Buffers for GC?** Value-type structs (`EnemyTelemetry`) stored in pre-sized arrays reside in contiguous heap memory. Writing to existing elements generates zero heap garbage, eliminating GC collection spikes.
- **Why Custom Object Pooling over Unity 2021 `UnityEngine.Pool`?** Writing an explicit, modular `ObjectPool` using `Queue<T>` and `IPoolable` provides full control over hierarchy parenting, dynamic expansion logging, and state reset hooks, demonstrating core senior-level data structure design.
- **Why a `HashSet<EnemyHealth>` for EnemyRegistry?** A `HashSet` guarantees $O(1)$ insertion, $O(1)$ removal, and $O(1)$ count retrieval while inherently preventing duplicate registrations if an entity is activated multiple times.

---

## Limitations & Trade-offs

- **Memory vs CPU Trade-off (Pooling):** Pre-warming 85 enemies and 35 projectiles incurs an initial heap/memory cost at startup. For memory-constrained platforms (mobile/consoles), pool capacities must be budgeted carefully against peak concurrent entity limits.
- **Time-Slicing Fidelity:** The 4-frame cadence in Phase 4.1 staggers CPU calculations across frames. While imperceptible for ambient AI workloads, safety-critical gameplay calculations (e.g., authoritative physics) require per-frame execution or execution on worker threads via the Job System.
- **Modest Frame-Time Gain in Phase 4.4:** `GameObject.FindGameObjectsWithTag` was not a multi-millisecond bottleneck under 75 objects. Its primary real-world benefit is eliminating the recurring ~1.8 KB/frame array allocation and decoupling spawner checks from total scene hierarchy size.
- **Editor Profiler Overhead:** Profiling inside the Unity Editor includes EditorLoop, IMGUI (`GameHUD`), and scene view repaint overheads (~4–6 ms). Standalone release builds will run even faster.

---

## Screenshots

| View | Artifact Path | Description |
| :--- | :--- | :--- |
| **Baseline CPU Profiler** | [`Assets/Screenshots/baseline_cpu_profiler.png`](Assets/Screenshots/baseline_cpu_profiler.png) | 72.15 ms CPU math bottleneck in `EnemyWorkload.ExecuteCpuWorkload`. |
| **Baseline GC Profiler** | [`Assets/Screenshots/baseline_gc_profiler.png`](Assets/Screenshots/baseline_gc_profiler.png) | 540 KB/frame heap allocations in `ExecuteGcAllocations`. |
| **CPU Optimization** | [`Assets/Screenshots/cpu_optimization_profiler.png`](Assets/Screenshots/cpu_optimization_profiler.png) | CPU workload reduced to 2.85 ms via 15 iterations and static LUT. |
| **GC Optimization** | [`Assets/Screenshots/gc_optimization_profiler.png`](Assets/Screenshots/gc_optimization_profiler.png) | 100% elimination of enemy GC churn (0 B/frame); framerate reaches 50 FPS. |
| **Object Pooling** | [`Assets/Screenshots/object_pooling_profiler.png`](Assets/Screenshots/object_pooling_profiler.png) | Elimination of runtime `Instantiate`/`Destroy`; framerate reaches 75.9 FPS. |
| **Query Optimization** | [`Assets/Screenshots/query_optimization_profiler.png`](Assets/Screenshots/query_optimization_profiler.png) | Elimination of scene-wide tag search; $O(1)$ `EnemyRegistry` active; 78.5 FPS. |

---

## Skills Demonstrated

- **Unity & C#:** Advanced component architecture, memory layout, lifecycle hooks, interface design (`IPoolable`), and domain reload management.
- **Performance Profiling & Diagnosis:** In-depth profiler hierarchy analysis, GC allocation tracing, marker instrumentation, and Frame Debugger inspection.
- **CPU Optimization:** Algorithm complexity reduction, calculation time-slicing/staggering, static lookup tables (LUT), and inter-frame result caching.
- **Memory Optimization:** Managed heap allocation elimination, value-type data structures, buffer reuse, and GC collection hitch mitigation.
- **Object Lifecycle Management:** Custom Object Pooling, pre-warming, capacity budgeting, and state sanitization.
- **Data Structures & Query Design:** $O(1)$ registry design using `HashSet<T>` vs $O(N)$ scene-wide tagged hierarchy searches.
- **Rigorous Benchmarking:** Empirical A/B testing, reproducible stress scenarios, and professional technical documentation.

---

## How to Run

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/<your-username>/UnityPerformanceDebuggingDemo.git
   ```
2. **Open Project:** Launch **Unity Hub** and open the project using **Unity 2021.3.33f1**.
3. **Open Test Scene:** Navigate to `Assets/Scenes/` and open `ArenaScene.unity`.
4. **Enter Play Mode:** Press the **Play** button in the Unity Editor toolbar.
5. **Gameplay Controls:**
   - **WASD / Arrow Keys:** Move player.
   - **Mouse / Space:** Aim and fire projectiles (auto-fire active under stress test).
6. **Toggle Performance Modes:**
   - Press **`B`** on the keyboard at runtime to toggle between **Baseline Mode (Unoptimized)** and **Optimized Mode**.
   - Observe the live framerate and diagnostic metrics on the top-left HUD overlay.
7. **Open Unity Profiler:** Open **Window $\rightarrow$ Analysis $\rightarrow$ Profiler** (`Ctrl+7`) to inspect live CPU Main Thread and Memory allocation markers.
