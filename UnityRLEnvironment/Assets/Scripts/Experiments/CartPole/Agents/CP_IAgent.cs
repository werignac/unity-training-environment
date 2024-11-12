using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.CartPole.Agent
{
    public interface CP_IAgent : IDisposable
    {
		CartPoleCommand Evaluate(CartPoleState state, out Dictionary<string, object> additionalOutput);
    }
}
