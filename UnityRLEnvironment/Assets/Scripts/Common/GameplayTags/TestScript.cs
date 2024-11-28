using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.GameplayTags
{
    public class TestScript : MonoBehaviour
    {
		[SerializeReference, GameplayTag()]
		private GameplayTag gameplayTag;
    }
}
