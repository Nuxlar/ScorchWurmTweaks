using BepInEx;
using EntityStates;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using R2API;
using RoR2.Skills;
using RoR2.Projectile;
using KinematicCharacterController;
using RoR2.ContentManagement;
using EntityStates.Scorchling;

namespace ScorchWurmTweaks
{
  // TODO: look into potential ghost wurm bug, also implement better lava bomb
  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  public class Main : BaseUnityPlugin
  {
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Nuxlar";
    public const string PluginName = "ScorchWurmTweaks";
    public const string PluginVersion = "1.2.0";

    internal static Main Instance { get; private set; }
    public static string PluginDirectory { get; private set; }
    public static GameObject burrowEffectPrefab;

    private const BindingFlags allFlags = (BindingFlags)(-1);

    public void Awake()
    {
      Instance = this;

      Log.Init(Logger);

      { new ILHook(typeof(Main).GetMethod(nameof(BaseStateOnEnterCaller), allFlags), BaseStateOnEnterCallerMethodModifier); }

      ContentAddition.AddEntityState<CheckReposition>(out _);
      ContentAddition.AddEntityState<BetterLavaBomb>(out _);
      ContentAddition.AddEntityState<BetterScorchlingBurrow>(out _);

      LoadAssets();
      TweakBody();
      TweakBurrowDef();
      TweakBreachDef();
      TweakSkillDrivers();
      TweakProjectileGhost();
      TweakLavaBombDef();

      IL.ScorchlingController.Update += PreventBreakage;
      On.EntityStates.Scorchling.ScorchlingBreach.OnEnter += TweakBreachState;
      On.EntityStates.Scorchling.SpawnState.OnEnter += TweakSpawnState;
      On.ScorchlingController.Start += IncreaseTrailDelay;
      On.ScorchlingController.Burrow += ReplaceBurrow;
      On.ScorchlingController.Breach += ReplaceBreach;
    }

    private void PreventBreakage(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      Instruction instr = null;
      if (c.TryGotoNext(MoveType.AfterLabel,
        x => x.MatchLdarg(0),
        x => x.MatchLdfld(out _),
        x => x.MatchCallOrCallvirt<CharacterMotor>("get_" + nameof(CharacterMotor.isGrounded)),
        x => x.MatchBrtrue(out _),
        x => MatchAny(x, out instr)
      ))
      {
        c.Emit(OpCodes.Br, instr);
      }
    }

    private void IncreaseTrailDelay(On.ScorchlingController.orig_Start orig, ScorchlingController self)
    {
      self.timeInAirToShutOffDustTrail = 10f;
      orig(self);
    }

    private void TweakSpawnState(On.EntityStates.Scorchling.SpawnState.orig_OnEnter orig, EntityStates.Scorchling.SpawnState self)
    {
      orig(self);
      self.outer.SetNextState(new ScorchlingBreach());
    }

    private void ReplaceBurrow(On.ScorchlingController.orig_Burrow orig, ScorchlingController self)
    {
      self.isRecentlyBurrowed = true;
      self.isBurrowed = true;
      self.SetTeleportPermission(true);
      if (NetworkServer.active)
      {
        self.ensureBurrowSkill.stock = 0;
        if (self.characterBody.GetBuffCount(RoR2Content.Buffs.HiddenInvincibility.buffIndex) < 1)
          self.characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility.buffIndex);
      }
      self.originalLayer = self.gameObject.layer;
      self.gameObject.layer = LayerIndex.GetAppropriateFakeLayerForTeam(self.characterBody.teamComponent.teamIndex).intVal;
      if ((bool)self.baseDirt && (bool)self.dustTrail)
      {
        self.baseDirt.SetActive(false);
        self.dustTrail.SetActive(true);
        self.dustTrailActive = true;
        self.armatureObject.SetActive(false);
        self.meshObject.SetActive(false);
      }
      self.enemyDetection.Enable();
    }

    private void ReplaceBreach(On.ScorchlingController.orig_Breach orig, ScorchlingController self)
    {
      self.isBurrowed = false;
      self.SetTeleportPermission(false);
      if (NetworkServer.active)
      {
        self.characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility.buffIndex);
        self.lavaBombSkill.stock = 1;
        if ((bool)self.characterBody)
          self.characterBody.isSprinting = false;
        self.breachBaseDirtRotation = self.baseDirt.transform.rotation;
        self.breachBaseDirtRotation.eulerAngles = new Vector3(self.breachBaseDirtRotation.eulerAngles.x, Mathf.Floor(self.breachBaseDirtRotation.eulerAngles.y), self.breachBaseDirtRotation.eulerAngles.z);
        self.baseDirt.transform.localEulerAngles = Vector3.zero;
        self.baseDirt.transform.rotation = self.breachBaseDirtRotation;
      }
      self.gameObject.layer = self.originalLayer;
      if ((bool)self.baseDirt && (bool)self.dustTrail)
      {
        self.baseDirt.SetActive(true);
        self.dustTrail.SetActive(false);
        self.dustTrailActive = false;
        self.armatureObject.SetActive(true);
        self.meshObject.SetActive(true);
      }
      self.enemyDetection.Disable();
    }

    private void TweakBreachState(On.EntityStates.Scorchling.ScorchlingBreach.orig_OnEnter orig, EntityStates.Scorchling.ScorchlingBreach self)
    {
      BaseStateOnEnterCaller(self);

      self.crackToBreachTime = 1f;
      self.breachToBurrow = 1f;
      self.proceedImmediatelyToLavaBomb = false;
      self.amServer = NetworkServer.active;
      self.scorchlingController = self.characterBody.GetComponent<ScorchlingController>();
      Util.PlaySound(self.preBreachSoundString, self.gameObject);
      self.enemyCBody = self.characterBody.master.GetComponent<BaseAI>().currentEnemy?.characterBody;

      BreachComponentNux breachComponent = self.gameObject.GetComponent<BreachComponentNux>();
      if (breachComponent.breachPosition != Vector3.zero)
        self.breachPosition = breachComponent.breachPosition;
      else
        self.breachPosition = self.characterBody.footPosition;

      self.breachToBurrow += self.crackToBreachTime;
      self.burrowToEndOfTime += self.breachToBurrow;

      self.characterBody.SetAimTimer(self.breachToBurrow);
      TeleportHelper.TeleportBody(self.characterBody, self.breachPosition, false);
      EffectManager.SpawnEffect(self.crackEffectPrefab, new EffectData()
      {
        origin = self.breachPosition,
        scale = self.crackRadius
      }, true);
      self.scorchlingController.SetTeleportPermission(false);
    }

    private void LoadAssets()
    {
      AssetReferenceT<GameObject> vfxRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.VFXScorchlingEnterBurrow_prefab);
      AssetAsyncReferenceManager<GameObject>.LoadAsset(vfxRef).Completed += (x) => burrowEffectPrefab = x.Result;
    }

    private void TweakBreachDef()
    {
      AssetReferenceT<SkillDef> defRef = new AssetReferenceT<SkillDef>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.ScorchlingBreach_asset);
      AssetAsyncReferenceManager<SkillDef>.LoadAsset(defRef).Completed += (x) =>
      {
        SkillDef breachDef = x.Result;
        breachDef.activationState = new SerializableEntityStateType(typeof(CheckReposition));
      };
    }

    private void TweakLavaBombDef()
    {
      AssetReferenceT<SkillDef> defRef = new AssetReferenceT<SkillDef>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.ScorchlingLavaBomb_asset);
      AssetAsyncReferenceManager<SkillDef>.LoadAsset(defRef).Completed += (x) =>
      {
        SkillDef lavaBombDef = x.Result;
        lavaBombDef.activationState = new SerializableEntityStateType(typeof(BetterLavaBomb));
      };
    }

    private void TweakBurrowDef()
    {
      AssetReferenceT<SkillDef> defRef = new AssetReferenceT<SkillDef>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.ScorchlingEnsureBurrow_asset);
      AssetAsyncReferenceManager<SkillDef>.LoadAsset(defRef).Completed += (x) =>
      {
        SkillDef burrowDef = x.Result;
        burrowDef.activationState = new SerializableEntityStateType(typeof(CheckReposition));
        burrowDef.baseRechargeInterval = 12f;
      };
    }

    private void TweakBody()
    {
      AssetReferenceT<GameObject> bodyRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.ScorchlingBody_prefab);
      AssetAsyncReferenceManager<GameObject>.LoadAsset(bodyRef).Completed += (x) =>
      {
        CharacterBody body = x.Result.GetComponent<CharacterBody>();
        body.baseMoveSpeed = 0f;
        x.Result.AddComponent<BreachComponentNux>();
        Destroy(body.GetComponent<CharacterMotor>());
        Destroy(body.GetComponent<KinematicCharacterMotor>());
      };
    }

    private void TweakProjectileGhost()
    {
      AssetReferenceT<GameObject> ghostRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.LavaBombHeatOrbGhost_prefab);
      AssetAsyncReferenceManager<GameObject>.LoadAsset(ghostRef).Completed += (x) =>
      {
        // "RoR2/DLC2/Scorchling/LavaBombHeatOrbProjectile.prefab"
        // scorchlingBombZone.GetComponent<ProjectileDotZone>().lifetime = 6f;
        // scorchlingBombZone.GetComponent<DestroyOnTimer>().duration = 6f; // 3f
        GameObject scorchlingBombZoneGhost = x.Result;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 1f);
        // 8.84 8.84 8.84 indicator scale
        ObjectScaleCurve objectScaleCurve = scorchlingBombZoneGhost.transform.GetChild(0).gameObject.AddComponent<ObjectScaleCurve>();
        objectScaleCurve.overallCurve = curve;
        objectScaleCurve.useOverallCurveOnly = true;
        objectScaleCurve.timeMax = 0.25f;
      };
    }

    private void TweakSkillDrivers()
    {
      AssetReferenceT<GameObject> masterRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.ScorchlingMaster_prefab);
      AssetAsyncReferenceManager<GameObject>.LoadAsset(masterRef).Completed += (x) =>
      {
        AISkillDriver[] skillDrivers = x.Result.GetComponents<AISkillDriver>();
        foreach (AISkillDriver skillDriver in skillDrivers)
        {
          if (skillDriver.customName == "ChaseOffNodegraphClose" || skillDriver.customName == "FollowNodeGraphToTarget")
          {
            GameObject.Destroy(skillDriver);
          }
          if (skillDriver.customName == "ChaseOffNodegraph")
          {
            skillDriver.customName = "Stare";
            skillDriver.maxDistance = float.PositiveInfinity;
            skillDriver.minDistance = 0f;
            skillDriver.skillSlot = SkillSlot.None;
            skillDriver.requireSkillReady = false;
          }
          if (skillDriver.customName == "Breach")
          {
            skillDriver.maxDistance = float.PositiveInfinity;
            skillDriver.skillSlot = RoR2.SkillSlot.Utility;
            skillDriver.requiredSkill = null;
            skillDriver.activationRequiresAimConfirmation = false;
            skillDriver.aimType = AISkillDriver.AimType.AtMoveTarget;
          }
          if (skillDriver.customName == "LavaBomb")
          {
            skillDriver.maxDistance = 50f;
            skillDriver.noRepeat = false;
          }
        }
      };
    }

    // self is just for being able to call self.OnEnter() inside hooks.
    private static void BaseStateOnEnterCaller(BaseState self)
    {

    }

    // self is just for being able to call self.OnEnter() inside hooks.
    private static void BaseStateOnEnterCallerMethodModifier(ILContext il)
    {
      var cursor = new ILCursor(il);
      cursor.Emit(OpCodes.Ldarg_0);
      cursor.Emit(OpCodes.Call, typeof(BaseState).GetMethod(nameof(BaseState.OnEnter), allFlags));
    }

    public static bool MatchAny(Instruction instr, out Instruction instruction)
    {
      instruction = instr;
      return true;
    }
  }
}