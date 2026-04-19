// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 4.0.19
// 

using Colyseus.Schema;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;
#endif

namespace VectoArena.Schema {
	public partial class GameState : Colyseus.Schema.Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public GameState() { }
		[Type(0, "string")]
		public string matchState = default(string);

		[Type(1, "map", typeof(MapSchema<PlayerState>))]
		public MapSchema<PlayerState> players = null;

		[Type(2, "ref", typeof(ZoneState))]
		public ZoneState zone = null;
	}
}
