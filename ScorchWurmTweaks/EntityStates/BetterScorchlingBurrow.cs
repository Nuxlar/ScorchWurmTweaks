using RoR2;
using UnityEngine;
using EntityStates;
using EntityStates.Scorchling;
using RoR2.CharacterAI;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;

namespace ScorchWurmTweaks;

public class BetterScorchlingBurrow : BaseState
{

    public string burrowSoundString = "Play_scorchling_burrow";
    public string burrowLoopSoundString = "Play_scorchling_burrow_loop";
    public string burrowStopLoopSoundString = "Stop_scorchling_burrow_loop";
    public GameObject burrowEffectPrefab = Main.burrowEffectPrefab;
    public float burrowRadius = 1f;
    public float animDurationBurrow = 1f;
    public float burrowAnimationDuration = 1f;
    public float teleportDelay = 2f;
    private ScorchlingController sController;
    private bool waitingForBurrow;

    public override void OnEnter()
    {
        base.OnEnter();
        this.sController = this.characterBody.GetComponent<ScorchlingController>();

        if (!ShouldReposition())
            return;
        Util.PlaySound(this.burrowSoundString, this.gameObject);
        Util.PlaySound(this.burrowLoopSoundString, this.gameObject);
        EffectManager.SpawnEffect(this.burrowEffectPrefab, new EffectData()
        {
            origin = this.characterBody.footPosition,
            scale = this.burrowRadius
        }, true);
        this.PlayAnimation("FullBody, Override", "Burrow", "Burrow.playbackRate", this.animDurationBurrow);
        if ((bool)this.characterMotor)
            this.characterMotor.walkSpeedPenaltyCoefficient = 0.0f;
        if ((bool)this.characterBody)
            this.characterBody.isSprinting = false;
        if ((bool)this.rigidbodyMotor)
            this.rigidbodyMotor.moveVector = Vector3.zero;
        this.waitingForBurrow = true;
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        this.HandleWaitForBurrow();
        if (this.fixedAge <= this.teleportDelay)
            return;
        this.outer.SetNextState(new ScorchlingBreach());
    }

    public override void OnExit()
    {
        base.OnExit();
        this.HandleWaitForBurrow();
    }

    private void HandleWaitForBurrow()
    {
        if (!this.waitingForBurrow || (double)this.fixedAge <= this.burrowAnimationDuration)
            return;
        this.sController.Burrow();
        this.waitingForBurrow = false;
    }

    private bool ShouldReposition()
    {
        bool shouldReposition = true;
        CharacterBody enemyCBody = this.characterBody.master.GetComponent<BaseAI>().currentEnemy?.characterBody;
        Vector3 breachPosition;

        if ((bool)enemyCBody)
        {
            Vector3 footPosition = this.characterBody.footPosition;
            NodeGraph nodeGraph = SceneInfo.instance.GetNodeGraph(MapNodeGroup.GraphType.Ground);
            Vector3 position1 = RaycastToFloor(enemyCBody.coreTransform.position);
            if (position1 == enemyCBody.coreTransform.position)
            {
                shouldReposition = false;
                return shouldReposition;
            }

            List<NodeGraph.NodeIndex> nodesInRange = nodeGraph.FindNodesInRange(position1, 10f, 20f, HullMask.Golem);
            Vector3 position2 = new Vector3();
            bool flag = false;
            int num1 = 35;
            while (!flag)
            {
                NodeGraph.NodeIndex nodeIndex = nodesInRange.ElementAt<NodeGraph.NodeIndex>(UnityEngine.Random.Range(1, nodesInRange.Count));
                nodeGraph.GetNodePosition(nodeIndex, out position2);
                double num2 = (double)Vector3.Distance(this.characterBody.coreTransform.position, position2);
                --num1;
                if (num2 > 35.0 || num1 < 0)
                    flag = true;
            }
            breachPosition = position2 + Vector3.up * 1.5f;

            if (breachPosition.x >= (footPosition.x - 2f) && breachPosition.x <= (footPosition.x + 2f) && breachPosition.z >= (footPosition.z - 2f) && breachPosition.z <= (footPosition.z + 2f) && breachPosition.y >= (footPosition.y - 5f) && breachPosition.y <= (footPosition.y + 5f))
            {
                shouldReposition = false;
            }
        }

        return shouldReposition;
    }

    private Vector3 RaycastToFloor(Vector3 position)
    {
        RaycastHit hitInfo;
        return Physics.Raycast(new Ray(position, Vector3.down), out hitInfo, 200f, (int)LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ? hitInfo.point : position;
    }

    public override InterruptPriority GetMinimumInterruptPriority()
    {
        return this.waitingForBurrow ? InterruptPriority.Frozen : InterruptPriority.Skill;
    }
}
