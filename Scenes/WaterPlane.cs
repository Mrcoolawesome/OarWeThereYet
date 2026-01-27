using Godot;
using System;

public partial class WaterPlane : MeshInstance3D
{

	ShaderMaterial _waterMaterial;
	private Image _noise;

	private float _noiseScale;
	private float _waveSpeed;
	private float _heightScale;
	private float _time;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// get all the parameters from the shader
		_waterMaterial = (ShaderMaterial)GetActiveMaterial(0);
		_noiseScale = (float)_waterMaterial.GetShaderParameter("noise_scale");
		_waveSpeed = (float)_waterMaterial.GetShaderParameter("wave_speed");
		_heightScale = (float)_waterMaterial.GetShaderParameter("height_scale");

		// Normally we'd get the noise texture here but like it doesn't work so we get it once when GetHeight is called
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// track the time for the shader
		_time += (float)delta;
		_waterMaterial.SetShaderParameter("wave_time", _time);
	}

	// get the height for a specific world position
	public float GetHeight(Vector3 worldPosition)
	{
		// uv coordinates are always between 0 and 1, so the Wrap function keeps them within those ranges
		float uvX = Mathf.Wrap(worldPosition.X / _noiseScale + _time * _waveSpeed, 0.0f, 1.0f);
		float uvY = Mathf.Wrap(worldPosition.Z / _noiseScale + _time * _waveSpeed, 0.0f, 1.0f);

		if (_noise == null)
    {
			// Try to grab it again (Lazy Loading)
			Texture2D waveTex = (Texture2D)_waterMaterial.GetShaderParameter("wave");
			_noise = waveTex.GetImage();
    }

    Vector2I pixelPosition = new((int)(uvX * _noise.GetWidth()), (int)(uvY * _noise.GetHeight()));
		return GlobalPosition.Y + _noise.GetPixelv(pixelPosition).R * _heightScale;
	}
}
