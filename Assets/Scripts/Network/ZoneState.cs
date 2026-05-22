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
	public partial class ZoneState : Colyseus.Schema.Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public ZoneState() { }
		[Type(0, "string")]
		public string currentState = default(string);

		[Type(1, "number")]
		public float currentCenterX = default(float);

		[Type(2, "number")]
		public float currentCenterZ = default(float);

		[Type(3, "number")]
		public float currentRadius = default(float);

		[Type(4, "number")]
		public float nextCenterX = default(float);

		[Type(5, "number")]
		public float nextCenterZ = default(float);

		[Type(6, "number")]
		public float nextRadius = default(float);

		[Type(7, "number")]
		public float timer = default(float);

		[Type(8, "number")]
		public float waitTime = default(float);

		[Type(9, "number")]
		public float shrinkDuration = default(float);

		[Type(10, "number")]
		public float currentPhase = default(float);

		[Type(11, "number")]
		public float currentDamagePerSecond = default(float);

		[Type(12, "number")]
		public float damageMultiplierPerPhase = default(float);

		[Type(13, "number")]
		public float shrinkFactor = default(float);
	}
}
