

namespace werignac.GameplayTags
{
    public sealed class GameplayTagManager_Example
    {
		public sealed class Levels : GameplayTag
		{
			public sealed class Factory : GameplayTag
			{
				public static string StaticName
				{
					get
					{
						return "Factory";
					}
				}

				public static GameplayTag StaticParent
				{
					get
					{
						return new Levels();
					}
				}

				public static GameplayTag[] StaticChildren
				{
					get
					{
						return new GameplayTag[] { };
					}
				}


				protected override string GetName()
				{
					return StaticName;
				}

				protected override GameplayTag GetParent()
				{
					return StaticParent;
				}

				protected override GameplayTag[] GetChildren()
				{
					return StaticChildren;
				}
			}

			public static string StaticName
			{
				get
				{
					return "Levels";
				}
			}

			public static GameplayTag StaticParent
			{
				get
				{
					return null;
				}
			}

			public static GameplayTag[] StaticChildren
			{
				get
				{
					return new GameplayTag[] { new Factory() };
				}
			}

			protected override string GetName()
			{
				return StaticName;
			}

			protected override GameplayTag[] GetChildren()
			{
				return StaticChildren;
			}

			protected override GameplayTag GetParent()
			{
				return StaticParent;
			}
		}
    }
}
