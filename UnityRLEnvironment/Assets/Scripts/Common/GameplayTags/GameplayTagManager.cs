
namespace werignac.GameplayTags {
	public sealed class GameplayTagManager {
		public sealed class Weapon : GameplayTag {
			public sealed class MegaGun : GameplayTag {
				public static string StaticName {
					get { return "MegaGun"; }
				}
				public static GameplayTag StaticParent {
					get { return new Weapon(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public sealed class GattlingGun : GameplayTag {
				public static string StaticName {
					get { return "GattlingGun"; }
				}
				public static GameplayTag StaticParent {
					get { return new Weapon(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public sealed class Blunderbuss : GameplayTag {
				public static string StaticName {
					get { return "Blunderbuss"; }
				}
				public static GameplayTag StaticParent {
					get { return new Weapon(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public static string StaticName {
				get { return "Weapon"; }
			}
			public static GameplayTag StaticParent {
				get { return null; }
			}
			public static GameplayTag[] StaticChildren {
				get { return new GameplayTag[] { new Blunderbuss(), new GattlingGun(), new MegaGun() }; }
			}
			protected override string GetName() {
				 return StaticName;
			}
			protected override GameplayTag GetParent() {
				 return StaticParent;
			}
			protected override GameplayTag[] GetChildren() {
				 return StaticChildren;
			}
		}
		public sealed class Levels : GameplayTag {
			public sealed class Solid_Super : GameplayTag {
				public static string StaticName {
					get { return "Solid_Super"; }
				}
				public static GameplayTag StaticParent {
					get { return new Levels(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public sealed class Solid : GameplayTag {
				public static string StaticName {
					get { return "Solid"; }
				}
				public static GameplayTag StaticParent {
					get { return new Levels(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public sealed class Bouncy : GameplayTag {
				public sealed class Hallway : GameplayTag {
					public static string StaticName {
						get { return "Hallway"; }
					}
					public static GameplayTag StaticParent {
						get { return new Bouncy(); }
					}
					public static GameplayTag[] StaticChildren {
						get { return new GameplayTag[] {  }; }
					}
					protected override string GetName() {
						 return StaticName;
					}
					protected override GameplayTag GetParent() {
						 return StaticParent;
					}
					protected override GameplayTag[] GetChildren() {
						 return StaticChildren;
					}
				}

				public sealed class Bathroom : GameplayTag {
					public static string StaticName {
						get { return "Bathroom"; }
					}
					public static GameplayTag StaticParent {
						get { return new Bouncy(); }
					}
					public static GameplayTag[] StaticChildren {
						get { return new GameplayTag[] {  }; }
					}
					protected override string GetName() {
						 return StaticName;
					}
					protected override GameplayTag GetParent() {
						 return StaticParent;
					}
					protected override GameplayTag[] GetChildren() {
						 return StaticChildren;
					}
				}

				public sealed class Living_Room : GameplayTag {
					public static string StaticName {
						get { return "Living_Room"; }
					}
					public static GameplayTag StaticParent {
						get { return new Bouncy(); }
					}
					public static GameplayTag[] StaticChildren {
						get { return new GameplayTag[] {  }; }
					}
					protected override string GetName() {
						 return StaticName;
					}
					protected override GameplayTag GetParent() {
						 return StaticParent;
					}
					protected override GameplayTag[] GetChildren() {
						 return StaticChildren;
					}
				}

				public static string StaticName {
					get { return "Bouncy"; }
				}
				public static GameplayTag StaticParent {
					get { return new Levels(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] { new Living_Room(), new Bathroom(), new Hallway() }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public static string StaticName {
				get { return "Levels"; }
			}
			public static GameplayTag StaticParent {
				get { return null; }
			}
			public static GameplayTag[] StaticChildren {
				get { return new GameplayTag[] { new Bouncy(), new Solid(), new Solid_Super() }; }
			}
			protected override string GetName() {
				 return StaticName;
			}
			protected override GameplayTag GetParent() {
				 return StaticParent;
			}
			protected override GameplayTag[] GetChildren() {
				 return StaticChildren;
			}
		}
		public sealed class GameModes : GameplayTag {
			public sealed class Campaign : GameplayTag {
				public static string StaticName {
					get { return "Campaign"; }
				}
				public static GameplayTag StaticParent {
					get { return new GameModes(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public sealed class PvE : GameplayTag {
				public static string StaticName {
					get { return "PvE"; }
				}
				public static GameplayTag StaticParent {
					get { return new GameModes(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public sealed class PvP : GameplayTag {
				public static string StaticName {
					get { return "PvP"; }
				}
				public static GameplayTag StaticParent {
					get { return new GameModes(); }
				}
				public static GameplayTag[] StaticChildren {
					get { return new GameplayTag[] {  }; }
				}
				protected override string GetName() {
					 return StaticName;
				}
				protected override GameplayTag GetParent() {
					 return StaticParent;
				}
				protected override GameplayTag[] GetChildren() {
					 return StaticChildren;
				}
			}

			public static string StaticName {
				get { return "GameModes"; }
			}
			public static GameplayTag StaticParent {
				get { return null; }
			}
			public static GameplayTag[] StaticChildren {
				get { return new GameplayTag[] { new PvP(), new PvE(), new Campaign() }; }
			}
			protected override string GetName() {
				 return StaticName;
			}
			protected override GameplayTag GetParent() {
				 return StaticParent;
			}
			protected override GameplayTag[] GetChildren() {
				 return StaticChildren;
			}
		}

		public static GameplayTag[] RootTags {
			get { return new GameplayTag[] {new Weapon(), new Levels(), new GameModes()}; }
		}
	}
}