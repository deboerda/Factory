# Factory — Manufacturing Digital Twin (Unity)

> Unity sample of a **shop-floor / production-line digital twin**: animated equipment, workpiece flow, and scene camera orchestration. Portfolio piece from an automotive-parts manufacturing digitalization era (first production digital-twin rollout; **~30%** improvement in business-process automation — LinkedIn / resume metric).

[![Unity](https://img.shields.io/badge/Unity-URP-black?logo=unity)](#)
[![C#](https://img.shields.io/badge/C%23-Scripts-239120)](#)

## What this demonstrates

- **Production-line visualization** — pipeline / workstation simulation (`PipelineSimulator`), workpiece spawn & parenting, machine guards
- **Robot / arm motion** — `ArmRealTimeSimulator` for articulated equipment driven in real time
- **Scene UX** — orbit camera switcher, skylight control, scene loader for multi-area tours
- **Data hooks** — DB reader/manager stubs for plant-side data binding (demo wiring)
- **Art pipeline** — PBR materials, terrain, TextMesh Pro, URP settings suitable for plant walkthrough demos

## Highlights

| Module | Script | Role |
|--------|--------|------|
| Line flow | `PipelineSimulator`, `Workpiece*` | Workpiece motion along the line |
| Equipment | `ArmRealTimeSimulator`, `MachineGuardController` | Arms & safety guards |
| Camera / tour | `OrbitCameraSwitcher`, `LookAtTarget`, `SceneLoader` | Operator viewpoint |
| App shell | `ApplicationManager`, `SkylightController` | Runtime orchestration |
| Data | `DbManager`, `DbReader_*` | Plant data read path (sample) |

## Tech stack

Unity · URP · C# · TextMesh Pro · NuGet/plugins as needed for data access demos

## Repo layout

```
Assets/Script/     # Production twin controllers
Assets/Scenes/     # Factory scenes
Assets/Moudl/ …    # Models / props
```

## Role

Digital-systems / Unity visualization work for **manufacturing digital twin** — cyber-physical shop-floor view used in production digitalization.

---

*Generic manufacturing framing (no OEM / plant names). 制造业数字孪生 Unity 示例 — 产线、工件流、设备动画与场景漫游。*
