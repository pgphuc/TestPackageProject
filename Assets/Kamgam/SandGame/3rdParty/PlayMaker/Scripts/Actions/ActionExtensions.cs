#if PLAYMAKER
using HutongGames.PlayMaker;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tooltip = HutongGames.PlayMaker.TooltipAttribute;

namespace Kamgam.SandGame.PlayMaker
{
    public static class ActionExtensions
    {
        /// <summary>
        /// Used to set the inner values of a custom type wrapper.
        /// </summary>
        /// <param name="Result"></param>
        /// <param name="sandWorld"></param>
        /// <param name="reuseResultVariable"></param>
        public static void SetResultSandWorld(this FsmObject Result, SandWorld sandWorld, bool reuseResultVariable)
        {
            if (reuseResultVariable && Result != null && Result.Value != null)
            {
                SandWorldObject wrapper = Result.Value as SandWorldObject;
                if (wrapper != null)
                {
                    wrapper.SandWorld = sandWorld;
                    return;
                }
            }

            Result.Value = SandWorldObject.CreateInstance(sandWorld);
        }

        public static void SetResultGeneric(this FsmObject Result, object data, bool reuseResultVariable)
        {
            if (reuseResultVariable && Result != null && Result.Value != null)
            {
                GenericObject wrapper = Result.Value as GenericObject;
                if (wrapper != null)
                {
                    wrapper.Data = data;
                    return;
                }
            }

            Result.Value = GenericObject.CreateInstance(data);
        }
    }
}
#endif
