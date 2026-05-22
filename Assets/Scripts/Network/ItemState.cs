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
	public partial class ItemState : Colyseus.Schema.Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public ItemState() { }
		[Type(0, "string")]
		public string id = default(string);

		[Type(1, "string")]
		public string type = default(string);

		[Type(2, "number")]
		public float x = default(float);

		[Type(3, "number")]
		public float y = default(float);

		[Type(4, "number")]
		public float z = default(float);

		[Type(5, "string")]
		public string pickupBy = default(string);

		[Type(6, "number")]
		public float pickupProgress = default(float);
	}
}
