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
	public partial class PlayerState : Colyseus.Schema.Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public PlayerState() { }
		[Type(0, "string")]
		public string id = default(string);

		[Type(1, "string")]
		public string username = default(string);

		[Type(2, "number")]
		public float x = default(float);

		[Type(3, "number")]
		public float y = default(float);

		[Type(4, "number")]
		public float z = default(float);

		[Type(5, "number")]
		public float rotation = default(float);

		[Type(6, "number")]
		public float hp = default(float);

		[Type(7, "string")]
		public string currentWeapon = default(string);

		[Type(8, "number")]
		public float ammo = default(float);

		[Type(9, "string")]
		public string meleeWeapon = default(string);

		[Type(10, "string")]
		public string rangedWeapon = default(string);

		[Type(11, "number")]
		public float kills = default(float);

		[Type(12, "boolean")]
		public bool isDead = default(bool);

		[Type(13, "string")]
		public string skinId = default(string);
	}
}
