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
    public class SandWorldDebugLogElement : FsmStateAction
    {
        [ActionSection("Sand World Source")]

        [RequiredField]
        [UIHint(UIHint.Variable)]
        [Tooltip("Source of the Sand World.")]
        public FsmObject SandWorld;

        public override void OnEnter()
        {
            if (SandWorld.TryGetSandWorld(out var element))
            {
                Debug.Log(element);
            }

            Finish();
        }
    }
}
#endif
