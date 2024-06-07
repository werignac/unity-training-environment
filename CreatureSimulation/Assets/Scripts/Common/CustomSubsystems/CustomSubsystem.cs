using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using werignac.Subsystem;

namespace werignac.GeneticAlgorithm.Subsystems
{
	//[Subsystem(SubsystemLifetime.SCENE)]
    public class CustomSubsystem : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
			SubsystemManagerComponent manager = SubsystemManagerComponent.Get();

			PhysicsUpdateSubsystem physics = manager.GetSubsystem<PhysicsUpdateSubsystem>();
			SynchronizationContextSubsystem sync = manager.GetSubsystem<SynchronizationContextSubsystem>();

			physics.MinPhysicsUpdatePeriod = Time.fixedDeltaTime;
			physics.PhysicsTimeStep = Time.fixedDeltaTime / 10;
			sync.SetSynchronizationContextType(SynchronizationContextSubsystem.SynchronizationContextType.C_SHARP);
        }

    }
}
