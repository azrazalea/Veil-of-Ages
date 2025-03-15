using Godot;

public partial class TestScene : Node2D
{
    public override void _Ready()
    {
        GD.Print("Test scene is running");
    }

    public override void _Process(double delta)
    {
        // Keep window open
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            GetTree().Quit();
        }
    }
}
