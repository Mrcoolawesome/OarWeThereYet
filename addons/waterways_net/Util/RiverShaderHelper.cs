using Godot;
using Waterways.Data;

namespace Waterways.Util;

public static class RiverShaderHelper
{
    public static void SetStandardMaterialShader(ShaderMaterial material, ShaderType shaderType)
    {
        switch (shaderType)
        {
            case ShaderType.Water:
                material.Shader = ResourceLoader.Load<Shader>($"{WaterwaysPlugin.PluginPath}/Shaders/river.gdshader");
                material.Shader.ResourceLocalToScene = true;
                material.SetShaderParameter("normal_bump_texture", ResourceLoader.Load<Texture>($"{WaterwaysPlugin.PluginPath}/Textures/water_normal_bump.png"));
                
                // setting the render priority to be higher than 0 so that it happens after the boat hull mask is rendered
                material.RenderPriority = 3; 
                
                break;

            case ShaderType.None:
                material.Shader = null;
                break;
        }
    }
}
