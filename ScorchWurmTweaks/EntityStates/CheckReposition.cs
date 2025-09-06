using RoR2;
using UnityEngine;
using EntityStates;
using RoR2.CharacterAI;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace ScorchWurmTweaks;

public class CheckReposition : BaseState
{

    public override void OnEnter()
    {
        base.OnEnter();
        if (!NetworkServer.active)
            return;

        ShouldReposition();
    }


    public override void FixedUpdate()
    {
        base.FixedUpdate();
        this.outer.SetNextStateToMain();
    }

    private void ShouldReposition()
    {
        bool shouldReposition = true;
        CharacterBody enemyCBody = this.characterBody.master.GetComponent<BaseAI>().currentEnemy?.characterBody;
        Vector3 breachPosition;

        if ((bool)enemyCBody)
        {
            BreachComponentNux breachComponent = this.gameObject.GetComponent<BreachComponentNux>();
            Vector3 footPosition = this.characterBody.footPosition;
            NodeGraph nodeGraph = SceneInfo.instance.GetNodeGraph(MapNodeGroup.GraphType.Ground);
            Vector3 position1 = RaycastToFloor(enemyCBody.coreTransform.position);
            if (position1 == enemyCBody.coreTransform.position)
            {
                shouldReposition = false;
            }

            List<NodeGraph.NodeIndex> nodesInRange = nodeGraph.FindNodesInRange(position1, 10f, 20f, HullMask.Golem);
            Vector3 position2 = new Vector3();
            bool flag = false;
            int num1 = 35;
            if (nodesInRange.Count > 0)
            {
            while (!flag)
            {
                NodeGraph.NodeIndex nodeIndex = nodesInRange.ElementAt<NodeGraph.NodeIndex>(UnityEngine.Random.Range(0, nodesInRange.Count));
                nodeGraph.GetNodePosition(nodeIndex, out position2);
                double num2 = (double)Vector3.Distance(footPosition, position2);
                --num1;
                if (num2 > 35.0 || num1 < 0)
                    flag = true;
            }
            breachPosition = position2;
            if (breachComponent.breachPosition == breachPosition)
            {
                shouldReposition = false;
            }

            if (shouldReposition)
                breachComponent.breachPosition = breachPosition;
            }
        }
    }

    private Vector3 RaycastToFloor(Vector3 position)
    {
        RaycastHit hitInfo;
        return Physics.Raycast(new Ray(position, Vector3.down), out hitInfo, 200f, (int)LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ? hitInfo.point : position;
    }

    public override InterruptPriority GetMinimumInterruptPriority()
    {
        return InterruptPriority.Death;
    }
}
