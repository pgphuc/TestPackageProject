#if PLAYMAKER
using HutongGames.PlayMaker;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Tooltip = HutongGames.PlayMaker.TooltipAttribute;
using Kamgam.SandGame;

namespace Kamgam.SandGame.PlayMaker
{
    [ActionCategory("Sand Game")]
    public class SandWorldDrawBrushAt : FsmStateAction
    {
        [RequiredField]
        [UIHint(UIHint.Variable)]
        [Tooltip("Sand World")]
        public FsmObject SandWorld;

        [RequiredField]
        [ObjectType(typeof(PixelMaterialId))]
        [Tooltip("Pixel Material")]
        public FsmEnum PixelMaterial;

        [RequiredField]
        [ObjectType(typeof(PixelWorldBrushShape))]
        [Tooltip("Brush Shape")]
        public FsmEnum BrushShape;

        [RequiredField]
        [Tooltip("Brush Size")]
        public FsmInt BrushSize;

        [RequiredField]
        [Tooltip("Position to draw at in pixel world.")]
        public FsmFloat PositionX;

        [RequiredField]
        [Tooltip("Position to draw at in pixel world.")]
        public FsmFloat PositionY;

        public override void OnEnter()
        {
            if (SandWorld.TryGetSandWorld(out var sandWorld) && sandWorld.PixelWorld != null)
            {
                sandWorld.PixelWorld.DrawBrushAt(
                    (PixelWorldBrushShape)BrushShape.Value,
                    BrushSize.Value,
                    PositionX.Value,
                    PositionY.Value,
                    (PixelMaterialId)PixelMaterial.Value);
            }

            Finish();
        }
    }
}
#endif
