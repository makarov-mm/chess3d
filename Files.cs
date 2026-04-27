namespace Chess3D;

public static class Files
{
    public static string ShaderVertex => Path.Combine(AppContext.BaseDirectory, "Shaders", "chess.vert");
    public static string ShaderFragment => Path.Combine(AppContext.BaseDirectory, "Shaders", "chess.frag");
    public static string Model(string name) => Path.Combine(AppContext.BaseDirectory, "Models", name + ".obj");
}
