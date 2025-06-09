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

namespace ScorchWurmTweaks
{
  // TODO: look into potential ghost wurm bug, also implement better lava bomb
  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  public class Main : BaseUnityPlugin
  {
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Nuxlar";
    public const string PluginName = "ScorchWurmTweaks";
    public const string PluginVersion = "1.1.1";

    internal static Main Instance { get; private set; }
    public static string PluginDirectory { get; private set; }
    public static GameObject burrowEffectPrefab;

    private const BindingFlags allFlags = (BindingFlags)(-1);

    public void Awake()
    {
      Instance = this;

      Stopwatch stopwatch = Stopwatch.StartNew();

      Log.Init(Logger);

      new ILHook(typeof(Main).GetMethod(nameof(BaseStateOnEnterCaller), allFlags), BaseStateOnEnterCallerMethodModifier);

      ContentAddition.AddEntityState<BetterScorchlingBurrow>(out _);
      // TODO: rewrite the component and entitystates cuz it freaks out in mp for some reason, man...
      LoadAssets();
      TweakBody();
      TweakBurrowDef();
      TweakBreachDef();
      TweakSkillDrivers();
      TweakProjectileGhost();

      On.EntityStates.Scorchling.ScorchlingBreach.OnEnter += TweakBreachState;
      On.EntityStates.Scorchling.SpawnState.OnEnter += TweakSpawnState;
      On.ScorchlingController.Burrow += TweakBurrow;
      On.ScorchlingController.Breach += TweakBreach;

      stopwatch.Stop();
      Log.Info_NoCallerPrefix($"Initialized in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    private void TweakSpawnState(On.EntityStates.Scorchling.SpawnState.orig_OnEnter orig, EntityStates.Scorchling.SpawnState self)
    {
      orig(self);
      self.outer.SetNextState(new BetterScorchlingBurrow());
    }

    private void TweakBurrow(On.ScorchlingController.orig_Burrow orig, ScorchlingController self)
    {
      orig(self);
      self.breachSkill.stock = 0;
      self.lavaBombSkill.stock = 0;
      self.ensureBurrowSkill.stock = 0;
    }

    private void TweakBreach(On.ScorchlingController.orig_Breach orig, ScorchlingController self)
    {
      orig(self);
      self.breachSkill.stock = 0;
      self.lavaBombSkill.stock = 1;
      self.ensureBurrowSkill.stock = 0;
    }

    private void TweakBreachState(On.EntityStates.Scorchling.ScorchlingBreach.orig_OnEnter orig, EntityStates.Scorchling.ScorchlingBreach self)
    {
      { new ILHook(typeof(Main).GetMethod(nameof(BaseStateOnEnterCaller), allFlags), BaseStateOnEnterCallerMethodModifier); }

      self.crackToBreachTime = 2f;
      self.breachToBurrow = 2.1f;
      self.proceedImmediatelyToLavaBomb = false;
      self.amServer = NetworkServer.active;
      self.scorchlingController = self.characterBody.GetComponent<ScorchlingController>();
      Util.PlaySound(self.preBreachSoundString, self.gameObject);
      if (!self.amServer)
        return;
      self.enemyCBody = self.characterBody.master.GetComponent<BaseAI>().currentEnemy?.characterBody;
      if (self.proceedImmediatelyToLavaBomb)
        self.breachToBurrow = 1f;
      self.breachToBurrow += self.crackToBreachTime;
      self.burrowToEndOfTime += self.breachToBurrow;
      self.breachPosition = self.characterBody.footPosition;

      if ((bool)self.enemyCBody)
      {
        NodeGraph nodeGraph = SceneInfo.instance.GetNodeGraph(MapNodeGroup.GraphType.Ground);
        Vector3 position1 = self.enemyCBody.coreTransform.position;
        List<NodeGraph.NodeIndex> nodesInRange = nodeGraph.FindNodesInRange(position1, 10f, 20f, HullMask.Golem);
        Vector3 position2 = new Vector3();
        bool flag = false;
        int num1 = 35;
        while (!flag)
        {
          NodeGraph.NodeIndex nodeIndex = nodesInRange.ElementAt<NodeGraph.NodeIndex>(UnityEngine.Random.Range(0, nodesInRange.Count));
          nodeGraph.GetNodePosition(nodeIndex, out position2);
          double num2 = (double)Vector3.Distance(self.characterBody.coreTransform.position, position2);
          --num1;
          if (num2 > 35.0 || num1 < 0)
            flag = true;
        }
        self.breachPosition = position2 + Vector3.up * 1.5f;
      }

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
        breachDef.activationState = new SerializableEntityStateType(typeof(BetterScorchlingBurrow));
      };
    }

    private void TweakBurrowDef()
    {
      AssetReferenceT<SkillDef> defRef = new AssetReferenceT<SkillDef>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC2_Scorchling.ScorchlingEnsureBurrow_asset);
      AssetAsyncReferenceManager<SkillDef>.LoadAsset(defRef).Completed += (x) =>
      {
        SkillDef burrowDef = x.Result;
        burrowDef.activationState = new SerializableEntityStateType(typeof(BetterScorchlingBurrow));
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
        //  Destroy(scorchlingBody.GetComponent<CharacterMotor>());
        //  Destroy(scorchlingBody.GetComponent<KinematicCharacterMotor>());
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
  }
}