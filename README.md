# SpecterCS

### *RCS Simulation Platform — "Reveal the Invisible."*

**Current Release: v1.2.2** ([full changelog](https://github.com/n0m4official/SpecterRCS/releases))

#### A note on development pace

This project is developed in my spare time alongside full-time post-secondary studies (and, as of mid-2026, a capstone thesis). Development is driven by learning objectives, research interest, and available time, so updates are irregular in cadence but not in seriousness — every release fixes or improves the actual electromagnetic modeling, not just UI polish. See [Status](#status) below for what's currently open.

---

## Overview

**SpecterCS** is a real-time radar cross-section (RCS) simulation platform built in C#. It models electromagnetic scattering using **Physical Optics (PO)** and **edge diffraction (UTD/PTD-inspired)** techniques, with support for **parallel CPU computation** and **GPU acceleration**.

The system provides interactive 3D visualization, enabling analysis of radar signatures across varying frequencies, azimuth, and elevation angles.

---

## Screenshot

<p align="center">
  <img src="Screenshot 2026-04-01 140506.png" width="520" alt="Active software running using a model of an Airbus A320 NEO">
  <br>
  <em>Active software running using a model of an Airbus A320 NEO </br> Model: https://www.printables.com/model/552457-airbus-a320-neo/files</em>
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
* Fluid/thermal simulation and EM-thermal-fluid coupling
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
* On first load, RCS values may appear impossibly high until the elevation slider is moved once — root cause not yet identified

**Current engine limitations:**
* Diffraction model is UTD-inspired, not a full multi-bounce diffraction solution
* No multiple scattering (single-bounce PO + edge diffraction only)
* Limited validation against measured RCS datasets
* GPU path uses centroid-based approximation (CPU path is authoritative)
* No time-domain or transient simulation

---

## Roadmap

**Done:**
* [x] Advanced diffraction models (UTD, exact planar-polygon PO)
* [x] Material and dielectric modeling
* [x] Polarization handling (HH, VV, HV, VH)
* [x] Material presets (PEC, Aluminium, Titanium Alloy)
* [x] Polar RCS plots (`RcsPolarPlot` control) <!-- confirm this is wired up / feature-complete, not just scaffolded -->

**Planned:**
* [ ] Fix OBJ import inflation and first-load RCS bug
* [ ] Bi-directional multiphysics coupling (EM–thermal–fluid)
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
