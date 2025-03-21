using Godot;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Beings
{
	public partial class HumanTownsfolk : Being
	{
		public override BeingAttributes DefaultAttributes { get; } = BaseAttributesSet;

		public override void _Ready()
		{
			selfAsEntity().AddTrait<VillagerTrait>();

			base._Ready();

		}
		public override void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes attributes = null)
		{
			_baseMoveTicks = 5;
			base.Initialize(gridArea, startGridPos, attributes);
		}
	}
}
