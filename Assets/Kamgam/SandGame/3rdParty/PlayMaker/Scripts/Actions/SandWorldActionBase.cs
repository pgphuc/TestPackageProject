#if PLAYMAKER
using HutongGames.PlayMaker;
using Tooltip = HutongGames.PlayMaker.TooltipAttribute;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Kamgam.SandGame;

namespace Kamgam.SandGame.PlayMaker
{
    /// <summary>
    /// This class is the base for all actions that need to access a UI Document. It fetches the document from a game object.
    /// </summary>
    public abstract class SandWorldActionBase : FsmStateAction
    {
        [ActionSection("Sand Game")]

        public FsmOwnerDefault SandWorldSource;

        [HideIf("_hideWorld")]
        [ObjectType(typeof(UIDocument))]
        [Tooltip("Optional: If not specified then the SandWorld will be fetched from the 'Game Object' field via GetComponent().")]
        public FsmObject SandWorld;

        protected SandWorld _cachedWorld;
        protected bool _didCacheWorld = false;
        public bool _hideWorld()
        {
            return (SandWorldSource != null && SandWorldSource.OwnerOption == OwnerDefaultOption.UseOwner) || (SandWorldSource.OwnerOption == OwnerDefaultOption.SpecifyGameObject && SandWorldSource.GameObject.Value != null);
        }

        public void ClearCache()
        {
            _didCacheWorld = false;
            _cachedWorld = null;
        }

        public override void OnEnter()
        {
            if(_didCacheWorld)
            {
                OnEnterWithSandWorld(_cachedWorld);
                return;
            }

            bool goIsNull = false;
            if (SandWorldSource.OwnerOption == OwnerDefaultOption.SpecifyGameObject && (SandWorldSource == null || SandWorldSource.GameObject == null || SandWorldSource.GameObject.Value == null))
            {
                goIsNull = true;
            }

            SandWorld world = null;

            bool docIsNull = false;
            if (SandWorld == null || SandWorld.Value == null)
            {
                docIsNull = true;
            }

            if (goIsNull && docIsNull)
            {
                // Both are null then abort.
                Finish();
                return;
            }

            if (docIsNull)
            {
                var go = SandWorldSource.OwnerOption == OwnerDefaultOption.SpecifyGameObject ? SandWorldSource.GameObject.Value : Owner;
                world = go.GetComponent<SandWorld>();
            }
            else
            {
                world = SandWorld.Value as SandWorld;
            }

            // If doc is still null then abort.
            if (world == null)
            {
                Finish();
                return;
            }

            _didCacheWorld = true;
            _cachedWorld = world;

            OnEnterWithSandWorld(_cachedWorld);

            if(!Finished)
                Finish();
        }

        public abstract void OnEnterWithSandWorld(SandWorld sandWorld);

    }
}
#endif
