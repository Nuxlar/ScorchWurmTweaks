using RoR2;
using UnityEngine;
using EntityStates;
using EntityStates.Scorchling;

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

        if (this.sController.isBurrowed)
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

    public override InterruptPriority GetMinimumInterruptPriority()
    {
        return this.waitingForBurrow ? InterruptPriority.Frozen : InterruptPriority.Skill;
    }
}
