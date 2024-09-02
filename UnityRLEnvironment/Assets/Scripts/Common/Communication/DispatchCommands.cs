/// Author: William Erignac
/// Version 09-02-2024
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace werignac.Communication.Dispatch.Commands
{
	public enum DispatchCommandType { NONE, RUN, SET, QUIT }

	public class DispatchCommand : Object
	{
		public DispatchCommandType Type { get; protected set; } = DispatchCommandType.NONE;
	}

	public class DispatchRunCommand : DispatchCommand
	{
		public string ExperimentToRun { get; private set; }

		public DispatchRunCommand(string experimentToRun)
		{
			Type = DispatchCommandType.RUN;
			ExperimentToRun = experimentToRun;
		}
	}

	public class DispatchSetCommand : DispatchCommand
	{
		public string SettingName { get; private set; }
		public string SettingValue { get; private set; }

		public DispatchSetCommand(string settingName, string settingValue)
		{
			Type = DispatchCommandType.SET;
			SettingName = settingName;
			SettingValue = settingValue;
		}

	}

	public class DispatchQuitCommand : DispatchCommand
	{
		public DispatchQuitCommand() { Type = DispatchCommandType.QUIT; }
	}
}
