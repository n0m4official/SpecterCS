# SpecterCS

### *RCS Simulation Platform — “Reveal the Invisible.”*

#### IMPORTANT TO NOTE:
This project is being developed in my spare time alongside my full-time post-secondary studies. Development priorities are driven primarily by learning objectives, research interests, and available time. Updates will be irregular, but the project remains active and will continue to evolve as time permits.

---

## Overview

**SpecterCS** is a real-time radar cross-section (RCS) simulation platform built in C#. It models electromagnetic scattering using **Physical Optics (PO)** and **edge diffraction (PTD-inspired)** techniques, with support for **parallel CPU computation** and **GPU acceleration**.

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

  * Physical Optics using analytic triangle integration (Ling–Lee–Chuang)
  * Edge diffraction using UTD/PTD-inspired models with Fresnel transition functions
  * Coherent phase-based summation across surfaces and edges

* **Material & Electromagnetic Modeling**

  * Perfect Electric Conductor (PEC) surfaces
  * Dielectric and Radar Absorbing Material (RAM) coatings
  * Fresnel-based reflection with complex permittivity/permeability

* **Hybrid Compute Architecture**

  * Multi-threaded CPU engine for accurate physics computation
  * GPU acceleration (ComputeSharp) for real-time visualization
  * Coherent CPU results + approximate GPU heatmap rendering

* **3D Visualization**

  * Interactive 3D viewport (HelixToolkit)
  * Per-facet RCS heatmap (dBsm)
  * Real-time parameter updates

* **Geometry Processing Pipeline**

  * OBJ and STL import
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
SpecterCS.Core
 ├── Engine        # RCS computation (PO + diffraction)
 ├── Geometry      # Mesh, facets, edges, LOD
 ├── Radar         # Radar configuration & physics
 ├── Import        # OBJ / STL loaders
 └── Gpu           # GPU compute pipeline

SpecterCS.Wpf
 ├── Rendering     # Heatmap + 3D scene
 ├── Controls      # UI components
 └── ViewModels    # MVVM structure (in progress)
```

---

## Simulation Model

### Physical Optics (PO)

Surface scattering is computed using the **analytic triangle integral** (Ling–Lee–Chuang formulation), derived from the Stratton–Chu equations.

Key characteristics:

* Exact phase integration across triangle vertices
* Coherent summation of scattered fields
* Correct amplitude normalization (k² / 2π)
* Material-dependent Fresnel reflection

---

### Edge Diffraction (UTD/PTD)

Edge contributions are modeled using a **Uniform Theory of Diffraction (UTD)** inspired approach:

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

---

## Getting Started

### Requirements

* .NET 6+ or later
* Windows (required for GPU acceleration)
* GPU with DirectX 12 support (optional but recommended)

---

### Run the Application

1. Download the archive
2. Unzip archive (DO NOT MOVE EXE FROM FOLDER)
3. Double click `Echo1.Wpf.exe`
4. Enjoy

---

### Load a Model

* Click **“Load OBJ / STL”**
  * **NOTE:** OBJ files are **NOT RECOMMENDED** as they have been shown to demonstrate impossibly high return values in testing
      * Caused by non-manifold geometry or vertex welding issues from OBJ format itself   
* Select a 3D mesh file
* The system will:

  * Import geometry
  * Build edges
  * Generate LODs
  * Begin real-time simulation

---

## Controls

| Control                    | Function          |
| -------------------------- | ----------------- |
| Mouse (Right Click + Drag) | Rotate camera     |
| W / A / S / D              | Move camera       |
| Q / E                      | Vertical movement |
| Shift                      | Faster movement   |

---

## Output

* **RCS (dBsm)** — logarithmic radar signature
* **RCS (m²)** — linear radar cross-section
* **Heatmap Visualization** — per-facet contribution

---

## Current Limitations

* Diffraction model is UTD-inspired and not a full multi-bounce diffraction solution
* No multiple scattering (single-bounce PO + edge diffraction only)
* Limited validation against measured RCS datasets
* GPU path uses centroid-based approximation (CPU path is authoritative)
* No time-domain or transient simulation

---

## Roadmap

* [X] Advanced diffraction models (full PTD / UTD)
* [X] Material and dielectric modeling
* [X] Polarization handling (HH, VV, HV, VH)
* [ ] Polar RCS plots
* [ ] Time-domain simulation
* [ ] Cloud / distributed computation
* [ ] AI-assisted stealth optimization

---

## Design Philosophy

SpecterCS is built around three principles:

* **Clarity** — visualize complex electromagnetic behavior intuitively
* **Performance** — leverage parallelism and GPU acceleration
* **Extensibility** — modular design for future expansion

---

## Status

Active development — evolving toward a professional-grade simulation platform.

---

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

---
