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

namespace ScorchWurmTweaks
{
  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  public class Main : BaseUnityPlugin
  {
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Nuxlar";
    public const string PluginName = "ScorchWurmTweaks";
    public const string PluginVersion = "1.1.0";

    internal static Main Instance { get; private set; }
    public static string PluginDirectory { get; private set; }
    public static GameObject burrowEffectPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC2/Scorchling/VFXScorchlingEnterBurrow.prefab").WaitForCompletion();

    private const BindingFlags allFlags = (BindingFlags)(-1);
    private GameObject scorchlingMaster = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC2/Scorchling/ScorchlingMaster.prefab").WaitForCompletion();
    private SkillDef burrowDef = Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC2/Scorchling/ScorchlingEnsureBurrow.asset").WaitForCompletion();
    private SkillDef breachDef = Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC2/Scorchling/ScorchlingBreach.asset").WaitForCompletion();


    public void Awake()
    {
      Instance = this;

      Stopwatch stopwatch = Stopwatch.StartNew();

      Log.Init(Logger);

      ContentAddition.AddEntityState<BetterScorchlingBurrow>(out _);
      burrowDef.activationState = new SerializableEntityStateType(typeof(BetterScorchlingBurrow));
      burrowDef.baseRechargeInterval = 12f;
      //breachDef.baseRechargeInterval = 12f;

      AISkillDriver[] skillDrivers = scorchlingMaster.GetComponents<AISkillDriver>();
      foreach (AISkillDriver skillDriver in skillDrivers)
      {
        if (skillDriver.customName == "ChaseOffNodegraphClose" || skillDriver.customName == "ChaseOffNodegraph" || skillDriver.customName == "FollowNodeGraphToTarget")
        {
          GameObject.Destroy(skillDriver);
        }
        if (skillDriver.customName == "Breach")
        {
          skillDriver.maxDistance = float.PositiveInfinity;
          skillDriver.skillSlot = RoR2.SkillSlot.Utility;
          skillDriver.requiredSkill = null;
          skillDriver.activationRequiresAimConfirmation = false;
          skillDriver.aimType = AISkillDriver.AimType.AtMoveTarget;
          //skillDriver.noRepeat = false;
        }
        if (skillDriver.customName == "LavaBomb")
        {
          //  skillDriver.activationRequiresTargetLoS = true;
          skillDriver.maxDistance = 50;
          skillDriver.noRepeat = false;
          // skillDriver.activationRequiresTargetLoS = true;
        }
      }

      On.EntityStates.Scorchling.ScorchlingBreach.OnEnter += TweakBreachState;
      On.EntityStates.Scorchling.SpawnState.OnEnter += TweakSpawn;
      On.ScorchlingController.Burrow += TweakBurrow;
      On.ScorchlingController.Breach += TweakBreach;

      stopwatch.Stop();
      Log.Info_NoCallerPrefix($"Initialized in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    private void TweakSpawn(On.EntityStates.Scorchling.SpawnState.orig_OnEnter orig, EntityStates.Scorchling.SpawnState self)
    {
      orig(self);
      self.outer.SetNextState(new EntityStates.Scorchling.ScorchlingBreach());
    }

    private void TweakBurrow(On.ScorchlingController.orig_Burrow orig, ScorchlingController self)
    {
      self.isRecentlyBurrowed = true;
      self.isBurrowed = true;
      self.SetTeleportPermission(true);
      if (NetworkServer.active)
      {
        // self.breachSkill.stock = 1;
        // self.lavaBombSkill.stock = 0;
        self.ensureBurrowSkill.stock = 0;
        self.characterMotor.walkSpeedPenaltyCoefficient = 1f;
        if (self.characterBody.GetBuffCount(RoR2Content.Buffs.HiddenInvincibility.buffIndex) < 1)
          self.characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility.buffIndex);
      }
      self.originalLayer = self.gameObject.layer;
      self.gameObject.layer = LayerIndex.GetAppropriateFakeLayerForTeam(self.characterBody.teamComponent.teamIndex).intVal;
      self.characterMotor.Motor.RebuildCollidableLayers();
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

    private void TweakBreach(On.ScorchlingController.orig_Breach orig, ScorchlingController self)
    {
      self.isBurrowed = false;
      self.SetTeleportPermission(false);
      if (NetworkServer.active)
      {
        self.characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility.buffIndex);
        // self.breachSkill.stock = 0;
        self.lavaBombSkill.stock = 1;
        // self.ensureBurrowSkill.stock = 5;
        self.characterMotor.walkSpeedPenaltyCoefficient = 0.0f;
        if ((bool)self.characterBody)
          self.characterBody.isSprinting = false;
        if ((bool)self.rigidBodyMotor)
          self.rigidBodyMotor.moveVector = Vector3.zero;
        self.breachBaseDirtRotation = self.baseDirt.transform.rotation;
        self.breachBaseDirtRotation.eulerAngles = new Vector3(self.breachBaseDirtRotation.eulerAngles.x, Mathf.Floor(self.breachBaseDirtRotation.eulerAngles.y), self.breachBaseDirtRotation.eulerAngles.z);
        self.baseDirt.transform.localEulerAngles = Vector3.zero;
        self.baseDirt.transform.rotation = self.breachBaseDirtRotation;
      }
      self.gameObject.layer = self.originalLayer;
      self.characterMotor.Motor.RebuildCollidableLayers();
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
      new ILHook(typeof(Main).GetMethod(nameof(BaseStateOnEnterCaller), allFlags), BaseStateOnEnterCallerMethodModifier);

      // self.proceedImmediatelyToLavaBomb = true;
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
          NodeGraph.NodeIndex nodeIndex = nodesInRange.ElementAt<NodeGraph.NodeIndex>(UnityEngine.Random.Range(1, nodesInRange.Count));
          nodeGraph.GetNodePosition(nodeIndex, out position2);
          double num2 = (double)Vector3.Distance(self.characterBody.coreTransform.position, position2);
          --num1;
          if (num2 > 35.0 || num1 < 0)
            flag = true;
        }
        self.breachPosition = position2 + Vector3.up * 1.5f;
      }

      if ((bool)self.characterMotor)
        self.characterMotor.walkSpeedPenaltyCoefficient = 0.0f;
      self.characterBody.SetAimTimer(self.breachToBurrow);
      TeleportHelper.TeleportBody(self.characterBody, self.breachPosition);
      EffectManager.SpawnEffect(self.crackEffectPrefab, new EffectData()
      {
        origin = self.breachPosition,
        scale = self.crackRadius
      }, true);
      self.scorchlingController.SetTeleportPermission(false);
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