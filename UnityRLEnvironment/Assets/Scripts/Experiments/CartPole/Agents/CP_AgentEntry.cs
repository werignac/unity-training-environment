using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace werignac.CartPole.Agent
{
	[Serializable]
	public class CP_AgentEntry
	{
		[SerializeField]
		private string m_agentName;

		[SerializeField]
		private CP_AgentAsset m_agentAsset;

		[SerializeField]
		private Texture2D m_agentIcon;

		[SerializeField]
		private string m_agentDescription;

		public string Name
		{
			get { return m_agentName; }
			private set { m_agentName = value; }
		}

		public CP_AgentAsset Asset
		{
			get { return m_agentAsset; }
			private set { m_agentAsset = value; }
		}

		public Texture2D Icon
		{
			get { return m_agentIcon; }
			private set { m_agentIcon = value; }
		}

		public string Description
		{
			get { return m_agentDescription; }
			private set { m_agentDescription = value; }
		}
	}
}
