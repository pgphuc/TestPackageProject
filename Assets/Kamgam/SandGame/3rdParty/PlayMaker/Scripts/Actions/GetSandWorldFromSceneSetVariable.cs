#if PLAYMAKER
using HutongGames.PlayMaker;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Tooltip = HutongGames.PlayMaker.TooltipAttribute;

namespace Kamgam.SandGame.PlayMaker
{
    [ActionCategory("Sand Game")]
    public class GetSandWorldFromSceneSetVariable : GetSandWorldFromSceneBase
    {
        [ActionSection("Set Variable")]

        [RequiredField]
        [UIHint(UIHint.Variable)]
        [ObjectType(typeof(SandWorldObject))]
        [Tooltip("Contains the found sand world object.\n" +
            "Actually it contains a FsmObject > SandWorldObj > Value, where Value = SandWorld.")]
        public FsmObject StoreSandWorld;

        [Tooltip("If enabled then the Store variable is reused to preserve memory.\n" +
            "Only enable this if you see a need for it in the Profiler.")]
        public bool ReuseStoreVariable = false;

        public override void OnWorldQueried(SandWorld sandWorld)
        {
            StoreSandWorld.SetResultSandWorld(sandWorld, ReuseStoreVariable);
        }

#if UNITY_EDITOR
        public override string AutoName()
        {
            return base.AutoName() + " > " + StoreSandWorld.Name;
        }
#endif
    }
}
#endif
