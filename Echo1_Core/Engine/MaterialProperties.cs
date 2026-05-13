// Echo1_RcsSimulator\Echo1_Core\Engine\MaterialProperties.cs
using Echo1.Core.Radar;
using System.Numerics;
using Complex = System.Numerics.Complex;

namespace Echo1.Core.Engine;

/// <summary>
/// Represents electromagnetic material properties for RCS computation.
///
/// Two surface types are modelled:
///  1. Perfect Electric Conductor (PEC) — infinite conductivity, Γ = ±1
///  2. Dielectric / RAM coating — uses single-layer Fresnel with complex permittivity
///
/// For a single-layer RAM coating on a PEC ground plane, the input impedance method
/// (Knott §5.3) is used:
///
///   Z_in = j·Z_d·tan(k_d·t)
///   Γ = (Z_in - Z_0) / (Z_in + Z_0)
///
/// where Z_d = Z_0 / sqrt(ε_r·μ_r), k_d = k_0·sqrt(ε_r·μ_r), t = layer thickness.
/// </summary>
public sealed class MaterialProperties
{
	public static readonly MaterialProperties PEC = new()
	{
		IsPec = true,
		Name = "PEC (perfect conductor)"
	};

	public static readonly MaterialProperties Aluminum = new()
	{
		IsPec = true,
		Name = "Aluminium (effective PEC at radar frequencies)"
	};

	public string Name { get; init; } = "PEC";

	/// <summary>True for ideal PEC surfaces (Γ = ±1 depending on polarization)</summary>
	public bool IsPec { get; init; } = true;

	/// <summary>Complex relative permittivity ε_r = ε' - jε'' (ε'' > 0 for lossy material)</summary>
	public Complex RelativePermittivity { get; init; } = Complex.One;

	/// <summary>Complex relative permeability μ_r (1.0 for non-magnetic RAM)</summary>
	public Complex RelativePermeability { get; init; } = Complex.One;

	/// <summary>Coating layer thickness in metres (0 = bare metal)</summary>
	public double LayerThicknessM { get; init; } = 0.0;

	/// <summary>
	/// Compute Fresnel monostatic reflection coefficient for a given incidence angle
	/// and polarisation. Returns a complex scalar multiplier for the PO amplitude.
	///
	/// For PEC: Γ_VV = +1, Γ_HH = -1 (sign convention: positive = no phase reversal).
	/// The engine uses this consistently, so cross-polarisation terms are zero in monostatic PO.
	///
	/// For RAM coating: uses single-layer impedance model.
	/// </summary>
	public Complex FresnelReflection(double cosTheta, Polarisation pol)
	{
		if (IsPec)
		{
			return pol switch
			{
				Polarisation.VV => Complex.One,
				Polarisation.HH => -Complex.One,
				Polarisation.HV => Complex.Zero,
				Polarisation.VH => Complex.Zero,
				_ => Complex.One
			};
		}

		// Dielectric coating on PEC ground plane
		// sinTheta from cosTheta:
		double sinTheta2 = 1.0 - cosTheta * cosTheta;
		Complex er = RelativePermittivity;
		Complex mr = RelativePermeability;
		Complex sqrtEm = ComplexSqrt(er * mr);

		// Propagation constant in coating
		double k0 = 2.0 * Math.PI / PhysicsConstants.C; // at 1 Hz — caller scales by freq
														// Note: k0 is multiplied externally; here we work with normalised expressions.
														// Internal angle cos via Snell:
		Complex cosT_d = ComplexSqrt(1.0 - sinTheta2 / (er * mr));

		// Single-layer input impedance (no transmission; PEC ground plane → Γ_metal = -1)
		// Z_in normalized to Z_0:
		double betaT = LayerThicknessM; // pre-multiplied by k_d outside; here kept generic
		Complex tanArg = Complex.ImaginaryOne * sqrtEm * cosT_d * LayerThicknessM;
		Complex tanVal = (ComplexExp(tanArg) - ComplexExp(-tanArg))
					   / (ComplexExp(tanArg) + ComplexExp(-tanArg));

		Complex Z_norm = (mr / cosT_d) * Complex.ImaginaryOne * tanVal;

		Complex gamma_V = (Z_norm * cosTheta - 1.0) / (Z_norm * cosTheta + 1.0);
		Complex gamma_H = (Z_norm - cosTheta) / (Z_norm + cosTheta);

		return pol switch
		{
			Polarisation.VV => gamma_V,
			Polarisation.HH => gamma_H,
			_ => Complex.Zero
		};
	}

	private static Complex ComplexSqrt(Complex c)
	{
		double r = c.Magnitude;
		double theta = Math.Atan2(c.Imaginary, c.Real);
		return new Complex(Math.Sqrt(r) * Math.Cos(theta / 2),
						   Math.Sqrt(r) * Math.Sin(theta / 2));
	}

	private static Complex ComplexExp(Complex c)
		=> new(Math.Exp(c.Real) * Math.Cos(c.Imaginary),
			   Math.Exp(c.Real) * Math.Sin(c.Imaginary));

	/// <summary>
	/// Factory: RAM coating parameterized by relative permittivity and thickness.
	/// Example: Dallenbach layer, ferrite tile, carbon-loaded foam.
	/// </summary>
	public static MaterialProperties Ram(string name, Complex epsilon_r, double thicknessM,
		Complex? mu_r = null) => new()
		{
			Name = name,
			IsPec = false,
			RelativePermittivity = epsilon_r,
			RelativePermeability = mu_r ?? Complex.One,
			LayerThicknessM = thicknessM
		};
}

/// <summary>
/// Predefined common materials for convenience.
/// </summary>
public static class KnownMaterials
{
	/// Typical carbon-loaded foam (Eccosorb-like), ε_r ≈ 3.5 - j1.0, 10mm
	public static MaterialProperties CarbonFoam10mm =>
		MaterialProperties.Ram("Carbon foam 10mm",
			new Complex(3.5, -1.0), 0.010);

	/// Ferrite tile (W-type), ε_r ≈ 12 - j3, μ_r ≈ 4 - j2, 3mm
	public static MaterialProperties FerriteTile3mm =>
		MaterialProperties.Ram("Ferrite tile 3mm",
			new Complex(12.0, -3.0), 0.003,
			new Complex(4.0, -2.0));

	/// Multilayer dielectric approximation, ε_r ≈ 6 - j1.5, 5mm
	public static MaterialProperties DielectricCoating5mm =>
		MaterialProperties.Ram("Dielectric coating 5mm",
			new Complex(6.0, -1.5), 0.005);

	/// Pure aluminium (highly conductive radar reflector), approximated as a lossy near-PEC at microwave frequencies.
	public static MaterialProperties Aluminium =>
		MaterialProperties.Ram("Aluminium",
			new Complex(1.0, -1.0e7), 0.020);

	/// Titanium alloy (conductive structural metal with higher RF losses than aluminium)
	public static MaterialProperties Titanium =>
		MaterialProperties.Ram("Titanium alloy",
			new Complex(1.0, -2.5e6), 0.020);
}
