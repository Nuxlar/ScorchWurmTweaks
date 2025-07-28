using EntityStates;
using EntityStates.Scorchling;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using System.Linq;
using UnityEngine;

namespace ScorchWurmTweaks;

public class BetterLavaBomb : EntityStates.BaseState
{
    public float breachToSpitTime = 1f;
    public float spitToLaunchTime = 0f;
    public float spitToBurrowTime = 4f;
    public float burrowToEndOfTime = 1f;
    public float animDurationBreach = 1f;
    public float animDurationSpit = 1f;
    public float animDurationBurrow = 1f;
    public float animDurationPostSpit = 1f;
    public float percentageToFireProjectile = 0.75f;
    public GameObject burrowEffectPrefab;
    public float burrowRadius = 1f;
    public static int mortarCount;
    public static float mortarDamageCoefficient = 2f;
    public string spitSoundString = "_shootcorchling_slagBomb_shoot";
    public static float timeToTarget = 3f;
    public static float projectileVelocity = 55f;

    private bool spitStarted;
    private bool firedProjectile;
    private bool earlyExit;
    private ScorchlingController sController;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.LogWarning(new ScorchlingLavaBomb().spitSoundString);
        this.sController = this.characterBody.GetComponent<ScorchlingController>();
        this.animDurationBreach = this.sController.isBurrowed ? this.animDurationBreach : 0.0f;
        this.spitToBurrowTime += this.animDurationBreach + this.animDurationSpit;
        this.burrowToEndOfTime += this.spitToBurrowTime;

        if (this.sController.isBurrowed)
        {
            this.earlyExit = true;
            if (!Util.HasEffectiveAuthority(this.characterBody.networkIdentity))
                return;
            this.outer.SetNextState(new ScorchlingBreach()
            {
                proceedImmediatelyToLavaBomb = true,
                breachToBurrow = this.breachToSpitTime
            });
        }
        else
            this.characterBody.SetAimTimer(this.burrowToEndOfTime);
    }

    public override void FixedUpdate()
    {
        if (this.earlyExit)
            return;
        base.FixedUpdate();
        if (!this.spitStarted && this.fixedAge > this.animDurationBreach)
        {
            this.spitStarted = true;
            this.PlayAnimation("FullBody, Override", "Spit", "Spit.playbackRate", this.animDurationSpit);
        }
        if (this.spitStarted && !this.firedProjectile && this.fixedAge > this.animDurationSpit * this.percentageToFireProjectile + this.animDurationBreach)
        {
            this.firedProjectile = true;
            Util.PlaySound(this.spitSoundString, this.gameObject);
            EffectManager.SimpleMuzzleFlash(ScorchlingLavaBomb.mortarMuzzleflashEffect, this.gameObject, "MuzzleFire", false);
            if (this.isAuthority)
                this.Spit();
        }
        if (!this.firedProjectile || this.fixedAge <= this.animDurationBreach + this.animDurationSpit + this.animDurationPostSpit)
            return;
        this.outer.SetNextStateToMain();
    }

    public void Spit()
    {
        Transform child = this.characterBody.modelLocator.modelTransform.GetComponent<ChildLocator>().FindChild("MuzzleFire");

        Ray aimRay = GetAimRay();
        Quaternion rotation = Util.QuaternionSafeLookRotation(aimRay.direction);
        Debug.LogWarning(ScorchlingLavaBomb.projectileVelocity);
        ProjectileManager.instance.FireProjectileWithoutDamageType(ScorchlingLavaBomb.mortarProjectilePrefab, child.position, rotation, this.gameObject, this.damageStat * BetterLavaBomb.mortarDamageCoefficient, 0.0f, Util.CheckRoll(this.critStat, this.characterBody.master), speedOverride: BetterLavaBomb.projectileVelocity);
    }

    public override void OnExit() => base.OnExit();

    public override InterruptPriority GetMinimumInterruptPriority()
    {
        return InterruptPriority.PrioritySkill;
    }
}