using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ScorchWurmTweaks
{
    public class BreachComponentNux : NetworkBehaviour
    {
        [SyncVar(hook = "OnShouldReposition")]
        public Vector3 breachPosition = Vector3.zero;
        private EntityStateMachine entityStateMachine;

        void OnShouldReposition(Vector3 newPosition)
        {
            this.breachPosition = newPosition;
            if (entityStateMachine)
            {
                entityStateMachine.SetNextState(new BetterScorchlingBurrow());
            }
        }

        private void Start()
        {
            EntityStateMachine[] esms = this.GetComponents<EntityStateMachine>();
            List<EntityStateMachine> esmList = esms.ToList();
            foreach (EntityStateMachine esm in esmList)
            {
                if (esm.customName == "Body")
                    this.entityStateMachine = esm;
            }
        }
    }
}