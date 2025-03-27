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
			if (Health == null) return;

			var villagerTrait = new VillagerTrait();
			villagerTrait.Initialize(this, Health);
			selfAsEntity().AddTrait(villagerTrait, 1);

			base._Ready();

		}
		public override void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes? attributes = null)
		{
			_baseMoveTicks = 5;
			base.Initialize(gridArea, startGridPos, attributes);
		}
	}
}
