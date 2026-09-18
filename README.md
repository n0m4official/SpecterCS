# SpecterCS

### *RCS Simulation Platform — "Reveal the Invisible."*

#### **Current Status: Hiatus (as of September 13, 2026)**

**Current Release: v1.3.0** ([full changelog](https://github.com/n0m4official/SpecterRCS/releases))

#### A note on development pace

This project is developed in my spare time alongside full-time post-secondary studies (as of mid-2026, a capstone thesis) in addition to starting military training and service. Development is driven by learning objectives, research interest, and available time, so updates are irregular in cadence but not in seriousness — every release fixes or improves the actual electromagnetic modeling, not just UI polish. See [Status](#status) below for what's currently open.

---

## Overview

**SpecterCS** is a real-time radar cross-section (RCS) simulation platform built in C#. It models electromagnetic scattering using **Physical Optics (PO)** and **edge diffraction (UTD/PTD-inspired)** techniques, with support for **parallel CPU computation** and **GPU acceleration**.

The system provides interactive 3D visualization, enabling analysis of radar signatures across varying frequencies, azimuth, and elevation angles.

User guide can be found [Here](#guide).

---

## Screenshot

<p align="center">
  <img src="Screenshot 2026-04-01 140506.png" width="520" alt="Active software running using a model of an Airbus A320 NEO">
  <br>
  <em>Active software (v1.1.1) running using a model of an Airbus A320 NEO </br> Model: https://www.printables.com/model/552457-airbus-a320-neo/files</em>
</p>

---

## Core Features

* **High-Fidelity RCS Computation**
  * Physical Optics using an exact planar-polygon phase integral (Stokes'-theorem boundary formulation, replacing the earlier Ling–Lee–Chuang centroid-based approach in v1.2.1)
  * Edge diffraction using UTD/PTD-inspired models with Fresnel transition functions
  * Coherent phase-based summation across surfaces and edges
  * Broadside limit handling for constant-phase facets

* **Material & Electromagnetic Modeling**
  * Perfect Electric Conductor (PEC) surfaces
  * Dielectric and Radar Absorbing Material (RAM) coatings
  * Fresnel-based reflection with complex permittivity/permeability
  * Built-in material presets: PEC, Aluminium, Titanium Alloy (v1.2.1+)

* **Hybrid Compute Architecture**
  * Multi-threaded CPU engine for accurate physics computation
  * GPU acceleration (ComputeSharp) for real-time visualization
  * Coherent CPU results + approximate GPU heatmap rendering

* **3D Visualization**
  * Interactive 3D viewport (HelixToolkit)
  * Per-facet RCS heatmap (dBsm)
  * Real-time parameter updates

* **Geometry Processing Pipeline**
  * OBJ and STL import (STL strongly recommended — see [Model Prep](#model-prep--mesh-requirements))
  * Automatic triangulation
  * Edge extraction with dihedral angle computation
  * Level-of-detail (LOD) mesh decimation

* **Radar Simulation**
  * Configurable frequency (GHz range)
  * Full azimuth/elevation control
  * Polarization support (HH, VV, HV, VH)
  * Real-time sweep computation

* **Caching & Performance Optimization**
  * Angular and frequency quantization
  * Polarization-aware caching
  * Fast recomputation for interactive workflows

---

## Architecture

SpecterCS is structured as a modular system:

```
Echo1_Core  (SpecterCS_Core.csproj)
 ├── Engine        # RCS computation: PhysicalOpticsKernel, EdgeDiffractionKernel, RcsEngine, RcsCache, BackFaceCuller, MaterialProperties
 ├── Geometry       # RcsMesh, Facet, BoundingBox, Vector3d, MeshDecimator (LOD)
 ├── Radar          # RadarConfig, FrequencyBand, RadarSweepState
 ├── Import         # ObjImporter, StlImporter
 └── GPU            # GpuRcsCompute, RcsShader, FacetGpuData

Echo1_Wpf  (SpecterCS_Wpf.csproj)
 ├── Rendering      # SceneBuilder, FreeFlyCamera, HeatmapColorMap, FacetMaterialCache
 ├── Controls       # RadarSweepControl, RcsPolarPlot
 └── ViewModels     # MainViewModel, HeatmapViewModel, RadarConfigViewModel (MVVM)
```

---

## Simulation Model

### Physical Optics (PO)

Surface scattering is computed using an **exact planar-polygon phase integral**, derived via a Stokes'-theorem boundary integral (replacing the earlier per-triangle Ling–Lee–Chuang analytic integral as of v1.2.2, for better numerical behavior on large/near-broadside facets).

Key characteristics:

* Exact phase integration across facet boundaries
* Coherent summation of scattered fields
* Correct amplitude normalization (k² scaling)
* Material-dependent Fresnel reflection
* Explicit broadside-limit handling for constant-phase facets

---

### Edge Diffraction (UTD/PTD)

Edge contributions are modeled using a **Uniform Theory of Diffraction (UTD)**-inspired approach, using an edge-weighted term (`EdgeTerm`, replacing the earlier `EdgeIntegral` as of v1.2.2) for improved handling of degenerate geometry:

* Wedge-based diffraction using dihedral angles
* Kouyoumjian–Pathak transition function for boundary smoothing
* Fresnel integral evaluation (series, asymptotic, and numerical quadrature)
* Monostatic coherent edge integration

---

### Material Interaction

Electromagnetic interaction is modeled via:

* Fresnel reflection coefficients (angle + polarization dependent)
* Complex permittivity and permeability
* Single-layer RAM coating approximation using impedance methods
* Preset materials: PEC, Aluminium, Titanium Alloy

---

## Getting Started

### Requirements

* .NET 6 or later
* Windows (required for GPU acceleration)
* GPU with DirectX 12 support (optional but recommended)

### Run the Application

1. Download the latest release archive
2. Unzip archive (**do not move the .exe out of the folder** — it depends on relative paths for assets/dependencies)
3. Double-click `Echo1.Wpf.exe`
4. Enjoy

---

## Guide

**Version:** v1.3.0  
**Application:** SpecterCS — RCS Simulator

> **Important:** SpecterCS is an educational and research visualization tool. Its results are not validated for engineering, safety-critical, operational, or defence decisions.

## 1. What SpecterCS does

SpecterCS estimates the monostatic radar cross section (RCS) of a 3D target mesh.

It provides:

- Physical Optics (PO) surface-scattering calculations.
- UTD/PTD-inspired edge-diffraction estimates.
- Coherent total RCS reporting in dBsm and square metres.
- Per-facet RCS heatmap visualization.
- Frequency, azimuth, elevation, and polarization controls.
- Full azimuth sweeps with CSV export.
- Frequency sweeps with an on-screen plot.
- Whole-model material assignment.
- An experimental EM–thermal–fluid coupling step.

The CPU solver is the authoritative RCS path. GPU compute support exists in the project but is not the source of the RCS value shown in the application.

## 2. Requirements

- Windows
- .NET 9 SDK/runtime
- DirectX 12-capable GPU is optional
- A supported 3D model in `.stl` or `.obj` format

To run from source:

```powershell
dotnet restore SpecterCS.sln
dotnet run --project Echo1_Wpf\SpecterCS_Wpf.csproj
```

## 3. Quick start

1. Start SpecterCS.
2. Select **Load OBJ / STL…**
3. Choose a model.
4. Confirm the model dimensions are expressed in **metres**.
5. Adjust frequency, azimuth, elevation, and polarization.
6. Read the RCS result and inspect the heatmap.

For the most reliable import, use a clean, watertight STL mesh.

## 4. Loading a model

Select **Load OBJ / STL…** in the **Model** panel.

After loading, the application displays:

- Number of facets
- Number of shared edges
- Bounding-box diagonal in metres

### Supported formats

| Format | Support | Notes |
|---|---|---|
| STL | Recommended | Use a clean, manifold, watertight mesh. |
| OBJ | Basic support | Only vertices and faces are used. Texture coordinates, normals, material files, and most advanced OBJ features are ignored. |

### Mesh requirements

Use meshes that are:

- Sized in metres.
- Closed/watertight where possible.
- Free of duplicate, zero-area, or severely overlapping triangles.
- Consistently wound, with outward-facing normals.
- Manifold, especially when using edge diffraction or the thermal solver.

Poor OBJ topology, inverted normals, non-manifold edges, or incorrect scale can produce misleading RCS values.

## 5. Navigating the 3D view

| Control | Action |
|---|---|
| Right-click + drag | Rotate the camera |
| `W` / `S` | Move forward / backward |
| `A` / `D` | Move left / right |
| `Q` / `E` | Move up / down |
| Hold `Left Shift` | Move faster |

## 6. Configuring the radar

### Frequency

Set frequency with the **Frequency (GHz)** slider.

Available range:

```text
1 GHz to 40 GHz
```

Preset buttons are provided for common bands:

| Preset | Frequency |
|---|---:|
| L | 1.3 GHz |
| S | 3.0 GHz |
| C | 5.5 GHz |
| X | 10.0 GHz |
| Ku | 16.0 GHz |
| Ka | 35.0 GHz |

Changing frequency recalculates the RCS.

### Azimuth and elevation

- **Azimuth:** −180° to +180°
- **Elevation:** −90° to +90°

The radar direction is calculated from these angles. Moving either slider recalculates the RCS and heatmap.

### Polarization

| Setting | Meaning |
|---|---|
| VV | Vertical transmit / vertical receive |
| HH | Horizontal transmit / horizontal receive |
| HV (cross) | Cross-polarized mode |

Cross-polarized behavior is currently approximate and should be treated as exploratory.

## 7. Reading the RCS result

The **RCS Result** panel displays:

| Value | Meaning |
|---|---|
| `dBsm` | RCS relative to one square metre: `10 × log10(RCS in m²)` |
| `m²` | Linear radar cross section |
| `PO only` | Surface-scattering contribution |
| `Edge diffraction` | Edge-diffraction contribution |

The total is a coherent EM result. It is not necessarily equal to a simple sum of the displayed component levels in dB.

### Heatmap

The model is coloured by per-facet RCS contribution:

```text
Dark blue → low contribution
Cyan/yellow → medium contribution
Red → high contribution
```

Use the **Display** panel to set the heatmap's minimum and maximum dBsm range.

The heatmap is useful for locating strong scattering regions, but it does not replace the coherent total RCS calculation.

## 8. Azimuth sweep

The **Sweep** panel supports manual and automated azimuth analysis.

### Automatic sweep

1. Set **Sweep rate (°/s)**.
2. Enable **Auto-sweep azimuth**.
3. The azimuth changes continuously and RCS updates during the sweep.

### Full sweep

Select **Compute full sweep** to compute:

```text
0° to 359° azimuth
1° spacing
```

When complete, SpecterCS draws a polar plot in the Sweep panel.

### Exporting an azimuth sweep

After a successful full sweep:

1. Select **Export CSV…**
2. Choose a destination and filename.

The CSV includes:

```text
azimuth_deg,rcs_dbsm
```

It also includes metadata for frequency, elevation, polarization, model name, and facet count.

## 9. Frequency sweep

The **Frequency sweep** panel evaluates RCS over a selected frequency interval.

1. Enter **Start GHz**.
2. Enter **Stop GHz**.
3. Select **Run frequency sweep**.

The application currently calculates 100 frequency samples and draws the result in the on-screen plot.

Frequency-sweep data is displayed in the application but is not exported by the current CSV export button.

## 10. Materials

The **Materials** panel applies one material to the entire loaded model.

Available choices:

| Material | Description |
|---|---|
| PEC (default) | Perfect electric conductor |
| Carbon foam RAM 10 mm | Carbon-loaded absorbing-material approximation |
| Ferrite tile 3 mm | Ferrite-based absorbing-material approximation |
| Dielectric coating 5 mm | Dielectric coating approximation |
| Aluminium 20 mm | Lossy aluminium approximation |
| Titanium Alloy 20 mm | Lossy titanium-alloy approximation |

To apply a selection:

1. Choose a material from the list.
2. Select **Apply to whole model**.
3. SpecterCS clears its RCS cache and recalculates the result.

The current interface does **not** provide region-selection controls, despite the panel text referring to a selected region.

## 11. Experimental EM–thermal–fluid simulation

SpecterCS includes an experimental single-step EM–thermal–fluid model.

The coupling sequence is:

```text
EM absorption
→ facet heating
→ conduction, convection, and radiation
→ surrounding-air temperature update
→ temperature-dependent EM material response
```

Select **Advance EM–thermal–fluid step** to advance one configured timestep.

### Important limitations

The current interface does not expose controls for:

- Incident power flux
- Ambient temperature
- Air velocity
- Pressure
- Timestep
- Number of coupling substeps
- Surface thermal properties

By default, `RadarConfig.IncidentPowerFluxWm2` is zero. This means normal RCS use does **not** heat the target, and selecting the multiphysics button will normally produce no meaningful thermal change.

For experimental developer use, configure the simulation in code before advancing it:

```csharp
_radar.IncidentPowerFluxWm2 = 1000.0;

_multiphysics = new CoupledSimulation(
    _mesh,
    _engine,
    new FlowConditions
    {
        AmbientTemperatureK = 293.15,
        AirTemperatureK = 293.15,
        VelocityMps = 20.0,
        PressurePa = 101325.0,
        CharacteristicLengthM = 1.0
    },
    new CoupledSimulationConfig
    {
        TimeStepSeconds = 0.05,
        CouplingSubsteps = 1
    });
```

This is a reduced-order surface model, not a computational-fluid-dynamics solver.

## 12. Interpreting results responsibly

RCS depends strongly on:

- Geometry scale
- Mesh quality
- Surface normal direction
- Frequency
- Viewing direction
- Polarization
- Material assumptions
- Edge topology
- Numerical approximation limits

Use the simulator to compare trends, visualize scattering regions, and explore parameter sensitivity.

Do not interpret a single result as a measured or certified RCS value.

## 13. Known limitations

- Only monostatic RCS is implemented.
- The CPU path is authoritative; GPU support is approximate and not used for the displayed final RCS.
- Edge diffraction is UTD/PTD-inspired, not a complete validated diffraction solution.
- Multiple scattering and multi-bounce effects are not modeled.
- OBJ import is intentionally basic.
- Mesh decimation is simple uniform facet sampling.
- Material data is approximate and should not be treated as validated characterization data.
- The heatmap uses per-facet values and is not a full field visualization.
- Frequency-sweep export is not currently available.
- The EM–thermal–fluid module is experimental and requires code configuration for meaningful heating.

## 14. Troubleshooting

### The RCS value seems too high

Check the following:

1. Confirm the mesh is scaled in metres.
2. Use a watertight STL instead of an OBJ where possible.
3. Verify outward-facing normals.
4. Check for duplicate geometry or overlapping shells.
5. Inspect the PO and edge-diffraction result breakdown.
6. Reduce mesh complexity only after confirming the original mesh is clean.
7. Treat values near diffraction boundaries with caution.

### The model is not visible or is difficult to inspect

- Use right-click drag to rotate.
- Use `W`, `A`, `S`, `D`, `Q`, and `E` to move.
- Load a mesh with a non-zero physical size.
- Verify the model contains valid triangles.

### The heatmap looks uniform

- Change azimuth, elevation, frequency, or material.
- Adjust **Heatmap min** and **Heatmap max**.
- Confirm that an RCS calculation has completed.

### The multiphysics step does not change temperature

Set a non-zero `IncidentPowerFluxWm2` in code. The default is zero to prevent ordinary RCS calculations from being interpreted as heating simulations.

---

## Model Prep & Mesh Requirements

This section exists to answer the most common setup questions (mesh format, materials, prerequisites).

### File format: use STL, not OBJ

OBJ files are **not recommended**. Many OBJ exports have topology and vertex-connectivity issues (non-manifold edges, duplicate/loose vertices) that the importer can't always catch, and these produce **inflated, non-physical RCS values**. This is a known, currently unresolved limitation (see [Status](#status)).

For reliable results:
* Export or convert your model to **STL**
* Make sure the mesh is **manifold** (closed, watertight, no self-intersections) — most CAD tools have a "check/repair mesh" or "make manifold" function
* Use **consistent metric units** — the importer assumes meters

### Loading a model

1. Click **"Load OBJ / STL"**
2. Select your mesh file
3. The system will import geometry, build edges, generate LODs, and begin real-time simulation
4. **Known first-load issue:** RCS values may appear impossibly high immediately after load. Move the elevation slider once to force a recompute — this resolves it. Root cause is still under investigation (see [Status](#status)).

### Defining materials

* Built-in presets: **PEC** (perfect conductor, theoretical upper-bound RCS), **Aluminium**, **Titanium Alloy**
* Materials are modeled via complex permittivity (εr) and permeability (μr); RAM/dielectric coatings use a single-layer Fresnel impedance approximation — multilayer (TMM) coatings are not yet supported.
* Custom coatings are not planned to be implemented due to legal reasons.

### What's *not* yet implemented

If you're looking for these, they're planned but not built — don't spend time trying to find them in the UI:
* Non-linear, spatially-variant surface impedance boundaries
* Multi-bounce (SBR) scattering — current engine is single-bounce PO + edge diffraction only
* Multilayer material coatings (TMM)
* Facet specific material overrides

---

## Controls

| Control                    | Function          |
| --------------------------- | ------------------ |
| Mouse (Right Click + Drag) | Rotate camera      |
| W / A / S / D               | Move camera        |
| Q / E                        | Vertical movement  |
| Shift                        | Faster movement    |

---

## Output

* **RCS (dBsm)** — logarithmic radar signature
* **RCS (m²)** — linear radar cross-section
* **Heatmap Visualization** — per-facet contribution

---

## Status

**Known open issues (as of v1.2.2):**
* OBJ files can still return inflated RCS values due to mesh-topology limitations — use STL (see [Model Prep](#model-prep--mesh-requirements))
* RCS values at certain angles will be impossibly large due to limitations with the engine. Fix is planned for release post v1.3.0.

**Current engine limitations:**
* Diffraction model is UTD-inspired, not a full multi-bounce diffraction solution
* No multiple scattering (single-bounce PO + edge diffraction only)
* Limited validation against measured RCS datasets
* GPU path uses centroid-based approximation (CPU path is authoritative)
* No time-domain or transient simulation

---
## Releases

| Release  | Date | Status | Notes |
| ------------- | ------------- | ------------- | ------------- |
| [v1.0.0](https://github.com/n0m4official/SpecterCS/releases/tag/V1.0.0) | March 24, 2026 | Not supported | Initial release |
| [v1.1.0](https://github.com/n0m4official/SpecterCS/releases/tag/V1.1.0) | April 1, 2026 | Not supported | Core Physics and Kernel Corrections, Material and Radar Absorbent Coatings, Engine and UI Improvements |
| [v1.1.1](https://github.com/n0m4official/SpecterCS/releases/tag/V1.1.1) | April 1, 2026 | Not supported | Emergency physics patch for v1.1.0 |
| [v1.2.1](https://github.com/n0m4official/SpecterCS/releases/tag/V1.2.1) | May 13, 2026 | Not supported | Added Aluminum and Titanium alloys to materials |
| [v1.2.2](https://github.com/n0m4official/SpecterCS/releases/tag/V1.2.2) | July 4, 2026 | Not supported | Reworked `PhysicalOpticsKernel` |
| [v1.3.0](https://github.com/n0m4official/SpecterCS/releases/tag/V1.3.0) | August 24, 2026 | Latest | Misc Bug fixes and implemented Bi-directional multiphysics coupling (EM–thermal–fluid) |

---

## Roadmap

**Done:**
* [x] Advanced diffraction models (UTD, exact planar-polygon PO)
* [x] Material and dielectric modeling
* [x] Polarization handling (HH, VV, HV, VH)
* [x] Material presets (PEC, Aluminium, Titanium Alloy)
* [x] Bi-directional multiphysics coupling (EM–thermal–fluid)

**Planned:**
* [ ] Fix OBJ import inflation and first-load RCS bug
* [ ] Polar RCS plots (`RcsPolarPlot` control) 
* [ ] Implement import for STEP files for higher LOD models
* [ ] Non-linear, spatially-variant surface impedance (Zs) boundaries
* [ ] Fluidic-embedded substrate homogenization
* [ ] High-G dynamic loading, deformable mesh
* [ ] Quantum capacitance (Cq) graphene models
* [ ] Galinstan oxidation layer modeling
* [ ] Time-domain simulation
* [ ] Cloud / distributed computation
* [ ] AI-assisted stealth optimization

---

## Design Philosophy

SpecterCS is built around three principles:

* **Clarity** — visualize complex electromagnetic behavior intuitively
* **Performance** — leverage parallelism and GPU acceleration
* **Extensibility** — modular design for future expansion

## What Makes SpecterCS Different

* Uses **analytic electromagnetic formulations**, not heuristic approximations
* Separates **visualization (GPU)** from **physics computation (CPU)**
* Supports **material-aware RCS modeling**, not just geometry-based scattering
* Designed as a **modular simulation engine**, not a single-purpose tool

---

## Disclaimer

This project is intended for **educational, research, and visualization purposes only**. It is not a validated engineering tool and should not be used for real-world defense or safety-critical applications.

---

## Author

Developed by **Mathew Dixon**
