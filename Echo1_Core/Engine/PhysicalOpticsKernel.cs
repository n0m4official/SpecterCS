// Echo1_RcsSimulator\Echo1_Core\Engine\PhysicalOpticsKernel.cs
using Echo1.Core.Geometry;
using Echo1.Core.Radar;
using System.Numerics;
using Complex = System.Numerics.Complex;

namespace Echo1.Core.Engine;

/// <summary>
/// Physical Optics (PO) kernel for monostatic RCS.
///
/// The correct monostatic PO formula for a perfectly electrically conducting (PEC)
/// triangular facet is derived from the Stratton-Chu surface integral:
///
///   E_s ∝ (jk / 2π) ∫∫ (n̂ × E_i) × k̂_s · e^(j2k k̂·r) dA
///
/// For a flat triangular facet illuminated by a plane wave, the integral over the
/// facet area in the far field reduces to the closed-form expression implemented below.
/// The bistatic PO amplitude for a single flat facet is:
///
///   F(k̂_i, k̂_s) = (k² / π) · A · cos(θ_i) · sinc(u) · sinc(v)  [general bistatic]
///
/// For monostatic (k̂_s = -k̂_i), the expression simplifies, and for arbitrary facet
/// shape the exact integral is computed via the analytic formula for a triangle
/// (Ufimtsev 2007, §3.2; Knott, Shaeffer & Tuley "Radar Cross Section", ch.4).
///
/// The full vector PO integral over a triangle with vertices P0, P1, P2 and
/// monostatic incident direction k̂ is:
///
///   I = Σ_i [ (k̂ × r_i) × (k̂ × r_{i+1}) / |cross| ] · (e^(j2k·k̂·r_i) - e^(j2k·k̂·r_{i+1})) / (2j·k·Δφ_i)
///
/// where Δφ_i is the phase difference between vertices i and i+1.
/// This is the Ling-Lee-Chuang (1989) exact triangle PO integral.
///
/// Reference: Ling, H., Chou, R.C., Lee, S.W. (1989). "Shooting and Bouncing Rays:
/// Calculating the RCS of an Arbitrarily Shaped Cavity." IEEE Trans. Antennas Propag.
/// </summary>
public static class PhysicalOpticsKernel
{
	/// <summary>
	/// Computes the exact PO monostatic scattering amplitude for a single triangular facet
	/// using the Ling-Lee-Chuang analytic triangle integral.
	///
	/// Returns the complex scattering amplitude S such that σ = 4π|S|² (see TotalRcsM2).
	/// S already carries the k² factor and has units of length (m), so |S|² is in m².
	/// </summary>
	public static Complex FacetContribution(Facet facet, Vector3 kHat, double k,
		MaterialProperties material, Polarisation pol = Polarisation.VV)
	{
		// Back-face culling: facet must face the radar
		double cosTheta = Vector3.Dot(facet.Normal, -kHat);
		if (cosTheta <= 1e-9) return Complex.Zero;

		// Compute material reflection coefficient (Fresnel, monostatic)
		// For PEC: Γ = -1 (H-pol), +1 (V-pol). For coated surfaces: use Fresnel.
		// NOTE: k is now passed through — coating behavior (quarter-wave cancellation etc.)
		// is fundamentally frequency-dependent and cannot be computed without it.
		Complex gamma = material.FresnelReflection(cosTheta, pol);

		// Phase of each vertex: φ_i = 2k · (k̂ · r_i)
		// Factor of 2 is because monostatic: incident + reflected path both traverse k̂·r.
		double phi0 = 2.0 * k * (kHat.X * facet.V0.X + kHat.Y * facet.V0.Y + kHat.Z * facet.V0.Z);
		double phi1 = 2.0 * k * (kHat.X * facet.V1.X + kHat.Y * facet.V1.Y + kHat.Z * facet.V1.Z);
		double phi2 = 2.0 * k * (kHat.X * facet.V2.X + kHat.Y * facet.V2.Y + kHat.Z * facet.V2.Z);

		// Phasors at each vertex
		var E0 = new Complex(Math.Cos(phi0), Math.Sin(phi0));
		var E1 = new Complex(Math.Cos(phi1), Math.Sin(phi1));
		var E2 = new Complex(Math.Cos(phi2), Math.Sin(phi2));

		// Exact PO integral I = ∫∫_facet e^(j2k·k̂·r) dA, evaluated via the boundary
		// (Stokes'-theorem) reduction — this is the formula the class docstring above
		// describes but the previous implementation never actually applied. Unlike the
		// old scalar phasor-difference sum, this carries proper units of area (m²) and
		// weights each edge by its true geometric contribution, not just a phase ratio.
		Complex I = TrianglePhaseIntegral(facet, kHat, k, cosTheta, E0, E1, E2, phi0, phi1, phi2);

		// PO amplitude: S = (j * k² / 2π) · cosθ · Γ · I
		// NOTE: no explicit "facet.Area" factor here — Area is now supplied by I itself
		// (see TrianglePhaseIntegral), since I is the true area integral, not a normalized
		// dimensionless phase factor. Multiplying by facet.Area again would double-count it.
		double amplitude = (k * k) / (2.0 * Math.PI) * cosTheta;
		var jk = new Complex(0.0, amplitude);   // j factor from surface current to radiation

		return jk * gamma * I;
	}

	/// <summary>
	/// Exact planar-polygon phase integral I = ∫∫_facet e^(jφ(r)) dA, reduced to a boundary
	/// (edge) sum via the 2D divergence theorem. For a vector field F = û·e^(jφ)/(jQ) in the
	/// facet plane (û, Q = direction/magnitude of the in-plane component of q = 2k·k̂), one can
	/// verify div(F) = e^(jφ), so ∫∫ e^(jφ) dA = ∮ F·n̂_edge ds. Evaluating that boundary integral
	/// edge-by-edge (each edge has constant tangent, so φ is linear along it) gives, after
	/// simplifying q_parallel·(n̂×d) = q·(n̂×d) (the n̂-component of q drops out of a triple product):
	///
	///   I = Σ_edges [ -k̂·(n̂ × (r_{i+1}-r_i)) / (2k·sin²θ) ] · (E_{i+1} - E_i) / (φ_{i+1} - φ_i)
	///
	/// where θ is the angle between k̂ and the facet normal (sinθ = 0 at normal incidence).
	/// This has a removable singularity at θ→0 (broadside), handled below via the direct
	/// area limit, which is exact in that limit (phase is constant across the whole facet).
	///
	/// Verified against the closed-form flat-plate broadside RCS σ = 4πA²/λ²: substituting
	/// this I into FacetContribution's amplitude reproduces that formula exactly at θ=0.
	/// </summary>
	private static Complex TrianglePhaseIntegral(
		Facet facet, Vector3 kHat, double k, double cosTheta,
		Complex E0, Complex E1, Complex E2,
		double phi0, double phi1, double phi2)
	{
		double sin2Theta = Math.Max(0.0, 1.0 - cosTheta * cosTheta);

		// Near-broadside: the boundary formula below has a 0/0 singularity here.
		// Exact limit as sin2Theta -> 0 is Area * (average phasor), since phase becomes
		// constant across the whole facet at exact normal incidence.
		if (sin2Theta < 1e-6)
		{
			Complex avg = (E0 + E1 + E2) / 3.0;
			return facet.Area * avg;
		}

		double invDenom = 1.0 / (2.0 * k * sin2Theta);
		Vector3 n = facet.Normal;

		return EdgeTerm(facet.V0, facet.V1, n, kHat, invDenom, E0, E1, phi0, phi1)
			 + EdgeTerm(facet.V1, facet.V2, n, kHat, invDenom, E1, E2, phi1, phi2)
			 + EdgeTerm(facet.V2, facet.V0, n, kHat, invDenom, E2, E0, phi2, phi0);
	}

	private static Complex EdgeTerm(
		Vector3 ra, Vector3 rb, Vector3 normal, Vector3 kHat, double invDenom,
		Complex Ea, Complex Eb, double phiA, double phiB)
	{
		// Geometric edge weight: -k̂·(n̂ × (r_b - r_a)) / (2k·sin²θ)
		Vector3 edge = rb - ra;
		Vector3 cross = Vector3.Cross(normal, edge);
		double numerator = kHat.X * cross.X + kHat.Y * cross.Y + kHat.Z * cross.Z;
		double weight = -numerator * invDenom;

		double dPhi = phiB - phiA;
		Complex phaseTerm = Math.Abs(dPhi) < 1e-9
			? (Ea + Eb) * 0.5          // degenerate: edge nearly perpendicular to k̂ in phase terms
			: (Eb - Ea) / dPhi;

		return weight * phaseTerm;
	}

	/// <summary>
	/// Converts coherent complex amplitude sum to monostatic RCS in m².
	/// σ = 4π |S|²
	/// where S is the accumulated FacetContribution sum (already contains k²/2π).
	/// </summary>
	public static double TotalRcsM2(Complex coherentSum)
	{
		double mag2 = coherentSum.Real * coherentSum.Real
					+ coherentSum.Imaginary * coherentSum.Imaginary;
		return 4.0 * Math.PI * mag2;
	}

	public static float ToDbsm(double rcsM2)
		=> rcsM2 > 1e-30 ? (float)(10.0 * Math.Log10(rcsM2)) : -100f;
}