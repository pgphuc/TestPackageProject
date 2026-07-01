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
    /// <summary>
    /// Base for all actions that are querying the visual tree for one single element.
    /// </summary>
    public abstract class GetSandWorldFromSceneBase : SandWorldActionBase
    {
        public override void OnEnterWithSandWorld(SandWorld sandWorld)
        {
            OnWorldQueried(sandWorld);

            if (!Finished)
                Finish();
        }

        public abstract void OnWorldQueried(SandWorld sandWorld);

#if UNITY_EDITOR
        public override string AutoName()
        {
            return "Sand World";
        }
#endif
    }
}
#endif
