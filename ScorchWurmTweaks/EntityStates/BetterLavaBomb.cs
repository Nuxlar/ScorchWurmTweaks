/*
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using System.Linq;
using UnityEngine;

namespace ScorchWurmTweaks;

public class BetterLavaBomb : EntityStates.BaseState
{
    public float breachToSpitTime = 1f;
    public float spitToLaunchTime = 0.3f;
    public float spitToBurrowTime = 5f;
    public float burrowToEndOfTime = 1f;
    public float animDurationBreach = 1f;
    public float animDurationSpit = 1f;
    public float animDurationBurrow = 1f;
    public float animDurationPostSpit = 0.75f;
    public float percentageToFireProjectile = 0.75f;
    public GameObject burrowEffectPrefab;
    public float burrowRadius = 1f;
    public string breachSoundString;
    public string spitSoundString;
    public string burrowSoundString;
    public string burrowLoopSoundString;
    public string burrowStopLoopSoundString;
    [Header("Lava Bomb Projectile")]
    public static GameObject mortarProjectilePrefab;
    public static GameObject mortarMuzzleflashEffect;
    public static int mortarCount;
    public static string mortarMuzzleName;
    public static string mortarSoundString;
    public static float mortarDamageCoefficient;
    public static float timeToTarget = 3f;
    public static float projectileVelocity = 55f;
    public static float minimumDistance;
    private bool spitStarted;
    private bool firedProjectile;
    private bool earlyExit;
    private ScorchlingController sController;
    private CharacterBody enemyCBody;

    public override void OnEnter()
    {
        base.OnEnter();
        this.sController = this.characterBody.GetComponent<ScorchlingController>();
        this.animDurationBreach = this.sController.isBurrowed ? this.animDurationBreach : 0.0f;
        this.spitToBurrowTime += this.animDurationBreach + this.animDurationSpit;
        this.burrowToEndOfTime += this.spitToBurrowTime;
        this.enemyCBody = this.characterBody.master.GetComponent<BaseAI>().currentEnemy?.characterBody;

        if (this.sController.isBurrowed)
        {
            this.earlyExit = true;
            if (!Util.HasEffectiveAuthority(this.characterBody.networkIdentity))
                return;
            this.outer.SetNextState((EntityState)new ScorchlingBreach()
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
            int num = (int)Util.PlaySound(this.spitSoundString, this.gameObject);
            EffectManager.SimpleMuzzleFlash(ScorchlingLavaBomb.mortarMuzzleflashEffect, this.gameObject, ScorchlingLavaBomb.mortarMuzzleName, false);
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
        Ray ray1 = new Ray(child.position, child.forward);
        Ray ray2 = new Ray(ray1.origin, Vector3.up);
        if (this.enemyCBody)
        {
            this.enemyCBody.mainHurtBox
         }
        BullseyeSearch bullseyeSearch = new BullseyeSearch();
        bullseyeSearch.searchOrigin = ray1.origin;
        bullseyeSearch.searchDirection = ray1.direction;
        bullseyeSearch.filterByLoS = false;
        bullseyeSearch.teamMaskFilter = TeamMask.allButNeutral;
        if ((bool)this.teamComponent)
            bullseyeSearch.teamMaskFilter.RemoveTeam(this.teamComponent.teamIndex);
        bullseyeSearch.sortMode = BullseyeSearch.SortMode.Angle;
        bullseyeSearch.RefreshCandidates();
        HurtBox hurtBox = bullseyeSearch.GetResults().FirstOrDefault<HurtBox>();
        bool flag = false;
        Vector3 vector3_1 = Vector3.zero;
        if ((bool)hurtBox)
        {
            vector3_1 = hurtBox.transform.position;
            flag = true;
        }
        else
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(ray1, out hitInfo, 1000f, (int)LayerIndex.world.mask | (int)LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
            {
                vector3_1 = hitInfo.point;
                flag = true;
            }
        }
        float speedOverride = ScorchlingLavaBomb.projectileVelocity;
        if (flag)
        {
            Vector3 vector3_2 = vector3_1 - ray2.origin;
            Vector2 vector2_1 = new Vector2(vector3_2.x, vector3_2.z);
            float num1 = vector2_1.magnitude;
            Vector2 vector2_2 = vector2_1 / num1;
            if (num1 < ScorchlingLavaBomb.minimumDistance)
                num1 = ScorchlingLavaBomb.minimumDistance;
            float initialYspeed = Trajectory.CalculateInitialYSpeed(ScorchlingLavaBomb.timeToTarget, vector3_2.y);
            float num2 = num1 / ScorchlingLavaBomb.timeToTarget;
            Vector3 vector3_3 = new Vector3(vector2_2.x * num2, initialYspeed, vector2_2.y * num2);
            speedOverride = vector3_3.magnitude;
            ray2.direction = vector3_3;
        }
        Quaternion rotation = Util.QuaternionSafeLookRotation(ray2.direction);
        ProjectileManager.instance.FireProjectileWithoutDamageType(ScorchlingLavaBomb.mortarProjectilePrefab, ray2.origin, rotation, this.gameObject, this.damageStat * ScorchlingLavaBomb.mortarDamageCoefficient, 0.0f, Util.CheckRoll(this.critStat, this.characterBody.master), speedOverride: speedOverride);
    }

    public override void OnExit() => base.OnExit();

    public override InterruptPriority GetMinimumInterruptPriority()
    {
        return InterruptPriority.PrioritySkill;
    }
}
*/