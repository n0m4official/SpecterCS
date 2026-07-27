using Echo1.Core.Engine;
using Echo1.Core.Geometry;
using Echo1.Core.Import;
using Echo1.Core.Radar;
using Echo1.Wpf.Rendering;
using HelixToolkit.Wpf;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Echo1.Wpf;

// This file controls everything and I hate that this is how the project ended up, I cannot touch or move anything without the whole thing throwing 50 errors.
// So as cursed as this is, i'm leaving it bc it works.

public partial class MainWindow : Window
{
	// Application state
	private RcsMesh? _mesh;
	private readonly RcsEngine _engine = new();
	private RadarConfig _radar = new();
	private readonly SceneBuilder _builder = new();
	private FreeFlyCamera? _flyCamera;

	// Sweep data (cached for export)
	private double[]? _lastSweepAz;
	private double[]? _lastSweepRcs;
	private double[]? _lastFreqSweepHz;
	private double[]? _lastFreqSweepRcs;

	// Heatmap range
	private float _heatMin = -40f;
	private float _heatMax = 10f;

	// Viewport (constructed in code to avoid XAML parser dependency on HelixToolkit)
	private readonly HelixViewport3D _viewport;
	private readonly ModelVisual3D _meshVisual = new();

	// Timers for sweep animation and continuous rendering
	private readonly DispatcherTimer _sweepTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
	private readonly DispatcherTimer _renderTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };

	public MainWindow()
	{
		InitializeComponent();

		// Set up 3D viewport and scene
		_viewport = new HelixViewport3D
		{
			// Use a dark background to enhance heatmap visibility; set to transparent if you want the window background to show through
			Background = Brushes.Transparent,
			// Automatically adjust the camera to fit the model when it's loaded
			ZoomExtentsWhenLoaded = true,
			// Configure a perspective camera with a reasonable default position and field of view
			Camera = new PerspectiveCamera
			{
				Position = new Point3D(0, 0, 50),
				LookDirection = new Vector3D(0, 0, -1),
				UpDirection = new Vector3D(0, 1, 0),
				FieldOfView = 60
			}
		};

		// Add a default light source to illuminate the model; this is necessary for the heatmap colors to be visible
		_viewport.Children.Add(new SunLight());
		// Mesh content will be added to _meshVisual; this allows us to update the model without reconstructing the entire viewport
		_viewport.Children.Add(_meshVisual);
		// Set the viewport as the child of the container defined in XAML (a Grid named ViewportHost)
		ViewportHost.Child = _viewport;

		// Set up timers for sweep animation and continuous rendering; these will call the respective tick handlers at approximately 30 FPS
		_sweepTimer.Tick += SweepTick;
		// The render timer allows for continuous updates to the scene, which is useful if we want to animate changes or ensure smooth interaction; it can be started when a model is loaded
		_renderTimer.Tick += RenderTick;
		// Initialize the free-fly camera controller, which allows the user to navigate the 3D scene using mouse and keyboard input; this is a custom implementation that wraps around the HelixViewport3D's camera
		_flyCamera = new FreeFlyCamera(_viewport);

		// Draw heatmap legend strip
		DrawHeatLegend();
		// Initialize slider labels to match default radar config values
		UpdateSliderLabels();
	}

	// ──────────────────────────────────────────────────────────────────────────
	// Model loading
	// ──────────────────────────────────────────────────────────────────────────
	// NOTE:
	// WPF OpenFileDialog does not integrate cleanly with async/await,
	// so file selection is handled synchronously while heavy processing
	// is offloaded to a background task to avoid UI blocking.
	//
	// Supported formats: OBJ and STL
	//
	// - STL files are generally well-formed and load reliably.
	// - OBJ files vary significantly between exporters and may contain
	//   inconsistencies (e.g., misaligned or non-manifold facets).
	//
	// Geometry issues in OBJ files can cause incorrect calculations,
	// such as treating interior and exterior surfaces as continuous,
	// resulting in inflated RCS (dBsm) values.
	//
	// If an OBJ fails to load or produces incorrect results:
	// → Re-export it as STL using a 3D modeling tool.
	// → Ensure the mesh is watertight and properly connected.
	private async void LoadModel_Click(object sender, RoutedEventArgs e)
	{
		// Open a file dialog for the user to select a 3D model file; this is done synchronously because WPF's OpenFileDialog does not support async/await patterns
		var dlg = new Microsoft.Win32.OpenFileDialog
		{
			// Filter to show only supported 3D model formats (OBJ and STL); users can select either format, and the loader will determine which one to use based on the file extension
			Filter = "3D Models|*.obj;*.stl",
			Title = "Load target geometry"
		};
		// Show the dialog and check if the user selected a file; if they cancelled, we simply return without doing anything
		if (dlg.ShowDialog() != true) return;

		// Disable the load button and update its text to indicate that loading is in progress
		BtnLoad.IsEnabled = false;
		// Note:
		//	The actual loading and processing of the model is done in a background task to avoid blocking the UI thread,
		//	which allows the application to remain responsive during potentially long operations, especially with large models.
		BtnLoad.Content = "Loading…";

		// Store the selected file path; this will be used in the background task to load the model.
		string path = dlg.FileName;
		// Use a try-catch block to handle any exceptions that may occur during the loading process
		try
		{
			// Load the model in a background task to avoid blocking the UI; the loader will determine which importer to use based on the file extension (STL or OBJ)
			_mesh = await Task.Run(() =>
				path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)
					? StlImporter.Load(path)
					: ObjImporter.Load(path));
			// After loading, we build LODs (Levels of Detail) for the mesh to allow for efficient rendering at different distances;

			// The LOD thresholds are set to 25% and 6.25% of the original facet count, which provides a balance between visual fidelity and performance when rendering complex models.
			_mesh.BuildLods(new[] { _mesh.Facets.Length / 4, _mesh.Facets.Length / 16 });
			// We also build the edge list for the mesh, which is necessary for accurate RCS calculations
			_mesh.BuildEdges();

			// Update the UI with information about the loaded mesh, including the number of facets, edges, and the diagonal size of the bounding box
			MeshInfoLabel.Content = $"{_mesh.Facets.Length:N0} facets · {_mesh.Edges.Length:N0} edges · "
								  + $"{_mesh.Bounds.DiagonalMetres:F1} m diagonal";
			// After loading and processing the mesh, we update the 3D scene to display the new model and then trigger a recomputation of the RCS values based on the current radar configuration
			UpdateScene();
			// Recompute the RCS values to reflect the newly loaded model
			RecomputeRcs();
			// Start the render timer to enable continuous updates to the scene
			_renderTimer.Start();
		}
		catch (Exception ex)
		{
			// If any exceptions occur during loading, we catch them and display an error message to the user using a MessageBox, which provides feedback on what went wrong without crashing the application.
			MessageBox.Show($"Failed to load model:\n{ex.Message}", "Load error",
				MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			// Regardless of success or failure, we re-enable the load button and reset its text to allow the user to attempt loading another model if desired.
			BtnLoad.IsEnabled = true;
			BtnLoad.Content = "Load OBJ / STL…";
			Console.WriteLine(_mesh.Bounds.DiagonalMetres);
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  RCS computation
	// ──────────────────────────────────────────────────────────────────────────
	// NOTE:
	// RCS computation can be time-consuming, especially for complex models and high frequencies, so it is performed in a background task to keep the UI responsive.
	private void RecomputeRcs()
	{
		// Check if a mesh is loaded before attempting to compute RCS
		if (_mesh is null) return;
		// Create snapshots of the current mesh and radar configuration to ensure thread safety when accessing these objects from the background task
		var meshSnap = _mesh;
		// The radar snapshot captures the current frequency, azimuth, elevation, sweep rate, and polarization settings
		var radarSnap = new RadarConfig
		{
			FrequencyHz = _radar.FrequencyHz,
			AzimuthDeg = _radar.AzimuthDeg,
			ElevationDeg = _radar.ElevationDeg,
			SweepRateDegPerSec = _radar.SweepRateDegPerSec,
			TxPol = _radar.TxPol
		};  // snapshot

		// Offload the RCS computation to a background task to avoid blocking the UI thread
		Task.Run(() =>
		{
			// Compute the RCS using the engine
			var result = _engine.Compute(meshSnap, radarSnap);
			// After computation, we update the UI with the new RCS values
			Dispatcher.InvokeAsync(() =>
			{
				// Update the labels to display the total RCS in dBsm, the equivalent area in m², and the contributions from physical optics and edge diffraction
				RcsDbLabel.Content = $"{result.TotalDbsm:F2} dBsm";
				// The RCS in m² is displayed with four decimal places
				RcsM2Label.Content = $"{result.TotalM2:F4} m²  ({FormatRcsNick(result.TotalM2)})";
				// The contributions from physical optics and edge diffraction are converted to dBsm using the PhysicalOpticsKernel's ToDbsm method and displayed with one decimal place
				PoDbLabel.Content = $"{PhysicalOpticsKernel.ToDbsm(result.PoM2):F1} dBsm";
				// The edge contribution is also converted to dBsm and displayed
				EdgeDbLabel.Content = $"{PhysicalOpticsKernel.ToDbsm(result.EdgeM2):F1} dBsm";
			});
		});
	}

	// This method provides a qualitative description of the RCS based on its value in square meters, which can help users understand the scale of the RCS without needing to interpret the raw numbers
	private static string FormatRcsNick(double rcsM2) => rcsM2 switch
	{
		// The thresholds for the RCS categories are defined based on typical values for different types of objects
		> 1000 => $"~{rcsM2 / 1000:F0} km² class",
		> 100 => "ship-class",
		> 10 => "heavy aircraft",
		> 1 => "fighter-class",
		> 0.01 => "stealth/small UAV",
		_ => "insect-scale"
	};

	// This method updates the 3D scene with the current mesh and heatmap range
	private void UpdateScene()
	{
		// Check if a mesh is loaded before attempting to update the scene
		if (_mesh is null) return;
		// Build the heatmap scene using the SceneBuilder
		Dispatcher.InvokeAsync(() =>
		{
			// The heatmap scene is constructed based on the current mesh and the specified heatmap range
			var scene = _builder.BuildHeatmapScene(_mesh, _heatMin, _heatMax);
			// The resulting scene is set as the content of the _meshVisual, which is part of the HelixViewport3D
			_meshVisual.Content = scene;
		});
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Timers
	// ──────────────────────────────────────────────────────────────────────────
	// The sweep timer updates the radar's azimuth based on the configured sweep rate, allowing for continuous rotation of the radar and real-time updates to the RCS values as the angle changes.
	// The render timer ensures that the scene is continuously updated, which is necessary for smooth animation and interaction.
	private void SweepTick(object? s, EventArgs e)
	{
		// Update the radar's azimuth based on the sweep rate and the timer interval (33 ms for approximately 30 FPS)
		_radar.AzimuthDeg = (_radar.AzimuthDeg + _radar.SweepRateDegPerSec * 0.033f) % 360f;
		// Update the azimuth slider to reflect the new azimuth value
		AzSlider.Value = _radar.AzimuthDeg;
		// Recompute the RCS based on the new azimuth
		RecomputeRcs();
	}

	// The render tick handler simply calls UpdateScene to refresh the 3D visualization
	private void RenderTick(object? s, EventArgs e) => UpdateScene();

	// ──────────────────────────────────────────────────────────────────────────
	//  Slider / control handlers
	// ──────────────────────────────────────────────────────────────────────────
	// Each of these handlers updates the corresponding property in the radar configuration and then triggers a recomputation of the RCS to reflect the changes in the UI.

	// The frequency slider updates the radar's frequency in Hz based on the slider value (which is in GHz), updates the label to show the current frequency, and then recomputes the RCS.
	private void FreqSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
	{
		// Convert the slider value from GHz to Hz and update the radar configuration
		_radar.FrequencyHz = e.NewValue * 1e9;
		// Update the frequency label to show the current frequency in GHz with one decimal place
		FreqValueLabel.Content = $"{e.NewValue:F1} GHz";
		// Recompute the RCS to reflect the new frequency
		RecomputeRcs();
	}

	// The azimuth slider updates the radar's azimuth angle in degrees, updates the label to show the current azimuth, and then recomputes the RCS based on the new angle.
	private void AzSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
	{
		// Update the radar's azimuth angle based on the slider value
		_radar.AzimuthDeg = (float)e.NewValue;
		// Update the azimuth label to show the current azimuth in degrees with one decimal place
		AzValueLabel.Content = $"{e.NewValue:F1}°";
		// Recompute the RCS to reflect the new azimuth
		RecomputeRcs();
	}

	// The elevation slider updates the radar's elevation angle in degrees, updates the label to show the current elevation, and then recomputes the RCS based on the new angle.
	private void ElSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
	{
		// Update the radar's elevation angle based on the slider value
		_radar.ElevationDeg = (float)e.NewValue;
		// Update the elevation label to show the current elevation in degrees with one decimal place
		ElValueLabel.Content = $"{e.NewValue:F1}°";
		// Recompute the RCS to reflect the new elevation
		RecomputeRcs();
	}

	// The sweep rate slider updates the radar's sweep rate in degrees per second, updates the label to show the current sweep rate, and then recomputes the RCS to reflect the new sweep rate.
	private void SweepRate_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
	{
		// Update the radar's sweep rate based on the slider value
		_radar.SweepRateDegPerSec = (float)e.NewValue;
		// Update the sweep rate label to show the current sweep rate in degrees per second as an integer
		SweepRateLabel.Content = $"{(int)e.NewValue}";
	}

	// The sweep checkbox starts or stops the sweep timer based on whether it is checked, allowing the radar to continuously rotate and update the RCS values in real-time.
	private void Sweep_Checked(object s, RoutedEventArgs e)
	{
		// Start the sweep timer if the checkbox is checked, otherwise stop it
		if (SweepToggle.IsChecked == true) _sweepTimer.Start();
		else _sweepTimer.Stop();
	}

	// The band preset buttons allow the user to quickly set the frequency slider to common radar bands (e.g., L-band, S-band, X-band) by clicking the corresponding button,
	// which updates the frequency and recomputes the RCS accordingly.
	private void BandPreset_Click(object sender, RoutedEventArgs e)
	{
		// Check if the sender is a Button and if it has a Tag that can be parsed as a double representing the frequency in GHz
		// If so, update the frequency slider to the specified value, which will trigger the frequency change handler to update the radar configuration and recompute the RCS.
		if (sender is Button btn && btn.Tag is string tagStr
			&& double.TryParse(tagStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double ghz))
		{
			FreqSlider.Value = ghz;
		}
	}

	// The polarization radio buttons update the radar's transmit polarization based on which option is selected (VV, HH, or HV), and then recompute the RCS to reflect the new polarization setting.
	private void Pol_Changed(object sender, RoutedEventArgs e)
	{
		// Update the radar's transmit polarization based on which radio button is checked
		_radar.TxPol = PolVV.IsChecked == true ? Polarisation.VV :
					   PolHH.IsChecked == true ? Polarisation.HH :
												 Polarisation.HV;
		// Invalidate the engine's cache to ensure that the new polarization setting is applied in subsequent RCS computations, and then recompute the RCS to reflect the change.
		_engine.InvalidateCache();
		// Recompute the RCS to reflect the new polarization setting
		RecomputeRcs();
	}

	// The heatmap range text boxes allow the user to specify the minimum and maximum dBsm values for the heatmap visualization
	private void HeatRange_Changed(object sender, RoutedEventArgs e)
	{
		// Try to parse the minimum and maximum heatmap values from the text boxes
		if (float.TryParse(HeatMinBox.Text, out float mn) &&
			float.TryParse(HeatMaxBox.Text, out float mx) && mn < mx)
		{
			// If the values are valid, update the heatmap range and refresh the scene to apply the new range to the heatmap visualization
			_heatMin = mn;
			_heatMax = mx;
			// Refresh the scene to apply the new heatmap range
			UpdateScene();
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Full azimuth sweep + polar plot
	// ──────────────────────────────────────────────────────────────────────────
	// This handler computes the RCS values for a full 360-degree azimuth sweep at 1-degree increments, which can be time-consuming for complex models, so it is performed in a background task to keep the UI responsive.
	private async void ComputeFullSweep_Click(object sender, RoutedEventArgs e)
	{
		// Check if a mesh is loaded before attempting to compute the full sweep
		if (_mesh is null) return;
		// Disable the button and update its text to indicate that the computation is in progress
		var btn = (Button)sender;
		// Disabling the button prevents the user from initiating multiple sweeps simultaneously, which could lead to performance issues or unintended behavior
		btn.IsEnabled = false;
		// Updating the button text provides feedback that the computation is underway
		btn.Content = "Computing…";

		// Create snapshots of the current mesh and radar configuration to ensure thread safety when accessing these objects from the background task
		var mesh = _mesh;
		// The radar snapshot captures the current frequency, elevation, polarization, and other settings that will be used for the sweep computation
		var radar = _radar;

		// Offload the full sweep computation to a background task to avoid blocking the UI thread
		try
		{
			// The SweepAzimuth method computes the RCS values for a full 360-degree sweep of the radar's azimuth angle, starting from 0 degrees to 359 degrees in 1-degree increments
			var (azDeg, rcsDb) = await Task.Run(() =>
				_engine.SweepAzimuth(mesh, radar, 0, 359, 1.0));

			// After the computation is complete:
			//		store the results in the _lastSweepAz and _lastSweepRcs fields for potential export,
			//		and then call DrawPolarPlot to visualize the results on a polar plot.
			//		Finally, enable the export button to allow the user to save the sweep data as a CSV file.
			_lastSweepAz = azDeg;
			_lastSweepRcs = rcsDb;
			DrawPolarPlot(azDeg, rcsDb);
			BtnExport.IsEnabled = true;
		}
		// If any exceptions occur during the sweep computation, we catch them and display an error message to the user using a MessageBox, which provides feedback on what went wrong without crashing the application.
		catch (Exception ex)
		{
			MessageBox.Show($"Failed to compute sweep:\n{ex.Message}", "Computation error",
				MessageBoxButton.OK, MessageBoxImage.Error);
		}
		// Regardless of success or failure, we re-enable the button and reset its text to allow the user to initiate another sweep if desired.
		finally
		{
			// Re-enable the button to allow the user to run another sweep if they wish, and reset the button text to its original state.
			btn.IsEnabled = true;
			btn.Content = "Compute full sweep";
		}
	}

	private void DrawPolarPlot(double[] azDeg, double[] rcsDb)
	{
		PolarCanvas.Children.Clear();
		if (azDeg.Length == 0) return;

		double w = PolarCanvas.ActualWidth > 0 ? PolarCanvas.ActualWidth : 300;
		double h = PolarCanvas.ActualHeight > 0 ? PolarCanvas.ActualHeight : 200;
		double cx = w / 2, cy = h / 2;
		double r = Math.Min(cx, cy) - 12;

		// Grid circles
		for (int ring = 1; ring <= 4; ring++)
		{
			double rr = r * ring / 4.0;
			var ellipse = new Ellipse
			{
				Width = rr * 2,
				Height = rr * 2,
				Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x27, 0x44)),
				StrokeThickness = 0.5
			};
			Canvas.SetLeft(ellipse, cx - rr);
			Canvas.SetTop(ellipse, cy - rr);
			PolarCanvas.Children.Add(ellipse);
		}

		// Normalise RCS values to [0, r]
		double minDb = rcsDb.Min();
		double maxDb = rcsDb.Max();
		double span = maxDb - minDb;
		if (span < 1.0) span = 1.0;

		// Draw polar trace
		var points = new System.Windows.Media.PointCollection();
		for (int i = 0; i < azDeg.Length; i++)
		{
			double az = azDeg[i] * Math.PI / 180.0;
			double norm = (rcsDb[i] - minDb) / span;
			double rr = norm * r;
			points.Add(new System.Windows.Point(cx + rr * Math.Sin(az), cy - rr * Math.Cos(az)));
		}

		var poly = new Polyline
		{
			Points = points,
			Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
			StrokeThickness = 1.2
		};
		PolarCanvas.Children.Add(poly);

		// Range labels
		AddCanvasText(PolarCanvas, cx + 4, cy - r - 2,
			$"{maxDb:F0} dBsm", 9, "#4FC3F7");
		AddCanvasText(PolarCanvas, cx + 4, cy - 2,
			$"{minDb:F0} dBsm", 9, "#4FC3F7");
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Frequency sweep
	// ──────────────────────────────────────────────────────────────────────────

	private async void RunFreqSweep_Click(object sender, RoutedEventArgs e)
	{
		if (_mesh is null) return;
		if (!double.TryParse(FreqSweepStart.Text, out double fStartGhz)) return;
		if (!double.TryParse(FreqSweepStop.Text, out double fStopGhz)) return;
		if (fStartGhz >= fStopGhz) return;

		var btn = (Button)sender;
		btn.IsEnabled = false;
		btn.Content = "Running…";

		var mesh = _mesh;
		var radar = _radar;

		try
		{
			var result = await Task.Run(() =>
			{
				int n = 100;
				var freqs = new double[n];
				var rcs = new double[n];
				Parallel.For(0, n, i =>
				{
					double f = (fStartGhz + (fStopGhz - fStartGhz) * i / (n - 1)) * 1e9;
					var r = new RadarConfig
					{
						FrequencyHz = f,
						AzimuthDeg = radar.AzimuthDeg,
						ElevationDeg = radar.ElevationDeg,
						TxPol = radar.TxPol
					};
					freqs[i] = f;
					rcs[i] = _engine.Compute(mesh, r).TotalDbsm;
				});
				return (freqs, rcs);
			});

			_lastFreqSweepHz = result.freqs;
			_lastFreqSweepRcs = result.rcs;
			DrawFreqPlot(result.freqs, result.rcs, fStartGhz, fStopGhz);
		}
		finally
		{
			btn.IsEnabled = true;
			btn.Content = "Run frequency sweep";
		}
	}

	private void DrawFreqPlot(double[] freqHz, double[] rcsDb,
		double fStartGhz, double fStopGhz)
	{
		FreqCanvas.Children.Clear();
		if (freqHz.Length == 0) return;

		double w = FreqCanvas.ActualWidth > 0 ? FreqCanvas.ActualWidth : 300;
		double h = FreqCanvas.ActualHeight > 0 ? FreqCanvas.ActualHeight : 120;
		double pl = 8, pr = 8, pt = 8, pb = 18;

		double minDb = rcsDb.Min();
		double maxDb = rcsDb.Max();
		double span = maxDb - minDb;
		if (span < 1) span = 1;

		var points = new System.Windows.Media.PointCollection();
		for (int i = 0; i < freqHz.Length; i++)
		{
			double x = pl + (freqHz[i] / 1e9 - fStartGhz) / (fStopGhz - fStartGhz) * (w - pl - pr);
			double y = (h - pb) - (rcsDb[i] - minDb) / span * (h - pt - pb);
			points.Add(new System.Windows.Point(x, y));
		}

		FreqCanvas.Children.Add(new Polyline
		{
			Points = points,
			Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
			StrokeThickness = 1.0
		});

		AddCanvasText(FreqCanvas, pl, h - 14, $"{fStartGhz:F0} GHz", 9, "#8BA5C9");
		AddCanvasText(FreqCanvas, w - pr - 30, h - 14, $"{fStopGhz:F0} GHz", 9, "#8BA5C9");
		AddCanvasText(FreqCanvas, pl, pt, $"{maxDb:F0}", 9, "#4FC3F7");
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Material assignment
	// ──────────────────────────────────────────────────────────────────────────


	// Actual material application happens in ApplyMaterial_Click; this handler is just a placeholder
	private void MaterialCombo_Changed(object sender, SelectionChangedEventArgs e) { }

	// Actual materials are classified, so we map the combo box selection to known material properties here
	// Will not implement custom material editing in this project due to:
	//		- legality concerns around user-provided material data
	//		- Complexity of accurate EM material characterisation
	//		- Actual material properties, compositions, ingredients, and properties are classified information in many jurisdictions
	private void ApplyMaterial_Click(object sender, RoutedEventArgs e)
	{
		if (_mesh is null) return;

		var mat = (MaterialCombo.SelectedItem as ComboBoxItem)?.Tag as string switch
		{
			"CarbonFoam" => KnownMaterials.CarbonFoam10mm,
			"Ferrite" => KnownMaterials.FerriteTile3mm,
			"Dielectric" => KnownMaterials.DielectricCoating5mm,
			"Aluminium" => KnownMaterials.Aluminium,
			"Titanium Alloy" => KnownMaterials.Titanium,
			_ => MaterialProperties.PEC
		};

		_mesh.MeshDefaultMaterial = mat;
		_engine.InvalidateCache();
		MaterialStatusLabel.Content = $"Applied: {mat.Name}";
		RecomputeRcs();
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  CSV export
	// ──────────────────────────────────────────────────────────────────────────

	private void ExportCsv_Click(object sender, RoutedEventArgs e)
	{
		if (_lastSweepAz is null || _lastSweepRcs is null) return;

		var dlg = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "CSV files|*.csv",
			FileName = $"RCS_sweep_{_radar.FrequencyHz / 1e9:F1}GHz_{DateTime.Now:yyyyMMdd_HHmm}.csv"
		};
		if (dlg.ShowDialog() != true) return;

		var sb = new StringBuilder();
		sb.AppendLine("# SpecterCS RCS azimuth sweep");
		sb.AppendLine($"# Frequency: {_radar.FrequencyHz / 1e9:F2} GHz");
		sb.AppendLine($"# Elevation: {_radar.ElevationDeg:F1} deg");
		sb.AppendLine($"# Polarisation: {_radar.TxPol}");
		sb.AppendLine($"# Model: {_mesh?.Name ?? "unknown"}");
		sb.AppendLine($"# Facets: {_mesh?.Facets.Length ?? 0}");
		sb.AppendLine("azimuth_deg,rcs_dbsm");

		for (int i = 0; i < _lastSweepAz.Length; i++)
			sb.AppendLine($"{_lastSweepAz[i]:F1},{_lastSweepRcs[i]:F4}");

		File.WriteAllText(dlg.FileName, sb.ToString());
		MaterialStatusLabel.Content = $"Exported {_lastSweepAz.Length} points.";
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────────────────────────────────

	private void UpdateSliderLabels()
	{
		FreqValueLabel.Content = $"{FreqSlider.Value:F1} GHz";
		AzValueLabel.Content = $"{AzSlider.Value:F1}°";
		ElValueLabel.Content = $"{ElSlider.Value:F1}°";
		SweepRateLabel.Content = $"{(int)SweepRateSlider.Value}";
	}

	private void DrawHeatLegend()
	{
		// Render heatmap legend as a LinearGradientBrush
		var stops = new GradientStopCollection();
		for (int i = 0; i <= 8; i++)
		{
			float t = i / 8f;
			var c = HeatmapColorMap.Sample(t);
			stops.Add(new GradientStop(c, t));
		}
		HeatLegend.Background = new LinearGradientBrush(stops,
			new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));
	}

	private static void AddCanvasText(Canvas canvas, double x, double y,
		string text, int fontSize, string hexColor)
	{
		var tb = new TextBlock
		{
			Text = text,
			FontSize = fontSize,
			Foreground = new SolidColorBrush(
				(Color)ColorConverter.ConvertFromString(hexColor))
		};
		Canvas.SetLeft(tb, x);
		Canvas.SetTop(tb, y);
		canvas.Children.Add(tb);
	}

	private void Window_KeyDown(object sender, KeyEventArgs e) { }
}
